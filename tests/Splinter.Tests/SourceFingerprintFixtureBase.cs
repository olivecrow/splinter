using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Splinter.Core;
using Splinter.Runtime;
using Splinter.SourceControl.Git;

namespace Splinter.Tests;

// Shared fixture code, never shared mutable repositories. Independent behavior
// classes can run concurrently instead of serializing every source test in one
// partial class. Each test still creates and disposes its own Git workspace.
public abstract class SourceFingerprintFixtureBase
{
    protected sealed partial class Tracker(string cache) : ISourceChangeProvider
    {
        public string Epoch = "first-provider";
        public Action<int>? OnObserve;
        private int observations;
        private long sequence;
        private readonly Dictionary<string, long> changes = new(StringComparer.OrdinalIgnoreCase);
        public void Mark(string path) => changes[path] = ++sequence;
        public string CachePath(WorkspaceRecord workspace) => cache;
        public Task<SourceChangeView> ObserveChangesAsync(WorkspaceRecord workspace, CancellationToken ct = default)
        {
            observations++; OnObserve?.Invoke(observations);
            var included = changes.Where(p => !SourcePathPolicy.Managed(p.Key, workspace.ManagedPaths))
                .OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => new SourcePathChange(p.Key, p.Value)).ToArray();
            var view = Paged ? PagedView(workspace, included) : new SourceChangeView(workspace.Id, workspace.Snapshot!.Reference!.Id, Epoch,
                included.Length == 0 ? 0 : included.Max(c => c.Sequence), included);
            return Task.FromResult(ObservedView?.Invoke(view) ?? view);
        }
    }

    protected sealed class Fixture : IDisposable
    {
        internal TestDirectory Directory = new();
        public WorkspaceRecord Workspace = null!;
        public Tracker Tracker = null!;
        public string Path(string file) => System.IO.Path.Combine(Workspace.Path, file);
        public GitWorkspaceProvider Provider(bool audit = false) => new(sourceChanges: Tracker, auditSource: audit);
        public static async Task<Fixture> Create(int largeFileBytes = 8192)
        {
            var f = new Fixture(); string repo = System.IO.Path.Combine(f.Directory.Path, "repo");
            System.IO.Directory.CreateDirectory(repo);
            await TestDirectory.Git(repo, "init", "-b", "development");
            await TestDirectory.Git(repo, "config", "user.name", "Fixture"); await TestDirectory.Git(repo, "config", "user.email", "fixture@example.invalid");
            await TestDirectory.Git(repo, "config", "core.autocrlf", "false");
            File.WriteAllText(System.IO.Path.Combine(repo, "code.cs"), "return 1;\n");
            File.WriteAllText(System.IO.Path.Combine(repo, "code.cs.meta"), "guid: initial");
            File.WriteAllText(System.IO.Path.Combine(repo, ".gitignore"), "Library/\n");
            File.WriteAllBytes(System.IO.Path.Combine(repo, "large.bin"), new byte[largeFileBytes]);
            await TestDirectory.Git(repo, "add", "."); await TestDirectory.Git(repo, "commit", "-m", "fixture");
            f.Workspace = new() { Id = "fingerprint-fixture", RequestKey = "test", ProjectPath = repo,
                Path = System.IO.Path.Combine(f.Directory.Path, "worker"), SourceReference = "splinter/test", BaseReference = "development", ManagedPaths = ["Library"] };
            f.Workspace.BaseRevision = (await new GitWorkspaceProvider().CreateAsync(f.Workspace, TestContext.Current.CancellationToken)).BaseRevision;
            f.Workspace.StorageMode = "git-snapshot";
            f.Workspace.Snapshot = new() { Reference = new("fake-snapshot", "fake", repo, repo, DateTimeOffset.UtcNow, true, true, "fixture-only") };
            f.Tracker = new(System.IO.Path.Combine(f.Directory.Path, "source.json"));
            return f;
        }
        public async Task<string> Oracle()
        {
            string[] files = (await TestDirectory.Git(Workspace.Path, "ls-files", "--cached", "--others", "--exclude-standard", "-z"))
                .Split('\0', StringSplitOptions.RemoveEmptyEntries);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            foreach (string file in files.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
            {
                if (SourcePathPolicy.Managed(file, Workspace.ManagedPaths) || !File.Exists(Path(file))) continue;
                byte[] name = Encoding.UTF8.GetBytes(file);
                hash.AppendData(BitConverter.GetBytes(name.Length)); hash.AppendData(name); hash.AppendData([1]);
                hash.AppendData(SHA256.HashData(File.ReadAllBytes(Path(file))));
            }
            return Convert.ToHexStringLower(hash.GetHashAndReset());
        }
        public void Dispose() => Directory.Dispose();
    }

    protected static void SeedSummaries(Fixture f)
    {
        f.Tracker.InitialSummaries = Directory.GetFiles(f.Workspace.Path).Where(path => Path.GetFileName(path) != ".git")
            .ToDictionary(path => Path.GetFileName(path)!, path =>
            {
                byte[] bytes = File.ReadAllBytes(path);
                return new SnapshotContentSummary(Path.GetFileName(path), bytes.Length,
                    Convert.ToHexStringLower(SHA256.HashData(bytes)), bytes.Contains((byte)'\r'), bytes.Contains((byte)0));
            }, StringComparer.Ordinal)!;
    }

    protected sealed partial class Tracker : ISourceContentSummaryProvider
    {
        public int InitialCalls;
        public Dictionary<string, SnapshotContentSummary>? InitialSummaries;
        public Func<SourceContentSummaryView, SourceContentSummaryView>? TransformInitial;
        public Task<SourceContentSummaryView?> ObserveInitialContentAsync(WorkspaceRecord workspace,
            SourceChangeView expected, string[] paths, CancellationToken ct = default)
        {
            InitialCalls++;
            if (InitialSummaries is null) return Task.FromResult<SourceContentSummaryView?>(null);
            var view = new SourceContentSummaryView(workspace.Snapshot!.Reference!, expected,
                paths.Where(path => InitialSummaries.ContainsKey(path) &&
                    !expected.Changes.Any(change => SourcePathPolicy.Affects(change.Path, path)))
                    .Select(path => InitialSummaries[path]).ToArray());
            return Task.FromResult<SourceContentSummaryView?>(TransformInitial?.Invoke(view) ?? view);
        }
    }

    protected sealed partial class Tracker
    {
        public bool Paged;
        private SourceChangeView PagedView(WorkspaceRecord workspace, SourcePathChange[] included)
        {
            string hash = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(included)));
            const string store = "000000000000000000000000";
            var root = new MetadataTreeRoot(1, store, included.Length == 0 ? null : new(0, 48, 256, included.Length, hash));
            return new(workspace.Id, workspace.Snapshot!.Reference!.Id, Epoch, included.Length == 0 ? 0 : included.Max(change => change.Sequence), [])
            {
                Index = new(1, "fake-contract-only", root, new(1, store, null), [.. workspace.ManagedPaths], included.Length, ContentHash: hash),
                ChangesReader = () => included
            };
        }
    }

    protected sealed partial class Tracker
    {
        public Func<SourceChangeView, SourceChangeView>? ObservedView;
    }
}
