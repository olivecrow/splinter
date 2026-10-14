using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Fsp;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.BeeProbe;

internal static class Program
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static Session? activeSession;
    private sealed record Request(string Root, string Basis, string Mode, string Nonce);
    public static int Main(string[] args)
    {
        try
        {
            if (args is ["capabilities"])
            {
                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    scope = "Development-only actual WinFsp Library/Bee NTFS graft; not a product backend",
                    commands = new[] { "serve --input request.json", "selftest --root NEW_DIRECTORY", "inspect-assembly ASSEMBLY_PATH", "capabilities" },
                    source = "Pinned immutable small NTFS fixture and existing range journal; no original Unity project",
                    nativeBee = "Cold empty owned directory; ordinary NTFS buffered durability; outputs preserved",
                    atomicRenameAcrossGraft = false,
                    actualBeeCompilation = "Unsupported: ERROR_NOT_SAME_DEVICE; no copy fallback",
                    limits = "No security sandbox; no arbitrary native-to-native rename boundary; no foreign data-mapping absence proof; no Editor lifecycle"
                }, Json)); return 0;
            }
            if (args is ["inspect-assembly", var assembly]) { InspectAssembly(assembly); return 0; }
            if (args is ["selftest", "--root", var testRoot]) { SelfTest(AllowedPath(testRoot)); return 0; }
            if (args is not ["serve", "--input", var input]) throw new ArgumentException("Use capabilities for the development-only command schema.");
            var request = JsonSerializer.Deserialize<Request>(File.ReadAllText(input), Json) ?? throw new ArgumentException("Missing request.");
            return Serve(new Session(request));
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            if (activeSession is not { Mounted: true } session) return 1;
            session.Report(ex);
            return Serve(session);
        }
    }
    private static int Serve(Session session)
    {
        for (;;)
        {
            try
            {
                session.PublishReady();
                string stop = Path.Combine(session.Root, "stop.json");
                if (File.Exists(stop))
                {
                    var data = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(stop));
                    if (data.GetProperty("nonce").GetString() != session.Nonce) throw new IOException("Stop nonce mismatch; preserve mount.");
                    session.Complete(); return session.HadErrors ? 1 : 0;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                // Post-unmount proof/publication failures return failure; never
                // try to Resume a disposed store. Durable prepared evidence stays.
                if (!session.Mounted) return 1;
                session.Resume();
                try { Write(Path.Combine(session.Root, "stop-blocked.json"), new { nonce = session.Nonce, error = ex.Message, type = ex.GetType().FullName, observedAt = DateTimeOffset.UtcNow }); }
                catch (Exception publication) { Console.Error.WriteLine(publication.Message); }
            }
            Thread.Sleep(100);
        }
    }
    private static string AllowedPath(string path)
    {
        string root = Path.GetFullPath(Environment.CurrentDirectory), full = Path.GetFullPath(path);
        if (!new[] { ".local", "artifacts" }.Any(name => full.StartsWith(Path.Combine(root, name) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Probe paths must be under this repository's .local or artifacts directory.");
        return full;
    }
    internal static void Write(string path, object value)
    {
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        { JsonSerializer.Serialize(output, value, Json); output.Flush(true); }
        File.Move(temporary, path, true);
    }
    private static void InspectAssembly(string path)
    {
        using var stream = File.OpenRead(path); using var reader = new PEReader(stream); var metadata = reader.GetMetadataReader();
        foreach (var handle in metadata.TypeDefinitions)
        {
            var type = metadata.GetTypeDefinition(handle);
            if (metadata.GetString(type.Name) != "RuntimeSentinel") continue;
            foreach (var fieldHandle in type.GetFields())
            {
                var field = metadata.GetFieldDefinition(fieldHandle);
                if (metadata.GetString(field.Name) != "Value" || field.GetDefaultValue().IsNil) continue;
                var value = metadata.GetConstant(field.GetDefaultValue());
                if (value.TypeCode != ConstantTypeCode.Int32) throw new IOException("Sentinel constant must be Int32.");
                int number = metadata.GetBlobReader(value.Value).ReadInt32();
                Console.WriteLine(JsonSerializer.Serialize(new { type = "RuntimeSentinel", field = "Value", value = number, loadedExecutableImage = false }, Json)); return;
            }
        }
        throw new IOException("RuntimeSentinel.Value constant not found.");
    }

    private sealed class Session
    {
        private readonly Request request;
        private readonly PinnedBasis basis;
        private readonly OverlayIdentity identity;
        private readonly JournalDeltaStore store;
        private readonly NativePin rootPin;
        private readonly NativePin? target;
        private readonly BeeFileSystem fs;
        private readonly FileSystemHost host;
        private readonly ConcurrentQueue<string> errors = new();
        private bool ready;
        public string Root { get; }
        public string View => Path.Combine(Root, "view");
        public string NativeBee => Path.Combine(Root, "native-bee");
        public bool HadErrors => !errors.IsEmpty;
        public bool Mounted { get; private set; }
        public string Nonce => request.Nonce;
        public void Report(Exception error) => errors.Enqueue(error.ToString());
        public Session(Request request)
        {
            this.request = request;
            if (request.Mode is not "overlay" and not "native-bee" || !Guid.TryParseExact(request.Nonce, "N", out _)) throw new ArgumentException("Require mode overlay/native-bee and a new 32-digit nonce.");
            Root = AllowedPath(request.Root); string input = AllowedPath(request.Basis);
            if (Root.Equals(input, StringComparison.OrdinalIgnoreCase) || Root.StartsWith(input + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || input.StartsWith(Root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Basis and owned output must be separate trees.");
            if (Path.Exists(Root)) throw new IOException("Preserve existing probe output; choose a new root.");
            Directory.CreateDirectory(Root); rootPin = new NativePin(Root);
            basis = new PinnedBasis(input, request.Nonce);
            identity = new(2, "bee-probe-" + request.Nonce, basis.Snapshot, 1L << 30, 65536);
            store = new JournalDeltaStore(Path.Combine(Root, "delta"), identity, basis);
            if (request.Mode == "native-bee") { Directory.CreateDirectory(NativeBee); target = new NativePin(NativeBee); }
            fs = new BeeFileSystem(store, target, ex => errors.Enqueue(ex.ToString())); host = new FileSystemHost(fs);
            int status = host.MountEx(View, 8, null, false, 0);
            if (status < 0) { host.Dispose(); store.Dispose(); target?.Dispose(); basis.Dispose(); rootPin.Dispose(); throw new IOException("MountEx: 0x" + status.ToString("x8")); }
            Mounted = true; activeSession = this;
        }
        public void PublishReady()
        {
            if (ready) return;
            Write(Path.Combine(Root, "ready.json"), new
            {
                request.Nonce, request.Mode, root = Root, view = View, nativeBee = target is null ? null : NativeBee,
                processId = Environment.ProcessId, processStartTimeUtcTicks = Process.GetCurrentProcess().StartTime.ToUniversalTime().Ticks,
                nativeTargetIdentity = target?.Evidence, sourceFiles = basis.Verify(),
                scope = "Small pinned fixture; Bee native outputs are outside range ACK/result semantics; no source payload copied"
            }); ready = true;
        }
        public void Resume() => fs.Resume();
        public void Complete()
        {
            fs.BeginStop(); rootPin.Verify(); target?.Verify();
            using var proof = target is null ? null : new NativeOutputProof(NativeBee);
            var source = basis.Verify(); store.Flush(); var before = store.Seal(); var storage = store.Observe();
            var changes = store.ObserveChanges(["Library", "Temp", "Logs", "obj"]);
            string resultRoot = Path.Combine(Root, "source-result");
            Directory.CreateDirectory(resultRoot); var results = new List<object>();
            foreach (var change in changes.Changes)
            {
                var entry = store.Stat(change.Path);
                if (entry is null || entry.Directory) { results.Add(new { path = change.Path, deleted = entry is null, directory = entry?.Directory }); continue; }
                if (entry.Length > 16 * 1024 * 1024) throw new IOException("Source result exceeds the bounded fixture allowance.");
                string path = Path.Combine(resultRoot, change.Path.Replace('/', Path.DirectorySeparatorChar)); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                byte[] bytes = new byte[checked((int)entry.Length)]; int offset = 0;
                while (offset < bytes.Length) { int count = store.Read(entry.Id, offset, bytes.AsSpan(offset)); if (count == 0) throw new EndOfStreamException(); offset += count; }
                // Identical retries may republish only their own bounded result.
                using (var output = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None)) { output.Write(bytes); output.Flush(true); }
                string hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
                if (Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))) != hash) throw new IOException("Independent result verification failed.");
                results.Add(new { path = change.Path, length = bytes.Length, sha256 = hash });
            }
            // Publish complete bounded results before releasing the provider. A
            // publication failure at this point leaves it mounted for retry.
            Write(Path.Combine(Root, "prepared-result.json"), new { request.Nonce, seal = before, independentSourceResults = results });
            host.Unmount();
            Mounted = false;
            if (fs.OpenHandles != 0 || fs.RetainedFileContexts != 0) throw new IOException("Unmount did not drain the owned overlay.");
            host.Dispose(); store.Dispose();
            using (var reopened = JournalDeltaStore.OpenReadOnly(Path.Combine(Root, "delta"), identity, basis))
                if (reopened.Seal() != before) throw new IOException("Independent stopped journal seal differs.");
            Write(Path.Combine(Root, "result.json"), new
            {
                request.Nonce, request.Mode, sourceUnchanged = true, sourceFiles = source, independentSourceResults = results,
                nativeBeeFiles = proof?.Files, nativeOutputsPreserved = target is not null,
                seal = before, storage, openHandles = fs.OpenHandles, retainedFileContexts = fs.RetainedFileContexts,
                normalUnmount = true, passed = !HadErrors, errors = errors.ToArray(),
                nativeBoundary = "Exclusive native file proof rejects open files/live image sections; no forced deletion or foreign data-map absence claim"
            });
            target?.Dispose(); basis.Dispose(); rootPin.Dispose();
        }
    }

    private static void SelfTest(string root)
    {
        if (Path.Exists(root)) throw new IOException("Selftest needs a new root."); Directory.CreateDirectory(root);
        string basis = Path.Combine(root, "basis"); Directory.CreateDirectory(Path.Combine(basis, "Assets"));
        Directory.CreateDirectory(Path.Combine(basis, "Library", "Bee")); Directory.CreateDirectory(Path.Combine(basis, "Library", "Artifacts"));
        File.WriteAllText(Path.Combine(basis, "Assets", "sentinel.txt"), "original");
        File.WriteAllBytes(Path.Combine(basis, "Library", "Artifacts", "base.bin"), [1, 2, 3, 4]);
        // A distinct owned target, not a claim about a second Unity Editor or
        // concurrent product workspace. Keep its actual bytes unchanged.
        string peer = Path.Combine(root, "peer-native", "Library", "Bee"); Directory.CreateDirectory(peer);
        string peerFile = Path.Combine(peer, "sentinel.bin"); File.WriteAllBytes(peerFile, [9, 8, 7, 6]);
        string peerHash = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(peerFile)));
        var rows = new List<object>();
        foreach (string mode in new[] { "overlay", "native-bee" })
        {
            var session = new Session(new(Path.Combine(root, mode), basis, mode, Guid.NewGuid().ToString("N"))); session.PublishReady();
            string bee = Path.Combine(session.View, "Library", "Bee"), original = Path.Combine(session.View, "Assets", "sentinel.txt");
            foreach (string existing in new[] { Path.Combine(session.View, "Library"), bee })
                if (NativeMethods.CreateDirectoryW(existing, IntPtr.Zero) || Marshal.GetLastWin32Error() != 183)
                    throw new IOException("Recursive mkdir requires ERROR_ALREADY_EXISTS for existing parents.");
            foreach (string added in new[] { Path.Combine(bee, "mkdir-regression"), Path.Combine(bee, "mkdir-regression", "nested") })
                if (!NativeMethods.CreateDirectoryW(added, IntPtr.Zero)) throw new IOException("Native recursive mkdir failed: " + Marshal.GetLastWin32Error());
            File.WriteAllText(original, "independent-result");
            File.WriteAllBytes(Path.Combine(bee, "file.bin"), new byte[8192]);
            using (var file = new FileStream(Path.Combine(bee, "file.bin"), FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
            using (var map = MemoryMappedFile.CreateFromFile(file, null, 0, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, true))
            using (var view = map.CreateViewAccessor()) { view.Write(4096, 0x12345678); view.Flush(); file.Flush(true); }
            // File.Move can use COPY_ALLOWED on Windows. This checks ordinary
            // file-operation contents only, not atomic rename qualification.
            File.Move(Path.Combine(bee, "file.bin"), Path.Combine(bee, "renamed.bin"));
            byte[] bytes = File.ReadAllBytes(Path.Combine(bee, "renamed.bin"));
            if (BitConverter.ToInt32(bytes, 4096) != 0x12345678 || File.ReadAllText(Path.Combine(basis, "Assets", "sentinel.txt")) != "original") throw new IOException("Source/mmap proof failed.");
            bool crossRenameRejected = true, imageRejected = true;
            if (mode == "native-bee")
            {
                string atomicSource = Path.Combine(bee, "atomic-rename.tmp"), atomicDestination = Path.Combine(bee, "atomic-rename.state");
                File.WriteAllBytes(atomicSource, [31, 41, 59, 26]);
                NativeMethods.ByHandleInfo atomicIdentity; string atomicHash;
                using (var before = NativeMethods.Open(atomicSource, 0x80000000, 7, false))
                { atomicIdentity = NativeMethods.Info(before); atomicHash = NativeMethods.Hash(before); }
                if (NativeMethods.MoveFileExW(atomicSource, atomicDestination, 0) || Marshal.GetLastWin32Error() != 17)
                    throw new IOException("Expected the diagnosed same-graft atomic rename limitation: ERROR_NOT_SAME_DEVICE.");
                using (var after = NativeMethods.Open(atomicSource, 0x80000000, 7, false))
                {
                    var actual = NativeMethods.Info(after);
                    if (actual.Index != atomicIdentity.Index || actual.VolumeSerialNumber != atomicIdentity.VolumeSerialNumber ||
                        NativeMethods.Hash(after) != atomicHash || File.Exists(atomicDestination))
                        throw new IOException("Rejected atomic rename changed the source identity/content or created a destination.");
                }
                bool namespaceRefused = false;
                try { Directory.Move(Path.Combine(session.View, "Library"), Path.Combine(session.View, "Library-renamed")); } catch (IOException) { namespaceRefused = true; } catch (UnauthorizedAccessException) { namespaceRefused = true; }
                if (!namespaceRefused || !Directory.Exists(bee)) throw new IOException("Native graft ancestor rename was allowed.");
                File.WriteAllText(Path.Combine(bee, "..", "..", "Assets", "normalized-path.txt"), "still-overlay");
                if (File.ReadAllText(Path.Combine(session.View, "Assets", "normalized-path.txt")) != "still-overlay" || Directory.Exists(Path.Combine(session.Root, "Assets")))
                    throw new IOException("Normalized path escaped the owned overlay.");
                crossRenameRejected = !NativeMethods.MoveFileExW(original, Path.Combine(bee, "escaped.txt"), 0);
                if (!crossRenameRejected || !File.Exists(original)) throw new IOException("Cross-boundary rename unexpectedly succeeded.");
                // A small explicit native test payload, never a Library seed or
                // application dependency modification. Do not load managed code.
                string image = Path.Combine(bee, "probe-image.dll"); File.Copy(Path.Combine(Environment.SystemDirectory, "version.dll"), image);
                IntPtr loaded = NativeMethods.LoadLibraryExW(image, IntPtr.Zero, 1);
                if (loaded == IntPtr.Zero) throw new IOException("Cannot map owned test image: " + Marshal.GetLastWin32Error());
                try
                {
                    try { using var live = new NativeOutputProof(session.NativeBee); imageRejected = false; }
                    catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode is 32 or 1224) { }
                    if (!imageRejected) throw new IOException("Live executable image was not rejected.");
                }
                finally { if (!NativeMethods.FreeLibrary(loaded)) throw new IOException("Owned image did not unload."); }
                using (var after = new NativeOutputProof(session.NativeBee)) { }
                bool refused = false; try { Directory.Move(session.NativeBee, session.NativeBee + "-changed"); } catch (IOException) { refused = true; }
                if (!refused) throw new IOException("Pinned target could be replaced.");
            }
            if (!SpinWait.SpinUntil(() => { try { session.Complete(); return true; } catch (IOException) { session.Resume(); return false; } }, TimeSpan.FromSeconds(10)))
                throw new IOException("Owned session did not close; preserve evidence.");
            if (session.HadErrors) throw new IOException("Filesystem callback errors were observed; see result.json.");
            if (Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(peerFile))) != peerHash || Directory.EnumerateFileSystemEntries(peer).Count() != 1)
                throw new IOException("Distinct owned peer target changed.");
            rows.Add(new { mode, mappedWriteVerified = true,
                existingParentMkdirError = 183, nestedMkdirVerified = true,
                atomicRenameAcrossGraftSupported = mode == "native-bee" ? false : (bool?)null,
                atomicRenameError = mode == "native-bee" ? 17 : (int?)null,
                rejectedAtomicRenamePreservesSourceAndDestination = mode == "native-bee" ? true : (bool?)null,
                crossRenameRejected = mode == "native-bee" ? crossRenameRejected : (bool?)null,
                liveImageRejected = mode == "native-bee" ? imageRejected : (bool?)null,
                separatePeerTargetUnchanged = true, peerSha256 = peerHash });
        }
        Write(Path.Combine(root, "selftest-result.json"), new { passed = true, rows, scope = "Real WinFsp/NTFS small fixture; no Editor or throughput timing" });
    }
}
