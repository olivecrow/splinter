using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32.SafeHandles;
using Splinter.Core;

namespace Splinter.Platform.Windows;

// The project lock serializes Splinter clients. These handles also exclude
// external writes/renames to existing affected files and parent identities.
// Publication never replaces an occupant of a name, including after recovery.
public sealed class WindowsOriginalFilePublisher : IOriginalFilePublisher
{
    private readonly Action<string, int>? boundary;
    private readonly bool allowPlasticAdministration;
    public WindowsOriginalFilePublisher(bool allowPlasticAdministration = false) => this.allowPlasticAdministration = allowPlasticAdministration;
    internal WindowsOriginalFilePublisher(Action<string, int> boundary, bool allowPlasticAdministration = false)
    { this.boundary = boundary; this.allowPlasticAdministration = allowPlasticAdministration; }
    public string RootIdentity(string project)
    {
        using var guard = new Guard(project, [], allowPlasticAdministration);
        return Id(guard.Root);
    }
    public OriginalFileObservation? Observe(string project, string relativePath, CancellationToken ct = default)
    {
        using var guard = new Guard(project, [], allowPlasticAdministration);
        return guard.Observe(relativePath, ct);
    }

    public void Publish(OriginalApplyState state, Func<OriginalFileVersion, Stream> openPayload, Action persist, CancellationToken ct = default)
    {
        var plan = state.Plan;
        if (allowPlasticAdministration && plan.SourceIdentity is null) throw Conflict("An explicit Plastic source guard is required.");
        using var guard = new Guard(plan.ProjectPath, plan.Changes.Where(c => c.Before?.Kind == "directory" && c.After is null).Select(c => c.Path).ToArray(), allowPlasticAdministration);
        if (Id(guard.Root) != plan.ProjectIdentity) throw Conflict("The physical original root changed.");
        string intent = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(state.IntentKey)))[..24];
        string Prefix(int i) => $".splinter-apply-{plan.OwnerId}-{intent}-{i}";

        // Open/check the entire write set before creating a stage or mutating
        // the original. The leases stay held until this call returns.
        for (int i = 0; i < plan.Changes.Length; i++)
        {
            ct.ThrowIfCancellationRequested();
            var change = plan.Changes[i]; var step = state.Steps[i];
            var current = guard.Observe(change.Path, ct, mutation: true);
            var staged = guard.Observe(Sibling(change.Path, Prefix(i) + ".new"), ct, mutation: true);
            var retired = guard.Observe(Sibling(change.Path, Prefix(i) + ".old"), ct, mutation: true);
            if (staged is not null && (step.StagedIdentity is null || staged.Identity != step.StagedIdentity))
                throw Conflict("Preserve an unowned or replaced publication stage: " + change.Path);
            if (retired is not null && retired.Identity != change.Expected?.Identity)
                throw Conflict("Preserve an unowned or replaced retired file: " + change.Path);
            if (step.Phase == "done" || step.Phase == "published")
            {
                if (!Matches(current, change.After) || !change.AlreadyMatches && change.After is not null && current?.Identity != step.StagedIdentity)
                    throw Conflict("A previously published original changed: " + change.Path);
            }
            else if (current?.Identity == step.StagedIdentity && step.StagedIdentity is not null)
            {
                if (!Matches(current, change.After)) throw Conflict("The published stage changed: " + change.Path);
            }
            else if (retired is not null || step.Phase == "retired")
            {
                if (current is not null && !Same(current, change.Expected)) throw Conflict("A new original occupies the retired name: " + change.Path);
                if (retired is not null && !Matches(retired, change.Before)) throw Conflict("The retired original changed: " + change.Path);
            }
            else if (!Same(current, change.Expected)) throw Conflict("The original changed after planning: " + change.Path);
            if (step.Phase == "pending" && (staged is not null || retired is not null))
                throw Conflict("A publication name already exists: " + change.Path);
        }
        boundary?.Invoke("preflight", -1);
        for (int i = 0; i < plan.Changes.Length; i++)
        {
            ct.ThrowIfCancellationRequested();
            var c = plan.Changes[i]; var s = state.Steps[i];
            string stagePath = Sibling(c.Path, Prefix(i) + ".new"), backupPath = Sibling(c.Path, Prefix(i) + ".old");
            if (s.Phase == "done") continue;
            if (c.AlreadyMatches) { s.Phase = "done"; persist(); continue; }
            var original = guard.Observe(c.Path, ct, mutation: true);
            if (c.After is null)
            {
                if (original is not null)
                {
                    if (!Same(original, c.Expected)) throw Conflict("The deletion target changed: " + c.Path);
                    s.Phase = "retired"; persist();
                    guard.Delete(c.Path, c.Expected!.Identity);
                    boundary?.Invoke("deleted", i);
                }
                s.Phase = "done"; persist(); continue;
            }
            // A rename may have completed before its receipt was saved.
            if (original?.Identity == s.StagedIdentity && Matches(original, c.After))
            { s.Phase = "published"; persist(); }
            if (s.Phase != "published")
            {
                var stage = guard.Observe(stagePath, ct, mutation: true);
                if (stage is null)
                {
                    if (s.StagedIdentity is not null) throw Conflict("The owned stage is missing: " + c.Path);
                    s.Phase = "staging"; persist();
                    var handle = guard.Create(stagePath, c.After.Kind == "directory");
                    boundary?.Invoke("stage-created-before-receipt", i);
                    s.StagedIdentity = Id(handle); persist();
                    boundary?.Invoke("stage-created", i);
                }
                if (s.Phase == "staging")
                {
                    guard.Fill(stagePath, s.StagedIdentity!, c.After, openPayload, ct);
                    if (original is not null) guard.CopySecurity(c.Path, stagePath);
                    guard.SetMetadata(stagePath, c.After, original is null ? null : c.Path);
                    s.StagedBytes = c.After.Length; s.Phase = "staged"; persist();
                    boundary?.Invoke("staged", i);
                }
                stage = guard.Observe(stagePath, ct, mutation: true);
                if (stage?.Identity != s.StagedIdentity || !Matches(stage, c.After)) throw Conflict("The owned payload stage changed: " + c.Path);
                if (original is not null)
                {
                    original = guard.Observe(c.Path, ct, mutation: true);
                    if (!Same(original, c.Expected)) throw Conflict("The original changed before retirement: " + c.Path);
                    boundary?.Invoke("original-checked", i);
                    guard.Rename(c.Path, backupPath, c.Expected!.Identity);
                    boundary?.Invoke("retired-before-receipt", i);
                    s.Phase = "retired"; persist();
                    boundary?.Invoke("retired", i);
                }
                guard.Rename(stagePath, c.Path, s.StagedIdentity!);
                boundary?.Invoke("published-before-receipt", i);
                s.Phase = "published"; persist();
                boundary?.Invoke("published", i);
            }
            var backup = guard.Observe(backupPath, ct, mutation: true);
            if (backup is not null)
            {
                if (!Same(backup, c.Expected)) throw Conflict("The retired original changed before cleanup: " + c.Path);
                guard.Delete(backupPath, backup.Identity);
                boundary?.Invoke("retired-removed", i);
            }
            s.Phase = "done"; persist();
        }
        state.Completed = true; persist();
        boundary?.Invoke("completed", -1);
    }

    private static string Sibling(string relative, string leaf) => relative.Contains('/') ? relative[..(relative.LastIndexOf('/') + 1)] + leaf : leaf;
    private static bool Same(OriginalFileObservation? a, OriginalFileObservation? b) => a is null ? b is null :
        b is not null && a.Identity == b.Identity && Matches(a, b.Version);
    private static bool Matches(OriginalFileObservation? value, OriginalFileVersion? expected) => expected is null ? value is null :
        expected.SameContent(value?.Version) && expected.RelevantAttributes == value!.Version.RelevantAttributes;
    private static WorkspaceException Conflict(string message) => new("OriginalApplyConflict", message);
    private static void Check(bool ok, string operation)
    {
        if (!ok) { int error = Marshal.GetLastWin32Error(); throw new WorkspaceException("OriginalApplyNativeFailure", operation + ": " + new Win32Exception(error).Message, new { nativeError = error }); }
    }
    private static Info Information(SafeFileHandle handle)
    { Check(GetFileInformationByHandle(handle, out var info), "Inspect original handle"); return info; }
    private static string Id(SafeFileHandle h) { var i = Information(h); return $"{i.Volume:X8}:{i.IndexHigh:X8}{i.IndexLow:X8}"; }
    private static long Length(Info i) => ((long)i.SizeHigh << 32) | i.SizeLow;
    private static long Time(NativeTime t) => ((long)t.High << 32) | t.Low;
    private const uint DeleteAccess = 0x10000, ReadControl = 0x20000, ReadAttributes = 0x80, Synchronize = 0x100000;
    private const uint UnsupportedAttributes = 0x400 | 0x800 | 0x4000 | 0x200;

    private sealed class Guard : IDisposable
    {
        private readonly Dictionary<string, SafeFileHandle> handles = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> deleteDirectories;
        private readonly string project;
        public SafeFileHandle Root { get; }
        public Guard(string project, string[] deleteDirectories, bool allowPlasticAdministration)
        {
            if (!OperatingSystem.IsWindows()) throw new WorkspaceException("OriginalApplyUnsupported", "Conditional publication requires Windows NTFS.");
            this.project = Path.GetFullPath(project);
            this.deleteDirectories = deleteDirectories.Select(Full).ToHashSet(StringComparer.OrdinalIgnoreCase);
            try
            {
                // Pin each existing ancestor, including the volume root.
                var paths = new Stack<string>();
                for (string? p = this.project; p is not null; p = Path.GetDirectoryName(p)) paths.Push(p);
                while (paths.TryPop(out var p)) PinDirectory(p);
                Root = handles[this.project];
                var fs = new StringBuilder(32);
                Check(GetVolumeInformationByHandleW(Root, null, 0, out _, out _, out _, fs, fs.Capacity), "Inspect original filesystem");
                if (fs.ToString() != "NTFS") throw new WorkspaceException("OriginalApplyUnsupported", "The fixed original must stay on NTFS.");
                for (string? p = this.project; p is not null; p = Path.GetDirectoryName(p))
                    foreach (string vcs in new[] { ".git", ".plastic", ".hg", ".svn" })
                        if (Present(Path.Combine(p, vcs)) && !(allowPlasticAdministration && vcs == ".plastic" && p == this.project))
                            throw new WorkspaceException("VcsMetadataUnsupported", "VCS administration requires its explicit compatibility boundary.");
            }
            catch { Dispose(); throw; }
        }
        private string Full(string relative)
        {
            if (string.IsNullOrEmpty(relative) || relative.Split('/').Any(p => p.Length == 0 || p is "." or ".." ||
                new[] { ".git", ".plastic", ".hg", ".svn" }.Contains(p, StringComparer.OrdinalIgnoreCase) ||
                p.EndsWith(' ') || p.EndsWith('.') || p.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || p.Length > 180 ||
                p.Split('.')[0].ToUpperInvariant() is "CON" or "PRN" or "AUX" or "NUL" or "COM1" or "COM2" or "COM3" or "COM4" or "COM5" or "COM6" or "COM7" or "COM8" or "COM9" or "LPT1" or "LPT2" or "LPT3" or "LPT4" or "LPT5" or "LPT6" or "LPT7" or "LPT8" or "LPT9"))
                throw new WorkspaceException("OriginalApplyUnsupported", "Preserve unsupported publication path names.");
            string path = Path.GetFullPath(Path.Combine(project, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!path.StartsWith(project + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw Conflict("A publication path escaped the fixed root.");
            return path;
        }
        private static bool Present(string path)
        {
            try { _ = File.GetAttributes(path); return true; }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException) { return false; }
        }
        private SafeFileHandle? Open(string path, bool mutation, bool stage = false)
        {
            if (handles.TryGetValue(path, out var existing)) return existing;
            // Desired delete access makes an existing writer's share mask part
            // of admission. No sharing for writes/deletes is granted in return.
            uint access = (Present(path) && (File.GetAttributes(path) & FileAttributes.Directory) != 0 ? 0x20u : 1u) |
                ReadAttributes | ReadControl | (mutation ? DeleteAccess : 0) | (stage ? 0x2u | 0x100u | 0x40000u | 0x80000u : 0);
            var handle = CreateFileW(path, access, 1, IntPtr.Zero, 3, 0x02000000 | 0x00200000, IntPtr.Zero);
            if (handle.IsInvalid)
            {
                int error = Marshal.GetLastWin32Error(); handle.Dispose();
                if (error is 2 or 3) return null;
                throw new WorkspaceException("OriginalApplyNativeFailure", "Cannot pin an original or publication handle: " + new Win32Exception(error).Message, new { nativeError = error });
            }
            try
            {
                var info = Information(handle);
                if ((info.Attributes & UnsupportedAttributes) != 0 || (info.Attributes & 16) == 0 && info.Links != 1)
                    throw new WorkspaceException("OriginalApplyUnsupported", "Reparse, compressed, encrypted, sparse or multiply linked original data must be preserved.");
                if (mutation && (info.Attributes & 1) != 0) throw new WorkspaceException("OriginalApplyUnsupported", "Read-only original entries require separate metadata handling.");
                handles.Add(path, handle); return handle;
            }
            catch { handle.Dispose(); throw; }
        }
        private SafeFileHandle PinDirectory(string path)
        {
            var h = Open(path, deleteDirectories.Contains(path)) ?? throw Conflict("An original ancestor is missing.");
            if ((Information(h).Attributes & 16) == 0) throw Conflict("An original ancestor is no longer a directory.");
            return h;
        }
        private bool Parents(string path)
        {
            var paths = new Stack<string>();
            for (string? p = Path.GetDirectoryName(path); p is not null && p.Length > project.Length; p = Path.GetDirectoryName(p)) paths.Push(p);
            while (paths.TryPop(out var p))
            {
                if (!Present(p)) return false;
                PinDirectory(p);
            }
            return true;
        }
        public OriginalFileObservation? Observe(string relative, CancellationToken ct, bool mutation = false)
        {
            string path = Full(relative);
            if (!Parents(path)) return null;
            var handle = Open(path, mutation, stage: mutation && Path.GetFileName(path).StartsWith(".splinter-apply-", StringComparison.OrdinalIgnoreCase) && path.EndsWith(".new", StringComparison.Ordinal));
            if (handle is null) return null;
            var info = Information(handle); bool directory = (info.Attributes & 16) != 0;
            if ((info.Attributes & UnsupportedAttributes) != 0 || !directory && info.Links != 1 || mutation && (info.Attributes & 1) != 0)
                throw new WorkspaceException("OriginalApplyUnsupported", "A pinned entry gained unsupported metadata or links.");
            RequireSingleStream(path, directory);
            string? hash = directory ? null : Hash(handle, Length(info), ct);
            return new(Id(handle), new(directory ? "directory" : "file", directory ? 0 : Length(info), hash,
                (int)info.Attributes, DateTime.FromFileTimeUtc(Time(info.Write)).Ticks));
        }
        public SafeFileHandle Create(string relative, bool directory)
        {
            string path = Full(relative);
            if (!Parents(path)) throw Conflict("The staged parent is not available.");
            var parent = PinDirectory(Path.GetDirectoryName(path)!);
            string leaf = Path.GetFileName(path);
            IntPtr text = Marshal.StringToHGlobalUni(leaf), unicode = IntPtr.Zero;
            try
            {
                var name = new UnicodeString { Length = checked((ushort)(leaf.Length * 2)), MaximumLength = checked((ushort)(leaf.Length * 2)), Buffer = text };
                unicode = Marshal.AllocHGlobal(Marshal.SizeOf<UnicodeString>()); Marshal.StructureToPtr(name, unicode, false);
                var attributes = new ObjectAttributes { Length = Marshal.SizeOf<ObjectAttributes>(), RootDirectory = parent.DangerousGetHandle(), ObjectName = unicode, Attributes = 0x40 };
                uint access = ReadAttributes | ReadControl | DeleteAccess | Synchronize | 0x100 | 0x40000 | 0x80000 | (directory ? 0u : 3u);
                int status = NtCreateFile(out var h, access, ref attributes, out _, IntPtr.Zero, directory ? 16u : 128u, 1, 2,
                    0x20u | 0x00200000u | (directory ? 1u : 0x40u), IntPtr.Zero, 0);
                if (status < 0)
                { h?.Dispose(); throw new WorkspaceException("OriginalApplyNativeFailure", "Create owned publication stage failed.", new { nativeError = RtlNtStatusToDosError(status) }); }
                handles.Add(path, h); return h;
            }
            finally { if (unicode != IntPtr.Zero) Marshal.FreeHGlobal(unicode); Marshal.FreeHGlobal(text); }
        }
        public void Fill(string relative, string identity, OriginalFileVersion version, Func<OriginalFileVersion, Stream> payload, CancellationToken ct)
        {
            string path = Full(relative); var h = Open(path, true, stage: true) ?? throw Conflict("The owned stage disappeared.");
            if (Id(h) != identity) throw Conflict("The stage identity changed.");
            if (version.Kind == "directory") return;
            using var input = payload(version); byte[] bytes = new byte[65536], existing = new byte[65536];
            long length = Length(Information(h)), offset = 0;
            if (length > version.Length) throw Conflict("The incomplete stage has foreign trailing content.");
            // The final length is already known. Reserve that allocation once
            // instead of letting incremental EOF growth over-allocate the stage.
            IntPtr allocation = Marshal.AllocHGlobal(8);
            try { Marshal.WriteInt64(allocation, version.Length); Check(SetFileInformationByHandle(h, 5, allocation, 8), "Reserve the bounded publication payload"); }
            finally { Marshal.FreeHGlobal(allocation); }
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            int count;
            while ((count = input.Read(bytes, 0, bytes.Length)) > 0)
            {
                ct.ThrowIfCancellationRequested(); hash.AppendData(bytes, 0, count);
                int retained = (int)Math.Min(count, Math.Max(0, length - offset));
                if (retained > 0 && (RandomAccess.Read(h, existing.AsSpan(0, retained), offset) != retained || !existing.AsSpan(0, retained).SequenceEqual(bytes.AsSpan(0, retained))))
                    throw Conflict("Preserve the altered incomplete payload stage.");
                RandomAccess.Write(h, bytes.AsSpan(retained, count - retained), offset + retained);
                offset += count;
            }
            if (offset != version.Length || Convert.ToHexStringLower(hash.GetHashAndReset()) != version.Hash) throw Conflict("The independent payload changed.");
            Check(FlushFileBuffers(h), "Flush owned publication payload");
        }
        public void CopySecurity(string original, string stage)
        {
            var source = handles[Full(original)]; var target = handles[Full(stage)];
            _ = GetKernelObjectSecurity(source, 7, null, 0, out uint required);
            if (required is 0 or > 65536) throw new WorkspaceException("OriginalApplyUnsupported", "Preserve an unsupported original security descriptor.");
            byte[] descriptor = new byte[required];
            Check(GetKernelObjectSecurity(source, 7, descriptor, required, out _), "Read original security descriptor");
            // Also preserve the DACL protection flag, so inheritance does not
            // silently change a file's permissions when it is replaced.
            Check(GetSecurityDescriptorControl(descriptor, out ushort control, out _), "Read original security control");
            Check(SetKernelObjectSecurity(target, 7u | ((control & 0x1000) != 0 ? 0x80000000u : 0x20000000u), descriptor), "Preserve original owner, group and DACL");
        }
        public void SetMetadata(string relative, OriginalFileVersion version, string? original)
        {
            var h = handles[Full(relative)]; var info = Information(h);
            var basic = new BasicInfo { Creation = original is null ? Time(info.Creation) : Time(Information(handles[Full(original)]).Creation),
                Write = new DateTime(version.ModifiedUtcTicks, DateTimeKind.Utc).ToFileTimeUtc(), Attributes = (uint)version.Attributes };
            IntPtr buffer = Marshal.AllocHGlobal(Marshal.SizeOf<BasicInfo>());
            try { Marshal.StructureToPtr(basic, buffer, false); Check(SetFileInformationByHandle(h, 0, buffer, (uint)Marshal.SizeOf<BasicInfo>()), "Preserve supported file metadata"); }
            finally { Marshal.FreeHGlobal(buffer); }
        }
        public void Rename(string from, string to, string identity)
        {
            string source = Full(from), target = Full(to); var h = handles[source];
            if (Id(h) != identity || !Parents(target)) throw Conflict("A publication identity changed.");
            _ = PinDirectory(Path.GetDirectoryName(target)!);
            if (!string.Equals(Path.GetDirectoryName(source), Path.GetDirectoryName(target), StringComparison.OrdinalIgnoreCase))
                throw Conflict("Publication renames must stay in the pinned same directory.");
            byte[] name = Encoding.Unicode.GetBytes(Path.GetFileName(target));
            int rootOffset = IntPtr.Size == 8 ? 8 : 4, lengthOffset = rootOffset + IntPtr.Size, nameOffset = lengthOffset + 4;
            int size = nameOffset + name.Length + 8;
            IntPtr buffer = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.WriteInt32(buffer, 0, 0); // ReplaceIfExists = false
                Marshal.WriteIntPtr(buffer, rootOffset, IntPtr.Zero); Marshal.WriteInt32(buffer, lengthOffset, name.Length);
                Marshal.Copy(name, 0, buffer + nameOffset, name.Length);
                Marshal.WriteInt64(buffer, nameOffset + name.Length, 0);
                int status = NtSetInformationFile(h, out _, buffer, (uint)size, 10);
                if (status < 0) throw new WorkspaceException("OriginalApplyNativeFailure", "Publish without replacing an occupant failed.", new { nativeError = RtlNtStatusToDosError(status) });
                handles.Remove(source); handles.Add(target, h);
            }
            finally { Marshal.FreeHGlobal(buffer); }
        }
        public void Delete(string relative, string identity)
        {
            string path = Full(relative); var h = handles[path];
            if (Id(h) != identity) throw Conflict("A cleanup identity changed.");
            IntPtr buffer = Marshal.AllocHGlobal(4);
            try { Marshal.WriteInt32(buffer, 1); Check(SetFileInformationByHandle(h, 4, buffer, 4), "Delete only the exact owned or planned entry"); }
            finally { Marshal.FreeHGlobal(buffer); }
            handles.Remove(path); h.Dispose();
            try
            {
                if (Present(path)) throw new WorkspaceException("OriginalApplyPending", "The exact deleted entry still has a live name or reader; preserve its receipt.");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { throw new WorkspaceException("OriginalApplyPending", "Deletion absence cannot yet be observed; preserve the exact publication intent.", new { ex.HResult }); }
        }
        private static string Hash(SafeFileHandle handle, long length, CancellationToken ct)
        {
            // Opening FILE_READ_DATA is required for these reads; CreateFile's
            // directory-safe access is augmented for ordinary files below.
            byte[] buffer = new byte[65536]; long offset = 0; using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            while (offset < length)
            { ct.ThrowIfCancellationRequested(); int count = RandomAccess.Read(handle, buffer.AsSpan(0, (int)Math.Min(buffer.Length, length - offset)), offset); if (count == 0) throw Conflict("An original read ended early."); hash.AppendData(buffer, 0, count); offset += count; }
            return Convert.ToHexStringLower(hash.GetHashAndReset());
        }
        private static void RequireSingleStream(string path, bool directory)
        {
            var find = FindFirstStreamW(path, 0, out var data, 0);
            if (find == new IntPtr(-1))
            { if (directory && Marshal.GetLastWin32Error() == 38) return; Check(false, "Inspect original streams"); return; }
            try
            {
                if (directory || data.Name != "::$DATA" || FindNextStreamW(find, out _)) throw new WorkspaceException("OriginalApplyUnsupported", "Alternate data streams require separate preservation.");
                int error = Marshal.GetLastWin32Error(); if (error != 38) Check(false, "Inspect original stream tail");
            }
            finally { _ = FindClose(find); }
        }
        public void Dispose() { foreach (var h in handles.Values) h.Dispose(); handles.Clear(); }
    }

    [StructLayout(LayoutKind.Sequential)] private struct NativeTime { public uint Low, High; }
    [StructLayout(LayoutKind.Sequential)] private struct Info { public uint Attributes; public NativeTime Creation, Access, Write; public uint Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow; }
    [StructLayout(LayoutKind.Sequential)] private struct BasicInfo { public long Creation, Access, Write, Change; public uint Attributes; }
    [StructLayout(LayoutKind.Sequential)] private struct UnicodeString { public ushort Length, MaximumLength; public IntPtr Buffer; }
    [StructLayout(LayoutKind.Sequential)] private struct ObjectAttributes { public int Length; public IntPtr RootDirectory, ObjectName; public uint Attributes; public IntPtr SecurityDescriptor, SecurityQualityOfService; }
    [StructLayout(LayoutKind.Sequential)] private struct IoStatus { public IntPtr Status, Information; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct StreamData { public long Size; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 296)] public string Name; }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern SafeFileHandle CreateFileW(string path, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetFileInformationByHandle(SafeFileHandle h, out Info info);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetVolumeInformationByHandleW(SafeFileHandle h, StringBuilder? volume, int capacity, out uint serial, out uint max, out uint flags, StringBuilder fs, int fsCapacity);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetFileInformationByHandle(SafeFileHandle h, int kind, IntPtr buffer, uint size);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool FlushFileBuffers(SafeFileHandle h);
    [DllImport("ntdll.dll")] private static extern int NtCreateFile(out SafeFileHandle h, uint access, ref ObjectAttributes attributes, out IoStatus status, IntPtr allocation, uint fileAttributes, uint share, uint disposition, uint options, IntPtr ea, uint eaLength);
    [DllImport("ntdll.dll")] private static extern uint RtlNtStatusToDosError(int status);
    [DllImport("ntdll.dll")] private static extern int NtSetInformationFile(SafeFileHandle h, out IoStatus status, IntPtr buffer, uint size, int kind);
    [DllImport("advapi32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetKernelObjectSecurity(SafeFileHandle h, uint information, byte[]? descriptor, uint length, out uint required);
    [DllImport("advapi32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetKernelObjectSecurity(SafeFileHandle h, uint information, byte[] descriptor);
    [DllImport("advapi32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetSecurityDescriptorControl(byte[] descriptor, out ushort control, out uint revision);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr FindFirstStreamW(string path, int level, out StreamData data, uint flags);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool FindNextStreamW(IntPtr h, out StreamData data);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool FindClose(IntPtr h);
}
