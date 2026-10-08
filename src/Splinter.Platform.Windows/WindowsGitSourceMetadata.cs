using System.Buffers;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32.SafeHandles;
using Splinter.Core;
using Splinter.SourceControl.Git;

namespace Splinter.Platform.Windows;

// Preparation observes each original file under a no-write/no-delete handle.
// Only its raw Git object hash admits timestamps; the original is not a reader
// fallback and all source handles close before returning the prepared map.
internal static class WindowsGitSourceMetadata
{
    internal static async Task<GitSourceMetadataManifest?> CaptureAsync(string project, GeneratedDirectoryReference basis,
        CancellationToken ct = default, Action<string>? opened = null)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        if (basis.ProjectPath != project || basis.RelativePath != "Library" || basis.SourceMetadataHash is not null)
            throw new WorkspaceException("GitSourceMetadataInvalid", "Capture requires the original Library tuple before its metadata digest.");
        long started = Stopwatch.GetTimestamp();
        var catalog = await GitSourceMetadata.CaptureTreeAsync(project, ct);
        if (catalog is null) return null;
        var entries = new List<GitSourceMetadataFile>();
        long examinedBytes = 0;
        byte[] buffer = ArrayPool<byte>.Shared.Rent(65536);
        using var parents = new DirectoryPins(project);
        try
        {
            foreach (var blob in catalog.Files)
            {
                ct.ThrowIfCancellationRequested();
                string path = Path.Combine(project, blob.Path.Replace('/', Path.DirectorySeparatorChar));
                if (!parents.TryPin(Path.GetDirectoryName(path)!)) continue;
                // GENERIC_READ / FILE_SHARE_READ / OPEN_REPARSE_POINT. Existing
                // writers make this file ineligible instead of supplying an
                // unverified timestamp. No file content is changed or copied.
                using var handle = CreateFileW(Native(path), 0x80000000, 1, 0, 3, 0x00200000, 0);
                if (handle.IsInvalid)
                {
                    int error = Marshal.GetLastWin32Error();
                    if (error is 2 or 3 or 32 or 33) continue;
                    throw Failure(path, error);
                }
                var before = Information(handle, path);
                if ((before.Attributes & (0x10u | 0x400u | 0x800u | 0x4000u | 0x200u)) != 0 || before.Links != 1 ||
                    Length(before) != blob.Length) continue;
                try { SnapshotInputChecks.Verify(path); }
                catch (WorkspaceException ex) when (ex.Code == "SnapshotFileUnsupported") { continue; }
                opened?.Invoke(blob.Path);
                using var hash = IncrementalHash.CreateHash(blob.ObjectId.Length == 40 ? HashAlgorithmName.SHA1 : HashAlgorithmName.SHA256);
                hash.AppendData(Encoding.ASCII.GetBytes("blob " + blob.Length.ToString(CultureInfo.InvariantCulture) + "\0"));
                long offset = 0;
                while (offset < blob.Length)
                {
                    ct.ThrowIfCancellationRequested();
                    int read = RandomAccess.Read(handle, buffer.AsSpan(0, (int)Math.Min(65536, blob.Length - offset)), offset);
                    if (read == 0) throw new WorkspaceException("GitSourceMetadataInvalid", "A fenced source file became short.");
                    hash.AppendData(buffer.AsSpan(0, read)); offset += read; examinedBytes += read;
                }
                var after = Information(handle, path);
                if (!SameFile(before, after) || RandomAccess.GetLength(handle) != blob.Length)
                    throw new WorkspaceException("GitSourceMetadataInvalid", "A fenced source file changed during content proof.");
                if (Convert.ToHexStringLower(hash.GetHashAndReset()) != blob.ObjectId) continue;
                entries.Add(new(blob.Path, blob.ObjectId, blob.Length, Time(before.CreationLow, before.CreationHigh),
                    Time(before.AccessLow, before.AccessHigh), Time(before.WriteLow, before.WriteHigh)));
            }
        }
        finally { ArrayPool<byte>.Shared.Return(buffer); }
        var result = new GitSourceMetadataManifest(1, basis, catalog.Revision, catalog.Tree, [.. entries], examinedBytes,
            catalog.Files.Length, Stopwatch.GetTimestamp() - started, Stopwatch.Frequency, "");
        result = result with { ContentHash = GitSourceMetadata.ComputeContentHash(result) };
        GitSourceMetadata.Validate(result, basis);
        return result;
    }

    private sealed class DirectoryPins : IDisposable
    {
        private readonly Dictionary<string, SafeFileHandle> handles = new(StringComparer.OrdinalIgnoreCase);
        internal DirectoryPins(string root) { try { if (!TryPin(root)) throw Failure(root, 3); } catch { Dispose(); throw; } }
        internal bool TryPin(string directory)
        {
            var pending = new Stack<string>();
            for (string? path = Path.GetFullPath(directory); path is not null && !handles.ContainsKey(path); path = Path.GetDirectoryName(path)) pending.Push(path);
            while (pending.TryPop(out string? path))
            {
                var handle = CreateFileW(Native(path), 0x80, 1, 0, 3, 0x02200000, 0);
                if (handle.IsInvalid)
                {
                    int error = Marshal.GetLastWin32Error(); handle.Dispose();
                    if (error is 2 or 3) return false;
                    throw Failure(path, error);
                }
                try
                {
                    var info = Information(handle, path);
                    if ((info.Attributes & 0x400) != 0 || (info.Attributes & 0x10) == 0)
                        throw new WorkspaceException("UnsafePath", "Source metadata ancestors must remain ordinary directories.");
                    handles.Add(path, handle);
                }
                catch { handle.Dispose(); throw; }
            }
            return true;
        }
        public void Dispose() { foreach (var handle in handles.Values.Reverse()) handle.Dispose(); handles.Clear(); }
    }
    private static bool SameFile(FileInformation left, FileInformation right) =>
        left.VolumeSerial == right.VolumeSerial && left.IndexHigh == right.IndexHigh && left.IndexLow == right.IndexLow &&
        left.SizeHigh == right.SizeHigh && left.SizeLow == right.SizeLow && left.Attributes == right.Attributes && left.Links == right.Links &&
        left.CreationHigh == right.CreationHigh && left.CreationLow == right.CreationLow &&
        left.WriteHigh == right.WriteHigh && left.WriteLow == right.WriteLow;
    private static long Length(FileInformation info) => ((long)info.SizeHigh << 32) | info.SizeLow;
    private static long Time(uint low, uint high) => ((long)high << 32) | low;
    private static string Native(string path) => path.StartsWith(@"\\?\", StringComparison.Ordinal) ? path : @"\\?\" + Path.GetFullPath(path);
    private static FileInformation Information(SafeFileHandle handle, string path) => GetFileInformationByHandle(handle, out var info)
        ? info : throw Failure(path, Marshal.GetLastWin32Error());
    private static WorkspaceException Failure(string path, int error) => new("GitSourceMetadataCaptureFailed",
        "Cannot prove original source metadata: " + new Win32Exception(error).Message, new { path, nativeError = error });
    [StructLayout(LayoutKind.Sequential)]
    private struct FileInformation
    { public uint Attributes, CreationLow, CreationHigh, AccessLow, AccessHigh, WriteLow, WriteHigh, VolumeSerial, SizeHigh, SizeLow, Links, IndexHigh, IndexLow; }
    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(string path, uint access, uint share, nint security, uint disposition, uint flags, nint template);
    [DllImport("kernel32", SetLastError = true)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle file, out FileInformation info);
}
