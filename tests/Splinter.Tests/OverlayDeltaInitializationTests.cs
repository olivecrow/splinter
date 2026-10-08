using System.Security.Cryptography;
using System.Text.Json;
using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class OverlayDeltaInitializationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FreshDeltaNeedsNoSourceOrLibraryPayloadOpen(bool graft)
    {
        using var f = new Fixture(graft);
        // Deny even read opens to every payload. Successful initialization
        // proves the client performs zero payload opens/reads, independently of
        // OS caching or an observer that could miss an I/O path.
        using (f.DenyPayloadAccess()) OverlayDeltaInitialization.Create(f.Request);
        Assert.True(File.Exists(Path.Combine(f.Request.DeltaPath, "identity.json")));
        Assert.False(File.Exists(Path.Combine(f.Request.DeltaPath, "journal-initialization.json")));
        using var provider = new ProviderScope(f.Request);
        using var journal = new JournalDeltaStore(f.Request.DeltaPath, f.Request.Identity, provider.Tree);
        byte[] bytes = new byte[f.Bytes.Length];
        var file = journal.Stat(graft ? "Library/data" : "data")!;
        Assert.Equal(bytes.Length, journal.Read(file.Id, 0, bytes));
        Assert.Equal(f.Bytes, bytes);
        Assert.Equal(0, journal.Observe().DurableSequence);
    }

    [Fact]
    public async Task ClientKeepsDurableDeltaBoundaryBeforeLaunchingProvider()
    {
        using var f = new Fixture(graft: true);
        string requestPath = Path.Combine(f.Directory, "request.json");
        await WindowsProcessWorkspaceProvider.WriteAsync(requestPath, f.Request, TestContext.Current.CancellationToken);
        var boundaries = new List<string>();
        using (f.DenyPayloadAccess())
        {
            await Assert.ThrowsAsync<InjectedStop>(() => WinFspOverlayClient.StartAsync(requestPath,
                Path.Combine(f.Directory, "unused-runners"), TestContext.Current.CancellationToken, boundary =>
                {
                    boundaries.Add(boundary);
                    if (boundary == "delta.after-record") throw new InjectedStop();
                    return Task.CompletedTask;
                }));
        }
        Assert.Equal(new[] { "delta.before-effect", "delta.after-record" }, boundaries);
        Assert.True(File.Exists(Path.Combine(f.Request.DeltaPath, "identity.json")));
        Assert.False(File.Exists(Path.Combine(f.Request.ControlPath, "provider-launch-intent.json")));
        Assert.False(Directory.Exists(Path.Combine(f.Directory, "unused-runners")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PayloadChangesAreStillRejectedByFullyVerifiedProviderReader(bool graft)
    {
        using var f = new Fixture(graft);
        OverlayDeltaInitialization.Create(f.Request);
        string file = graft ? f.LibraryFile! : f.SourceFile;
        var changed = f.Bytes.ToArray(); changed[changed.Length / 2] ^= 0xff;
        File.WriteAllBytes(file, changed);
        var error = Assert.Throws<WorkspaceException>(() =>
        {
            using var provider = new ProviderScope(f.Request);
        });
        Assert.Equal("GeneratedBasisChanged", error.Code);
        Assert.Equal(changed, File.ReadAllBytes(file));
        Assert.True(File.Exists(Path.Combine(f.Request.DeltaPath, "identity.json")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ChangedManifestFailsBeforeDeltaCreation(bool graft)
    {
        using var f = new Fixture(graft);
        string receipt = graft ? f.Request.GeneratedSnapshot!.ReceiptDirectory : f.Request.SnapshotReceiptDirectory;
        string path = Path.Combine(receipt, "manifest.json");
        File.AppendAllText(path, " "); // Still valid JSON, but not its fixed hash.
        string preserved = File.ReadAllText(path);
        Assert.Equal("GeneratedBasisChanged", Assert.Throws<WorkspaceException>(() => OverlayDeltaInitialization.Create(f.Request)).Code);
        Assert.False(Directory.Exists(f.Request.DeltaPath));
        Assert.Equal(preserved, File.ReadAllText(path));
    }

    [Theory]
    [InlineData("snapshot")]
    [InlineData("root")]
    [InlineData("proof")]
    [InlineData("owner")]
    public void WrongSourceAccessOrOwnerCannotInitialize(string changed)
    {
        using var f = new Fixture(graft: false);
        var access = f.Request.ReadAccess;
        var request = changed switch
        {
            "snapshot" => f.Request with { ReadAccess = access with { SnapshotId = "other" } },
            "root" => f.Request with { ReadAccess = access with { RootPath = f.Directory } },
            "proof" => f.Request with { ReadAccess = access with { VerificationId = new string('f', 64) } },
            _ => f.Request with { SnapshotOwnerId = "other" }
        };
        Assert.Throws<WorkspaceException>(() => OverlayDeltaInitialization.Create(request));
        Assert.False(Directory.Exists(f.Request.DeltaPath));
    }

    [Theory]
    [InlineData("snapshot")]
    [InlineData("root")]
    [InlineData("proof")]
    [InlineData("owner")]
    public void WrongGraftAccessOrOwnerCannotInitialize(string changed)
    {
        using var f = new Fixture(graft: true);
        var generated = f.Request.GeneratedSnapshot!;
        var access = generated.ReadAccess;
        var request = f.Request with
        {
            GeneratedSnapshot = changed switch
            {
                "snapshot" => generated with { ReadAccess = access with { SnapshotId = "other" } },
                "root" => generated with { ReadAccess = access with { RootPath = f.Directory } },
                "proof" => generated with { ReadAccess = access with { VerificationId = new string('f', 64) } },
                _ => generated with { OwnerId = "other" }
            }
        };
        Assert.Throws<WorkspaceException>(() => OverlayDeltaInitialization.Create(request));
        Assert.False(Directory.Exists(f.Request.DeltaPath));
    }

    [Theory]
    [InlineData("")]
    [InlineData("nested/Library")]
    [InlineData("/Library")]
    public void InvalidGraftPathIsRejectedBeforeSeeding(string path)
    {
        using var f = new Fixture(graft: true);
        var graft = f.Request.Identity.Graft!;
        var request = f.Request with
        {
            Identity = f.Request.Identity with { Graft = graft with { RelativePath = path, Basis = graft.Basis with { RelativePath = path } } }
        };
        Assert.Equal("GeneratedBasisMismatch", Assert.Throws<WorkspaceException>(() => OverlayDeltaInitialization.Create(request)).Code);
        Assert.False(Directory.Exists(f.Request.DeltaPath));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SourceLibraryEntryCannotBeShadowedDuringInitialization(bool directory)
    {
        using var f = new Fixture(graft: true, trackedLibraryDirectory: directory);
        using (f.DenyPayloadAccess())
            Assert.Equal("GeneratedBasisTracked", Assert.Throws<WorkspaceException>(() => OverlayDeltaInitialization.Create(f.Request)).Code);
        Assert.False(System.IO.Directory.Exists(f.Request.DeltaPath));
        string path = Path.Combine(f.Request.Identity.Snapshot.RootPath, "Library");
        Assert.True(directory ? System.IO.Directory.Exists(path) : File.Exists(path));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnknownProviderOrUnsupportedPathPolicyCannotInitialize(bool policy)
    {
        using var f = new Fixture(graft: false);
        var snapshot = policy ? f.Request.Identity.Snapshot with { PathPolicy = WorkspacePathPolicy.PosixId }
            : f.Request.Identity.Snapshot with { Provider = "unsupported-provider" };
        var request = f.Request with { Identity = f.Request.Identity with { Snapshot = snapshot } };
        Assert.Equal(policy ? "PathPolicyUnsupported" : "SnapshotProviderUnsupported",
            Assert.Throws<WorkspaceException>(() => OverlayDeltaInitialization.Create(request)).Code);
        Assert.False(Directory.Exists(f.Request.DeltaPath));
    }

    [Theory]
    [InlineData("latest.initialize.intent-flushed")]
    [InlineData("latest.initialize.identity")]
    [InlineData("latest.initialize.published")]
    public void SameInitializationIntentResumesWithoutPayloadAccess(string boundary)
    {
        using var f = new Fixture(graft: true);
        using (f.DenyPayloadAccess())
        {
            Assert.Throws<InjectedStop>(() => OverlayDeltaInitialization.Create(f.Request, new StopAt(boundary)));
            string path = Path.Combine(f.Request.DeltaPath, "journal-initialization.json");
            if (!File.Exists(path)) path += ".pending";
            string intent = File.ReadAllText(path);
            var changed = f.Request with { Identity = f.Request.Identity with { Id = "another-task" } };
            Assert.Throws<WorkspaceException>(() => OverlayDeltaInitialization.Create(changed));
            Assert.Equal(intent, File.ReadAllText(path));
            OverlayDeltaInitialization.Create(f.Request);
        }
        using var provider = new ProviderScope(f.Request);
        using var journal = new JournalDeltaStore(f.Request.DeltaPath, f.Request.Identity, provider.Tree);
        Assert.Equal(0, journal.Observe().DurableSequence);
        Assert.NotNull(journal.Stat("Library/data"));
    }

    private sealed class InjectedStop : Exception;
    private sealed class StopAt(string boundary) : IJournalMutationObserver
    {
        public void Observe(JournalMutationBoundary value)
        { if (value.Boundary == boundary) throw new InjectedStop(); }
    }

    private sealed class Fixture : IDisposable
    {
        private readonly TestDirectory directory = new();
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
        internal string Directory => directory.Path;
        internal byte[] Bytes { get; } = Enumerable.Range(0, 131072).Select(i => (byte)(i * 31)).ToArray();
        internal string SourceFile { get; }
        internal string? LibraryFile { get; }
        internal OverlayServeRequest Request { get; }

        internal Fixture(bool graft, bool? trackedLibraryDirectory = null)
        {
            if (!OperatingSystem.IsWindows()) Assert.Skip("Owned NTFS basis initialization contract.");
            var source = CreateBasis("source", trackedLibraryDirectory);
            SourceFile = Path.Combine(source.Snapshot.RootPath, "data");
            Request = new(new(2, "task", source.Snapshot, 1048576, 65536), Path.Combine(Directory, "delta"),
                Path.Combine(Directory, "mount"), Path.Combine(Directory, "control"), "token",
                new(source.Id, source.Snapshot.RootPath, source.ManifestHash), source.ReceiptDirectory, source.OwnerId);
            if (graft)
            {
                var library = CreateBasis("library");
                LibraryFile = Path.Combine(library.Snapshot.RootPath, "data");
                Request = Request with
                {
                    Identity = Request.Identity with { Graft = new("Library", library) },
                    GeneratedSnapshot = new(new(library.Id, library.Snapshot.RootPath, library.ManifestHash), library.ReceiptDirectory, library.OwnerId)
                };
            }
        }

        private GeneratedDirectoryReference CreateBasis(string name, bool? trackedLibraryDirectory = null)
        {
            string receipt = Path.Combine(Directory, name), basis = Path.Combine(receipt, "base");
            System.IO.Directory.CreateDirectory(basis);
            File.WriteAllBytes(Path.Combine(basis, "data"), Bytes);
            if (trackedLibraryDirectory is true) System.IO.Directory.CreateDirectory(Path.Combine(basis, "Library"));
            else if (trackedLibraryDirectory is false) File.WriteAllBytes(Path.Combine(basis, "Library"), [42]);
            var snapshot = new SnapshotReference(name, OwnedDirectorySnapshotTree.ProviderName, basis, basis,
                DateTimeOffset.UnixEpoch, true, true, "stopped-original-owned-directory");
            var manifest = OwnedDirectorySnapshotTree.Capture(snapshot, name);
            string path = Path.Combine(receipt, "manifest.json");
            File.WriteAllText(path, JsonSerializer.Serialize(manifest, Json));
            string hash = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
            File.WriteAllText(Path.Combine(receipt, "proof.json"), JsonSerializer.Serialize(
                new OwnedDirectoryProof(1, snapshot, name, hash, Bytes.Length), Json));
            File.WriteAllBytes(Path.Combine(receipt, "readers.lock"), []);
            return new(name, Directory, "Library", snapshot, receipt, name, hash);
        }

        internal IDisposable DenyPayloadAccess() => new PayloadLocks(LibraryFile is { } library ? [SourceFile, library] : [SourceFile]);
        public void Dispose() => directory.Dispose();
    }

    // Match the server's reader composition, including full payload admission.
    private sealed class ProviderScope : IDisposable
    {
        private readonly List<IDisposable> resources = [];
        internal ISnapshotTree Tree { get; }
        internal ProviderScope(OverlayServeRequest request)
        {
            try
            {
                resources.Add(WorkspaceSnapshotBasis.AcquireReadLease(request.Identity.Snapshot,
                    request.SnapshotReceiptDirectory, request.SnapshotOwnerId));
                var source = WorkspaceSnapshotBasis.OpenTree(request.Identity.Snapshot, request.ReadAccess,
                    request.SnapshotReceiptDirectory, request.SnapshotOwnerId);
                resources.Add(source); Tree = source;
                if (request.Identity.Graft is { } graft)
                {
                    resources.Add(WorkspaceSnapshotBasis.AcquireReadLease(graft.Basis.Snapshot, graft.Basis.ReceiptDirectory, graft.Basis.OwnerId));
                    var generated = WorkspaceSnapshotBasis.OpenTree(graft.Basis.Snapshot, request.GeneratedSnapshot!.ReadAccess,
                        graft.Basis.ReceiptDirectory, graft.Basis.OwnerId);
                    resources.Add(generated); Tree = new GraftedSnapshotTree(source, generated, graft);
                }
            }
            catch { Dispose(); throw; }
        }
        public void Dispose() { for (int i = resources.Count - 1; i >= 0; i--) resources[i].Dispose(); resources.Clear(); }
    }

    private sealed class PayloadLocks : IDisposable
    {
        private readonly List<FileStream> files = [];
        internal PayloadLocks(string[] paths)
        {
            try { foreach (string path in paths) files.Add(new(path, FileMode.Open, FileAccess.Read, FileShare.None)); }
            catch { Dispose(); throw; }
        }
        public void Dispose() { foreach (var file in files) file.Dispose(); files.Clear(); }
    }
}
