using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Platform.Windows;

// Internal journal backing aliases only; source input hard links remain
// unsupported. NTFS aliases preserve old backing identities through rename and
// unlink without duplicating their payload.
public sealed class WindowsJournalFilePins : IJournalFilePins
{
    public IRequestPathGuard? PinMetadataPaths(string directory)
    {
        try { return new WindowsJournalDirectoryGuard(directory, "identity.bin"); }
        catch (WorkspaceException error) when (error.Code == "JournalPathUnpinned") { return null; }
    }
    private static readonly JsonSerializerOptions Json = new()
    { RespectRequiredConstructorParameters = true, RespectNullableAnnotations = true,
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow };
    public void Verify(FileStream input, string expectedIdentity)
    {
        if (Identity(Information(input)) != expectedIdentity) throw Invalid("The opened frozen backing has another file identity.");
    }
    public string IdentifyOpened(FileStream input) => Identity(Information(input));
    public void VerifyPath(FileStream input, string path, string expectedIdentity)
    { Verify(input, expectedIdentity); WindowsOwnedDirectoryLease.RequirePath(input.SafeFileHandle, path); }
    public string Identify(string path)
    {
        using var input = Open(path); var info = Information(input);
        return $"{info.VolumeSerialNumber:x8}:{info.FileIndexHigh:x8}{info.FileIndexLow:x8}";
    }
    public void Pin(string source, string destination, string expectedIdentity)
    {
        WorkspaceStore.EnsurePlainPath(destination);
        if (File.Exists(destination))
        {
            if (Identify(destination) != expectedIdentity) throw Invalid("A capture alias has another backing identity.");
            return;
        }
        using var input = Open(source);
        if (Identity(Information(input)) != expectedIdentity) throw Invalid("The backing changed before its durable capture alias.");
        if (!CreateHardLinkW(NativePath(destination), NativePath(source), 0)) throw Invalid("Cannot preserve a capture backing: " + new Win32Exception(Marshal.GetLastWin32Error()).Message);
        if (Identify(destination) != expectedIdentity) throw Invalid("A published capture alias identifies another file.");
    }
    public int? LinkCount(string path)
    { using var input = Open(path); return checked((int)Information(input).NumberOfLinks); }
    public IDisposable CreateDirectory(string path, out string identity)
    {
        var directory = ManagedRestoreFileAccess.CreateDirectory(path, allowChildMutations: true);
        try
        {
            identity = SnapshotInputChecks.FileIdentity(directory, directory: true);
            return directory;
        }
        catch { directory.Dispose(); throw; }
    }
    public string IdentifyDirectory(string path)
    {
        WorkspaceStore.EnsurePlainPath(path);
        using var directory = CreateFileW(NativePath(path), 0x81u, 7u, IntPtr.Zero, 3, 0x02200000u, IntPtr.Zero);
        if (directory.IsInvalid) throw DeleteFailure("Cannot observe the preserved metadata directory", Marshal.GetLastWin32Error());
        WindowsOwnedDirectoryLease.RequirePath(directory, path);
        return SnapshotInputChecks.FileIdentity(directory, directory: true);
    }
    public IDisposable PinDirectory(string path, string expectedIdentity)
    {
        var directory = ManagedRestoreFileAccess.DirectoryHandle(path, metadataWrite: false, allowChildMutations: true);
        if (SnapshotInputChecks.FileIdentity(directory, directory: true) != expectedIdentity) { directory.Dispose(); throw Invalid("Preserve a replaced metadata directory."); }
        return directory;
    }
    public IDisposable MoveDirectory(string path, string destination, string expectedIdentity, Action? beforeRename = null)
    {
        using var ancestry = new ManagedRestoreFileAccess(path, destination);
        var directory = ManagedRestoreFileAccess.DirectoryHandle(path, moving: true, metadataWrite: false, allowChildMutations: true);
        try
        {
            if (SnapshotInputChecks.FileIdentity(directory, directory: true) != expectedIdentity) throw Invalid("Preserve a replaced metadata directory.");
            // NTFS requires descendant handles closed during a directory
            // rename. This exact moving owner and both parent chains remain
            // pinned while the caller releases/reacquires its child fences.
            beforeRename?.Invoke();
            ManagedRestoreFileAccess.MoveRelative(directory, destination);
            if (SnapshotInputChecks.FileIdentity(directory, directory: true) != expectedIdentity) throw Invalid("The preserved metadata directory changed identity.");
            return directory;
        }
        catch (WorkspaceException failure)
        {
            directory.Dispose();
            throw new WorkspaceException(failure.Code, "Cannot preserve the exact metadata directory: " + failure.Message + " " + JsonSerializer.Serialize(failure.Details), failure.Details);
        }
        catch { directory.Dispose(); throw; }
    }
    public void UnpinDirectory(string path, string expectedIdentity)
    {
        using var directory = ManagedRestoreFileAccess.DirectoryHandle(path, moving: true, metadataWrite: true, allowChildMutations: true);
        if (SnapshotInputChecks.FileIdentity(directory, directory: true) != expectedIdentity) throw Invalid("Preserve a replaced metadata directory.");
        ManagedRestoreFileAccess.DeleteDirectory(directory);
    }
    public long AllocatedBytes(FileStream input) => AllocatedBytes(input.SafeFileHandle);
    internal static long AllocatedBytes(SafeFileHandle input)
    {
        IntPtr information = Marshal.AllocHGlobal(24);
        try
        {
            if (!GetFileInformationByHandleEx(input, 1, information, 24)) throw Invalid("Cannot observe the owned file's allocated byte count.");
            long bytes = Marshal.ReadInt64(information); if (bytes < 0) throw Invalid("The owned file has an invalid allocated byte count."); return bytes;
        }
        finally { Marshal.FreeHGlobal(information); }
    }
    public void Unpin(string path, string expectedIdentity)
    {
        WorkspaceStore.EnsurePlainPath(path);
        using var handle = CreateFileW(NativePath(path), 0x10000u | 0x80u, 1u | 2u | 4u, IntPtr.Zero, 3, 0x00200000u, IntPtr.Zero);
        if (handle.IsInvalid) throw DeleteFailure("Cannot pin the exact owned content alias for deletion", Marshal.GetLastWin32Error());
        if (!GetFileInformationByHandle(handle, out var info) || (info.Attributes & (0x400u | 0x10u)) != 0 || Identity(info) != expectedIdentity)
            throw Invalid("Preserve a replaced or unsupported content alias.");
        // Ignore the readonly attribute for this one owned name. Clearing that
        // attribute would temporarily make every hard-link alias writable.
        IntPtr buffer = Marshal.AllocHGlobal(4);
        try
        {
            Marshal.WriteInt32(buffer, 1 | 16);
            if (!SetFileInformationByHandle(handle, 21, buffer, 4)) throw DeleteFailure("Cannot remove the exact owned immutable alias", Marshal.GetLastWin32Error());
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }
    public bool DetachBeforeTruncate(string path, long length)
    {
        using var input = Open(path);
        if (Information(input).NumberOfLinks <= 1 || input.Length <= length) return false;
        if (length < 0) throw Invalid("Invalid retained capsule prefix.");
        // The temporary file is outside the journal object inventory. An
        // interrupted pre-replacement copy is never adopted as a live object.
        string parent = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(path)!)!)!;
        string pendingDirectory = Path.Combine(parent, "capture-cow");
        WorkspaceStore.EnsurePlainPath(pendingDirectory); Directory.CreateDirectory(pendingDirectory);
        string originalIdentity = Identity(Information(input));
        string key = CopyKey(path, originalIdentity, length);
        string receipt = Path.Combine(pendingDirectory, key + ".json"), pending = Path.Combine(pendingDirectory, key + ".copy");
        if (Path.Exists(receipt) || Path.Exists(pending)) throw Invalid("Recover the exact unfinished retained-prefix copy before writing again.");
        CopyIntent intent;
        using (var output = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
        {
            intent = new(1, path, originalIdentity, length, Identity(Information(output)));
            // The empty file can be preserved as unknown if creation loses its
            // reply; no payload is written until its actual NTFS identity is
            // durably owned. Equal bytes alone never authorize its deletion.
            using (var ownership = new FileStream(receipt, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            { JsonSerializer.Serialize(ownership, intent, Json); ownership.Flush(true); }
            byte[] buffer = new byte[65536]; long remaining = length;
            while (remaining > 0)
            { int n = input.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining)); if (n == 0) throw Invalid("A pinned capsule prefix ended early."); output.Write(buffer, 0, n); remaining -= n; }
            output.Flush(true);
        }
        // MoveFileEx replacement cannot retire an open destination on all
        // supported Windows paths. The alias owns the old bytes independently.
        input.Dispose();
        if (Identify(path) != intent.Identity || Identify(pending) != intent.CopyIdentity) throw Invalid("Preserve a replaced retained-prefix source or copy.");
        File.Move(pending, path, true);
        File.Delete(receipt);
        return true;
    }
    private sealed record CopyIntent(int Version, string Source, string Identity, long Length, string CopyIdentity);
    private static string CopyKey(string source, string identity, long length) =>
        WorkspaceStore.Key(JsonSerializer.Serialize(new { Source = source, Identity = identity, Length = length }, Json));
    // Only a receipt-bound unfinished prefix is eligible for cleanup. Pins are
    // recovered first, before either this cleanup or live writer reclamation.
    public static void RecoverCopies(string control)
    {
        string directory = Path.Combine(control, "capture-cow");
        if (!Directory.Exists(directory)) return;
        WorkspaceStore.EnsurePlainPath(directory);
        var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string receipt in Directory.EnumerateFiles(directory, "*.json"))
        {
            WorkspaceStore.EnsurePlainPath(receipt);
            if (new FileInfo(receipt).Length > 65536) throw Invalid("A retained-prefix receipt exceeds its bound.");
            CopyIntent intent;
            try { intent = JsonSerializer.Deserialize<CopyIntent>(File.ReadAllText(receipt), Json) ?? throw Invalid("Missing retained-prefix intent."); }
            catch (JsonException) { throw Invalid("Preserve an incomplete retained-prefix intent."); }
            string key = CopyKey(intent.Source, intent.Identity, intent.Length);
            string prefix = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.Combine(control, "delta"))) + Path.DirectorySeparatorChar;
            if (intent.Version != 1 || string.IsNullOrWhiteSpace(intent.Identity) || string.IsNullOrWhiteSpace(intent.CopyIdentity) || Path.GetFileName(receipt) != key + ".json" || !Path.IsPathFullyQualified(intent.Source) ||
                !Path.GetFullPath(intent.Source).StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || intent.Length < 0)
                throw Invalid("Preserve an unknown retained-prefix copy owner.");
            string pending = Path.Combine(directory, key + ".copy"); known.Add(receipt); known.Add(pending);
            RemoveIncompleteCopy(pending, intent);
        }
        if (Directory.EnumerateFileSystemEntries(directory).Any(p => !known.Contains(p))) throw Invalid("Preserve unknown retained-prefix files.");
        foreach (string receipt in known.Where(p => p.EndsWith(".json", StringComparison.OrdinalIgnoreCase))) File.Delete(receipt);
        if (!Directory.EnumerateFileSystemEntries(directory).Any()) Directory.Delete(directory);
    }
    private static void RemoveIncompleteCopy(string pending, CopyIntent intent)
    {
        if (!File.Exists(pending))
        {
            using var source = Open(intent.Source); string current = Identity(Information(source));
            if (current != intent.Identity && (current != intent.CopyIdentity || source.Length != intent.Length))
                throw Invalid("Preserve a replaced retained-prefix publication.");
            return;
        }
        WorkspaceStore.EnsurePlainPath(pending);
        using (var source = Open(intent.Source))
        using (var copy = Open(pending))
        {
            if (Identity(Information(source)) != intent.Identity || Identity(Information(copy)) != intent.CopyIdentity ||
                copy.Length > intent.Length || copy.Length > source.Length)
                throw Invalid("Preserve a replaced or oversized retained-prefix copy.");
            byte[] expected = new byte[65536], actual = new byte[65536]; long remaining = copy.Length;
            while (remaining > 0)
            {
                int count = (int)Math.Min(expected.Length, remaining); source.ReadExactly(expected.AsSpan(0, count)); copy.ReadExactly(actual.AsSpan(0, count));
                if (!expected.AsSpan(0, count).SequenceEqual(actual.AsSpan(0, count))) throw Invalid("Preserve a changed retained-prefix copy.");
                remaining -= count;
            }
        }
        File.Delete(pending);
    }
    private static FileStream Open(string path)
    {
        WorkspaceStore.EnsurePlainPath(path);
        return new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 65536, FileOptions.SequentialScan);
    }
    // P/Invoke does not receive FileStream's automatic extended-path handling.
    // Explicitly qualify every owned native name, regardless of the host's
    // longPathAware manifest. The normal ancestry and opened-ID checks still
    // decide whether the exact name may be linked or removed.
    private static string NativePath(string path)
    {
        string absolute = Path.GetFullPath(path);
        if (absolute.StartsWith(@"\\?\", StringComparison.Ordinal)) return absolute;
        return absolute.StartsWith(@"\\", StringComparison.Ordinal)
            ? @"\\?\UNC\" + absolute[2..] : @"\\?\" + absolute;
    }
    private static string Identity(FileInformation info) => $"{info.VolumeSerialNumber:x8}:{info.FileIndexHigh:x8}{info.FileIndexLow:x8}";
    private static FileInformation Information(FileStream input)
    {
        if (!GetFileInformationByHandle(input.SafeFileHandle, out var info) || (info.Attributes & (0x400 | 0x10)) != 0)
            throw Invalid("An ordinary pinned journal file identity is required.");
        return info;
    }
    private static WorkspaceException Invalid(string message) => new("SourceCaptureBackingInvalid", message);
    private static WorkspaceException DeleteFailure(string operation, int error) => error is 32 or 33
        ? new("ImmutableContentReaderBusy", operation + "; retain the owned reclaim root until its reader closes.", new { nativeError = error })
        : Invalid(operation + ": " + new Win32Exception(error).Message);
    [StructLayout(LayoutKind.Sequential)]
    private struct FileInformation
    {
        public uint Attributes, CreationLow, CreationHigh, AccessLow, AccessHigh, WriteLow, WriteHigh;
        public uint VolumeSerialNumber, FileSizeHigh, FileSizeLow, NumberOfLinks, FileIndexHigh, FileIndexLow;
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(string path, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFileInformationByHandle(SafeFileHandle file, int information, IntPtr buffer, uint length);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLinkW(string destination, string source, nint security);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle file, out FileInformation information);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandleEx(SafeFileHandle file, int information, IntPtr buffer, uint length);
}
