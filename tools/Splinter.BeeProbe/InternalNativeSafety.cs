using System.ComponentModel;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32.SafeHandles;
using Splinter.Core;

namespace Splinter.BeeProbe;

// Actual mounted namespace evidence for the single-volume experiment. The
// fixture is owned and small; no installed Editor or existing project is used.
internal static class InternalNativeSafety
{
    private const string Inherited = "Library/Bee/inherited-sparse.bin";
    private const long InheritedLength = 64L * 1024 * 1024;
    private const long PatchOffset = 17L * 1024 * 1024 + 123;
    internal sealed record Fixture(string Basis, string PeerFile, string PeerSha256,
        IReadOnlyDictionary<string, string> OriginalHashes);
    internal sealed record ExpectedSource(string Path, long Length, string Sha256);
    internal sealed record Result(bool Passed, object[] Checks, ExpectedSource[] IndependentSourceResults,
        object NativeInventory, string Scope);

    internal static Fixture CreateFixture(string root)
    {
        root = Path.GetFullPath(root);
        if (Path.Exists(root)) throw new IOException("The internal-native safety fixture requires a new directory.");
        string basis = Path.Combine(root, "basis");
        Directory.CreateDirectory(Path.Combine(basis, "Assets"));
        Directory.CreateDirectory(Path.Combine(basis, "Library", "Bee"));
        File.WriteAllText(Path.Combine(basis, "Assets", "original.txt"), "original-source-sentinel", new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(basis, "Library", "Bee", "inherited-state.bin"), "original-bee-state", new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(basis, "Library", "Bee", "inherited-handle-state.bin"), "original-held-bee-state", new UTF8Encoding(false));
        using (var file = new FileStream(Path.Combine(basis, Inherited.Replace('/', Path.DirectorySeparatorChar)),
            FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read))
        {
            if (!DeviceIoControl(file.SafeFileHandle, 0x000900c4, IntPtr.Zero, 0, IntPtr.Zero, 0, out _, IntPtr.Zero))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot create the owned sparse NTFS input.");
            file.SetLength(InheritedLength);
            file.Write([11, 22, 33, 44]);
            file.Position = InheritedLength - 4; file.Write([55, 66, 77, 88]); file.Flush(true);
        }
        string peer = Path.Combine(root, "peer", "Library", "Bee", "sentinel.bin");
        Directory.CreateDirectory(Path.GetDirectoryName(peer)!); File.WriteAllBytes(peer, [9, 8, 7, 6]);
        return new(basis, peer, Hash(peer), HashTree(basis));
    }

    internal static Result RunMounted(string view, Fixture fixture, InternalNativeDeltaStore native,
        Func<(int OpenHandles, int RetainedFileContexts)>? contexts = null, Action? probeStop = null)
    {
        view = Path.GetFullPath(view);
        string bee = Path.Combine(view, "Library", "Bee");
        var checks = new List<object>();
        foreach (bool relative in new[] { false, true })
        foreach (bool replace in new[] { false, true })
            checks.Add(AtomicRename(view, relative, replace));
        checks.Add(ReplacedLogicalIdentities(native));
        checks.Add(RenamedDirectoryIdentities(native));
        checks.Add(DirectoryAndCaseRename(bee));
        checks.Add(FileOperations(bee));
        checks.Add(InheritedRanges(view, native));
        checks.Add(ReplaceInherited(view));
        checks.Add(MappedLifetime(bee, contexts, probeStop));
        checks.Add(ImageLifetime(bee, contexts, probeStop));
        var sourceResults = new List<ExpectedSource>();
        checks.Add(SourceTransfers(view, native, sourceResults));
        checks.Add(DirectoryBoundaryRefusal(view, native));
        native.Flush(); native.VerifyNativeInventory();
        RequireUnchanged(fixture);
        checks.Add(new { check = "original-and-separate-peer", originalFiles = fixture.OriginalHashes.Count,
            peerSha256 = fixture.PeerSha256, unchanged = true });
        return new(true, checks.ToArray(), sourceResults.ToArray(), native.NativeInventory(),
            "Real small WinFsp/NTFS namespace and mapping checks; no Editor, power-loss or transparent cross-boundary directory qualification.");
    }

    internal static void VerifyIndependentResults(string resultRoot, Result result)
    {
        foreach (var expected in result.IndependentSourceResults)
        {
            string path = Path.Combine(resultRoot, expected.Path.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path) || new FileInfo(path).Length != expected.Length || Hash(path) != expected.Sha256)
                throw new IOException("Independent source result differs after unmount: " + expected.Path);
        }
    }

    internal static void RequireUnchanged(Fixture fixture)
    {
        var actual = HashTree(fixture.Basis);
        if (actual.Count != fixture.OriginalHashes.Count || fixture.OriginalHashes.Any(pair => !actual.TryGetValue(pair.Key, out string? hash) || hash != pair.Value))
            throw new IOException("Original safety fixture changed.");
        if (Hash(fixture.PeerFile) != fixture.PeerSha256 || Directory.EnumerateFileSystemEntries(Path.GetDirectoryName(fixture.PeerFile)!).Count() != 1)
            throw new IOException("Separate peer sentinel changed.");
    }

    private static object AtomicRename(string view, bool relative, bool replace)
    {
        string prefix = (relative ? "relative" : "absolute") + (replace ? "-replace" : "-rename");
        string source = Path.Combine(view, "Library", "Bee", prefix + ".tmp");
        string destination = Path.Combine(view, "Library", "Bee", prefix + ".state");
        byte[] expected = Encoding.UTF8.GetBytes("new-" + prefix), old = Encoding.UTF8.GetBytes("old-" + prefix);
        File.WriteAllBytes(source, expected);
        using var sourceHandle = NativeMethods.Open(source, 0x80000000, 7, false);
        var sourceIdentity = NativeMethods.Info(sourceHandle);
        if (replace) File.WriteAllBytes(destination, old);
        string cwd = Environment.CurrentDirectory;
        try
        {
            if (relative) Environment.CurrentDirectory = view;
            Move(relative ? Path.GetRelativePath(view, source) : source,
                relative ? Path.GetRelativePath(view, destination) : destination, replace);
        }
        finally { Environment.CurrentDirectory = cwd; }
        using var published = NativeMethods.Open(destination, 0x80000000, 7, false);
        var publishedIdentity = NativeMethods.Info(published);
        if (File.Exists(source) || !SameIdentity(sourceIdentity, publishedIdentity) ||
            NativeMethods.Hash(sourceHandle) != Hash(expected) || NativeMethods.Hash(published) != Hash(expected))
            throw new IOException("Atomic rename failed to preserve the source identity and bytes: " + prefix);
        return new { check = "MoveFileExW", relative, replace, copyAllowed = false,
            sourceFileIndex = sourceIdentity.Index, publishedFileIndex = publishedIdentity.Index,
            volumeSerial = publishedIdentity.VolumeSerialNumber, destinationClosedBeforeReplace = replace,
            sha256 = Hash(expected) };
    }

    private static object ReplacedLogicalIdentities(InternalNativeDeltaStore native)
    {
        var outcomes = new List<object>();
        foreach (bool inherited in new[] { false, true })
        {
            string destination = inherited ? "Library/Bee/inherited-handle-state.bin" : "Library/Bee/direct-old.state";
            var old = inherited ? native.Stat(destination)! : native.Create(destination, false, 32);
            if (!inherited) native.Write(old.Id, 0, Encoding.UTF8.GetBytes("old-native-state"));
            byte[] before = ReadIdentity(native, old.Id);
            var replacement = native.Create("Library/Bee/direct-new-" + inherited + ".tmp", false, 32);
            byte[] next = Encoding.UTF8.GetBytes("new-logical-identity"); native.Write(replacement.Id, 0, next);
            native.Rename(replacement.Id, destination, true);
            if (native.Stat(destination)?.Id != replacement.Id || !ReadIdentity(native, replacement.Id).AsSpan().SequenceEqual(next) ||
                !ReadIdentity(native, old.Id).AsSpan().SequenceEqual(before) || !native.Get(old.Id).Deleted)
                throw new IOException("Replacement did not preserve the old retained logical identity.");
            native.ReleaseUnlinkedFile(old.Id);
            outcomes.Add(new { inheritedDestination = inherited, oldIdentityPreservedUntilRelease = true, replacementIdentityPreserved = true });
        }
        return new { check = "retained-replaced-logical-identities", outcomes,
            boundary = "Direct delta contract. Standard MoveFileExW rejects an open destination on NTFS and this view; no POSIX replacement claim." };
    }

    private static byte[] ReadIdentity(InternalNativeDeltaStore store, Guid id)
    {
        byte[] bytes = new byte[checked((int)store.Get(id).Length)]; int offset = 0;
        while (offset < bytes.Length)
        { int count = store.Read(id, offset, bytes.AsSpan(offset)); if (count == 0) throw new EndOfStreamException(); offset += count; }
        return bytes;
    }

    private static object RenamedDirectoryIdentities(InternalNativeDeltaStore native)
    {
        var directory = native.Create("Library/Bee/direct-directory", true, 16);
        var child = native.Create("Library/Bee/direct-directory/child.bin", false, 32);
        byte[] expected = [7, 5, 3, 1]; native.Write(child.Id, 0, expected);
        native.Rename(directory.Id, "Library/Bee/direct-directory-renamed", false);
        if (native.Stat("Library/Bee/direct-directory-renamed")?.Id != directory.Id ||
            native.Stat("Library/Bee/direct-directory-renamed/child.bin")?.Id != child.Id ||
            !ReadIdentity(native, child.Id).AsSpan().SequenceEqual(expected))
            throw new IOException("Direct directory rename did not preserve retained descendant identity and bytes.");
        return new { check = "directory-retained-logical-identities", directoryAndDescendantIdentityPreserved = true,
            boundary = "Direct delta contract. Native MoveFileExW directory publication is checked with descendant handles closed, as required by ordinary NTFS semantics." };
    }

    private static object DirectoryAndCaseRename(string bee)
    {
        string source = Path.Combine(bee, "directory-before"), target = Path.Combine(bee, "directory-after");
        Directory.CreateDirectory(Path.Combine(source, "child"));
        string file = Path.Combine(source, "child", "case.bin"); File.WriteAllBytes(file, [1, 3, 5, 7]);
        NativeMethods.ByHandleInfo id, directoryId;
        using (var before = NativeMethods.Open(file, 0x80000000, 7, false)) id = NativeMethods.Info(before);
        using (var directory = NativeMethods.Open(source, 0x81, 7, true)) directoryId = NativeMethods.Info(directory);
        Move(source, target, false);
        string renamed = Path.Combine(target, "child", "CASE.bin");
        using var held = NativeMethods.Open(Path.Combine(target, "child", "case.bin"), 0x80000000, 7, false);
        Move(Path.Combine(target, "child", "case.bin"), renamed, false);
        using var final = NativeMethods.Open(renamed, 0x80000000, 7, false);
        using var finalDirectory = NativeMethods.Open(target, 0x81, 7, true);
        if (!SameIdentity(id, NativeMethods.Info(final)) || !SameIdentity(directoryId, NativeMethods.Info(finalDirectory)) ||
            NativeMethods.Hash(held) != Hash(new byte[] { 1, 3, 5, 7 }) ||
            Path.GetFileName(Directory.EnumerateFiles(Path.GetDirectoryName(renamed)!).Single()) != "CASE.bin")
            throw new IOException("Directory or case-only rename changed file identity/content or casing.");
        return new { check = "directory-and-case-rename", directoryIdentityPreserved = true,
            descendantIdentityPreserved = true, descendantClosedBeforeDirectoryMove = true,
            retainedHandleReadableAfterCaseOnlyRename = true, casePreserved = true };
    }

    private static object FileOperations(string bee)
    {
        string file = Path.Combine(bee, "io-shapes.bin");
        byte[] pattern = Enumerable.Range(0, 3 * 65536 + 17).Select(index => (byte)(index % 251)).ToArray();
        using (var stream = new FileStream(file, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete))
        {
            stream.Write(ReadOnlySpan<byte>.Empty); stream.Write([91]); stream.Write(pattern);
            stream.Position = 4 * 65536; stream.Write([21, 22, 23]); stream.Flush(true);
        }
        byte[] bytes = File.ReadAllBytes(file);
        if (bytes.Length != 4 * 65536 + 3 || bytes[0] != 91 || !bytes.AsSpan(1, pattern.Length).SequenceEqual(pattern) ||
            bytes.AsSpan(1 + pattern.Length, 4 * 65536 - 1 - pattern.Length).IndexOfAnyExcept((byte)0) >= 0 ||
            !bytes.AsSpan(4 * 65536).SequenceEqual(new byte[] { 21, 22, 23 }))
            throw new IOException("Zero, short, multi-chunk or hole write mismatch.");
        using (var stream = new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete))
        { stream.SetLength(13); stream.SetLength(4096); stream.Flush(true); }
        bytes = File.ReadAllBytes(file);
        if (bytes.Length != 4096 || bytes.AsSpan(13).IndexOfAnyExcept((byte)0) >= 0)
            throw new IOException("Truncate/re-extend exposed discarded bytes.");
        DateTime timestamp = new(2020, 2, 3, 4, 5, 6, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(file, timestamp);
        if (File.GetLastWriteTimeUtc(file) != timestamp) throw new IOException("Native-backed logical timestamp changed.");
        return new { check = "write-shapes-and-metadata", zeroShortMultiChunkOffset = true,
            holesAreZero = true, truncateReextendIsZero = true, lastWriteUtc = timestamp };
    }

    private static object InheritedRanges(string view, InternalNativeDeltaStore native)
    {
        string file = Path.Combine(view, Inherited.Replace('/', Path.DirectorySeparatorChar));
        long beforeNative = native.NativePayloadBytes; int beforeFiles = native.NativeFileCount;
        long beforeJournal = native.Journal.GetJournalBytes();
        byte[] patch = Enumerable.Range(0, 4096).Select(index => (byte)(index % 239 + 1)).ToArray();
        using (var stream = new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete))
        {
            if (stream.Length != InheritedLength) throw new IOException("Inherited length differs.");
            byte[] head = new byte[4]; stream.ReadExactly(head);
            if (!head.AsSpan().SequenceEqual(new byte[] { 11, 22, 33, 44 })) throw new IOException("Inherited read differs.");
            stream.Position = PatchOffset; stream.Write(patch); stream.Flush(true);
            stream.Position = PatchOffset; byte[] read = new byte[patch.Length]; stream.ReadExactly(read);
            if (!read.AsSpan().SequenceEqual(patch)) throw new IOException("Inherited small overwrite differs.");
            stream.Position = InheritedLength - 4; stream.ReadExactly(head);
            if (!head.AsSpan().SequenceEqual(new byte[] { 55, 66, 77, 88 })) throw new IOException("Inherited tail differs.");
        }
        long growth = native.Journal.GetJournalBytes() - beforeJournal;
        if (native.NativePayloadBytes != beforeNative || native.NativeFileCount != beforeFiles || growth >= 1024 * 1024 ||
            native.Journal.Observe().InitialPayloadCopiedBytes != 0)
            throw new IOException("Inherited partial write materialized native payload or a full-file journal copy.");
        return new { check = "inherited-no-copy", inheritedBytes = InheritedLength, writtenBytes = patch.Length,
            offset = PatchOffset, nativePayloadGrowth = 0, nativeFileGrowth = 0, journalGrowthBytes = growth,
            initialPayloadCopiedBytes = native.Journal.Observe().InitialPayloadCopiedBytes };
    }

    private static object ReplaceInherited(string view)
    {
        string inherited = Path.Combine(view, "Library", "Bee", "inherited-state.bin");
        string created = Path.Combine(view, "Library", "Bee", "replacement-state.tmp");
        File.WriteAllText(created, "native-replacement-state", new UTF8Encoding(false));
        using var source = NativeMethods.Open(created, 0x80000000, 7, false);
        var identity = NativeMethods.Info(source); Move(created, inherited, true);
        using var current = NativeMethods.Open(inherited, 0x80000000, 7, false);
        if (!SameIdentity(identity, NativeMethods.Info(current)) ||
            NativeMethods.Hash(current) != Hash(Encoding.UTF8.GetBytes("native-replacement-state")))
            throw new IOException("New native output did not safely replace an inherited Bee name.");
        return new { check = "native-replaces-inherited", sourceIdentityPreserved = true, destinationClosedBeforeReplace = true };
    }

    private static object MappedLifetime(string bee, Func<(int OpenHandles, int RetainedFileContexts)>? contexts, Action? probeStop)
    {
        string source = Path.Combine(bee, "mapped.tmp"), destination = Path.Combine(bee, "mapped.state");
        File.WriteAllBytes(source, new byte[8192]);
        int? retained;
        bool? stopRefused = null;
        using (var file = new FileStream(source, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete))
        using (var mapping = MemoryMappedFile.CreateFromFile(file, null, 0, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, true))
        using (var access = mapping.CreateViewAccessor())
        {
            access.Write(4096, 0x12345678); access.Flush(); file.Flush(true); Move(source, destination, false);
            if (access.ReadInt32(4096) != 0x12345678) throw new IOException("Mapped bytes lost after rename.");
            file.Dispose(); retained = contexts?.Invoke().RetainedFileContexts;
            if (retained is <= 0) throw new IOException("Live mapped view lost its retained file context.");
            if (contexts?.Invoke().OpenHandles is > 0)
                throw new IOException("Mapping-stop check requires the original file handle to have closed.");
            stopRefused = RequireLiveMappingRefusal(probeStop, "data mapping");
            access.Write(4100, 0x11223344); access.Flush();
            if (access.ReadInt32(4100) != 0x11223344)
                throw new IOException("Refused provider stop damaged the still-live mapped view.");
        }
        byte[] actual = File.ReadAllBytes(destination);
        if (BitConverter.ToInt32(actual, 4096) != 0x12345678 || BitConverter.ToInt32(actual, 4100) != 0x11223344)
            throw new IOException("Mapped write did not persist after handle/view closure.");
        bool nameVisibleWhileDeletePending;
        using (var deletedHandle = NativeMethods.Open(destination, 0x80000000, 7, false))
        {
            string expected = NativeMethods.Hash(deletedHandle); File.Delete(destination);
            nameVisibleWhileDeletePending = File.Exists(destination);
            if (NativeMethods.Hash(deletedHandle) != expected)
                throw new IOException("Unlinked open identity lost its payload.");
        }
        if (!SpinWait.SpinUntil(() => !File.Exists(destination), TimeSpan.FromSeconds(2)))
            throw new IOException("Deleted name remained after the final handle closed.");
        return new { check = "mapping-and-unlink-lifetime", retainedContextsWhileMapped = retained,
            renamedWhileMapped = true, writeAfterHandleClosed = true, deletedOpenIdentityReadable = true,
            nameVisibleWhileDeletePending, nameAbsentAfterFinalHandleClosed = true, posixUnlinkSemanticsClaimed = false,
            stopWithLiveDataMappingRefused = stopRefused, mappedWriteAfterRefusedStopVerified = stopRefused };
    }

    private static object ImageLifetime(string bee, Func<(int OpenHandles, int RetainedFileContexts)>? contexts, Action? probeStop)
    {
        string image = Path.Combine(bee, "owned-version-image.dll");
        File.Copy(Path.Combine(Environment.SystemDirectory, "version.dll"), image);
        IntPtr loaded = NativeMethods.LoadLibraryExW(image, IntPtr.Zero, 1);
        if (loaded == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot map owned image through the virtual volume.");
        int? retained;
        bool? stopRefused = null;
        try
        {
            retained = contexts?.Invoke().RetainedFileContexts;
            if (retained is <= 0) throw new IOException("Executable image did not retain a virtual file context.");
            stopRefused = RequireLiveMappingRefusal(probeStop, "executable image");
            if (Marshal.ReadInt16(loaded) != 0x5a4d ||
                Marshal.ReadInt32(loaded, Marshal.ReadInt32(loaded, 0x3c)) != 0x00004550)
                throw new IOException("Refused provider stop damaged the loaded executable image.");
        }
        finally { if (!NativeMethods.FreeLibrary(loaded)) throw new Win32Exception(Marshal.GetLastWin32Error(), "Owned image unload failed."); }
        // No filename-specific deletion/reopen is used to make the retained
        // image context disappear. The same stop/drain callback must now succeed.
        bool? stoppedAfterUnload = null;
        if (probeStop is not null)
        {
            probeStop(); stoppedAfterUnload = true;
            if (contexts?.Invoke() is { } after && (after.OpenHandles != 0 || after.RetainedFileContexts != 0))
                throw new IOException("Successful post-image-unload stop did not drain file contexts.");
        }
        if (!File.Exists(image)) throw new IOException("Provider drain deleted the owned executable output.");
        return new { check = "executable-image-lifetime", retainedContextsWhileLoaded = retained,
            stopWithLoadedImageRefused = stopRefused, loadedImageStillReadableAfterRefusal = stopRefused,
            unloadSucceeded = true, normalStopAfterUnload = stoppedAfterUnload, imageOutputPreserved = true,
            foreignMappingAbsenceProven = false };
    }

    private static bool? RequireLiveMappingRefusal(Action? probeStop, string kind)
    {
        if (probeStop is null) return null;
        bool rejected = false;
        try { probeStop(); }
        catch (IOException) { rejected = true; }
        if (!rejected) throw new IOException("Provider stop accepted a still-live " + kind + ".");
        return true;
    }

    private static object SourceTransfers(string view, InternalNativeDeltaStore native, List<ExpectedSource> results)
    {
        string source = Path.Combine(view, "Library", "Bee", "promote.tmp");
        string destination = Path.Combine(view, "Assets", "promoted.txt");
        File.WriteAllText(source, "native-before", new UTF8Encoding(false));
        using (var held = new FileStream(source, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete))
        {
            var id = NativeMethods.Info(held.SafeFileHandle); Move(source, destination, false);
            held.Position = held.Length; held.Write(Encoding.UTF8.GetBytes("-journal-after")); held.Flush(true);
            using var published = NativeMethods.Open(destination, 0x80000000, 7, false);
            if (!SameIdentity(id, NativeMethods.Info(published)) ||
                NativeMethods.Hash(published) != Hash(Encoding.UTF8.GetBytes("native-before-journal-after")))
                throw new IOException("Source promotion lost identity or writes through the existing handle.");
        }
        results.Add(Expected(view, destination));
        string durable = Path.Combine(view, "Assets", "source-roundtrip.txt"), cache = Path.Combine(view, "Library", "Bee", "source-roundtrip.tmp");
        File.WriteAllText(durable, "durable-source", new UTF8Encoding(false));
        int beforeNativeFiles = native.NativeFileCount;
        using (var held = new FileStream(durable, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete))
        {
            var id = NativeMethods.Info(held.SafeFileHandle); Move(durable, cache, false);
            held.Position = held.Length; held.Write(Encoding.UTF8.GetBytes("-while-in-bee")); held.Flush(true);
            Move(cache, durable, false);
            using var current = NativeMethods.Open(durable, 0x80000000, 7, false);
            if (native.NativeFileCount != beforeNativeFiles || !SameIdentity(id, NativeMethods.Info(current)) ||
                NativeMethods.Hash(current) != Hash(Encoding.UTF8.GetBytes("durable-source-while-in-bee")))
                throw new IOException("Source-to-Bee demoted durable bytes or lost identity.");
        }
        results.Add(Expected(view, durable));
        var sourceView = native.Journal.ObserveChanges(["Library", "Temp", "Logs", "obj"]);
        if (results.Any(result => !sourceView.Changes.Any(change => change.Path == result.Path)))
            throw new IOException("Promoted source is missing from journal source changes.");
        return new { check = "source-classification-and-existing-handles", nativeToSourcePromotion = true,
            sourceToBeeRemainsJournal = true, identityPreserved = true, sourceChangesVisible = true,
            expectedResults = results.ToArray() };
    }

    private static object DirectoryBoundaryRefusal(string view, InternalNativeDeltaStore native)
    {
        string directory = Path.Combine(view, "Library", "Bee", "directory-boundary");
        Directory.CreateDirectory(directory); File.WriteAllText(Path.Combine(directory, "private.bin"), "kept");
        var entry = native.Stat("Library/Bee/directory-boundary") ?? throw new IOException("Missing native directory.");
        bool rejected = false;
        try { native.Rename(entry.Id, "Assets/directory-boundary", false); }
        catch (Exception error) when (error is WorkspaceException or UnauthorizedAccessException or NotSupportedException) { rejected = true; }
        if (!rejected || native.Stat("Assets/directory-boundary") is not null ||
            File.ReadAllText(Path.Combine(directory, "private.bin")) != "kept")
            throw new IOException("Unsupported native-containing directory source transfer was not preserved and rejected.");
        return new { check = "directory-source-crossing", supported = false, rejectedBeforePublication = true,
            invocation = "Direct store; does not claim Windows directory transfer support." };
    }

    private static ExpectedSource Expected(string view, string path) => new(
        Path.GetRelativePath(view, path).Replace('\\', '/'), new FileInfo(path).Length, Hash(path));
    private static void Move(string source, string destination, bool replace)
    {
        if (!NativeMethods.MoveFileExW(source, destination, replace ? 1u : 0u))
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"MoveFileExW without COPY_ALLOWED failed: {source} -> {destination}");
    }
    private static bool SameIdentity(NativeMethods.ByHandleInfo a, NativeMethods.ByHandleInfo b)
        => a.VolumeSerialNumber == b.VolumeSerialNumber && a.Index == b.Index;
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static string Hash(string file) { using var stream = File.OpenRead(file); return Convert.ToHexStringLower(SHA256.HashData(stream)); }
    private static Dictionary<string, string> HashTree(string root) => Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
        .ToDictionary(path => Path.GetRelativePath(root, path).Replace('\\', '/'), Hash, StringComparer.OrdinalIgnoreCase);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(SafeFileHandle device, uint code, IntPtr input, uint inputSize,
        IntPtr output, uint outputSize, out uint returned, IntPtr overlapped);
}
