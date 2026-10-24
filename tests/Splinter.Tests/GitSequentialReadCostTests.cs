using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Splinter.Core;
using Splinter.SourceControl.Git;

namespace Splinter.Tests;

public sealed class GitSequentialReadCostTests
{
    [Fact]
    public async Task RecordedProjectSequentialReadCost()
    {
        string? output = Environment.GetEnvironmentVariable("SPLINTER_GIT_SEQUENTIAL_COST");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(output), "Set SPLINTER_GIT_SEQUENTIAL_COST and fixed input paths for opt-in reader evidence.");
        Assert.False(File.Exists(output));
        string source = Environment.GetEnvironmentVariable("SPLINTER_GIT_SEQUENTIAL_SOURCE")!;
        string revision = Environment.GetEnvironmentVariable("SPLINTER_GIT_SEQUENTIAL_REVISION")!;
        string expectedPath = Environment.GetEnvironmentVariable("SPLINTER_GIT_SEQUENTIAL_MANIFEST")!;
        string common = await TestDirectory.Git(source, "rev-parse", "--path-format=absolute", "--git-common-dir");
        string treeId = await TestDirectory.Git(source, "rev-parse", revision + "^{tree}");
        var snapshot = new SnapshotReference("sequential-cost", GitObjectSnapshotProvider.ProviderName, source, common,
            DateTimeOffset.UtcNow, true, true, GitObjectSnapshotProvider.Consistency);
        // Construct the same internal reader in old/new frozen assemblies. This
        // component probe never registers refs or modifies the input repository.
        var constructor = typeof(GitObjectSnapshotTree).GetConstructors(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Single();
        object?[] arguments = constructor.GetParameters().Select(p => p.Name switch
        {
            "snapshot" => (object)snapshot, "tree" => treeId, "revision" => revision, "reference" => "",
            "health" => (Func<SnapshotObservation>)(() => new(snapshot.Id, true, null, 0, null)),
            _ => p.DefaultValue
        }).ToArray();
        using var tree = (GitObjectSnapshotTree)constructor.Invoke(arguments);
        using var expected = JsonDocument.Parse(await File.ReadAllTextAsync(expectedPath, TestContext.Current.CancellationToken));
        byte[] buffer = new byte[65536];
        long bytes = 0, allocation = GC.GetTotalAllocatedBytes();
        var clock = Stopwatch.StartNew();
        foreach (var file in expected.RootElement.EnumerateObject().OrderBy(f => f.Name, StringComparer.Ordinal))
        {
            long length = file.Value.GetProperty("bytes").GetInt64(), position = 0;
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            while (position < length)
            {
                int n = tree.Read(file.Name, position, buffer.AsSpan(0, (int)Math.Min(buffer.Length, length - position)));
                Assert.True(n > 0); hash.AppendData(buffer.AsSpan(0, n)); position += n;
            }
            Assert.Equal(file.Value.GetProperty("sha256").GetString(), Convert.ToHexStringLower(hash.GetHashAndReset()));
            bytes += position;
        }
        clock.Stop();
        var result = new { seconds = clock.Elapsed.TotalSeconds, bytes, tree.DecodedBlobBytes, tree.FullBlobProofs,
            tree.ReaderProcessStarts, tree.PeakReaderProcesses, tree.PeakChunkPayloadBytes, tree.PayloadCacheHits,
            allocatedBytes = GC.GetTotalAllocatedBytes() - allocation,
            assemblySha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(typeof(GitObjectSnapshotTree).Assembly.Location))),
            scope = "Complete fixed real-project content verification via sequential reader; excludes WinFsp, Git index refresh, Library and Editor. Constant health observer; no input repository writes." };
        tree.Dispose(); Assert.Equal(0, tree.ActiveReaderProcesses); Assert.Equal(0, tree.CachedPayloadBytes);
        using var stream = new FileStream(output!, FileMode.CreateNew, FileAccess.Write);
        JsonSerializer.Serialize(stream, result, new JsonSerializerOptions { WriteIndented = true });
        TestContext.Current.TestOutputHelper!.WriteLine(JsonSerializer.Serialize(result));
    }
}
