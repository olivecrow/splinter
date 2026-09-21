using System.Security.Cryptography;
using System.Text.Json;
using Splinter.Core;
using Splinter.Platform.Windows;

namespace Splinter.Tests;

public sealed class RefsStorageTests
{
    [Fact]
    public void NtfsCannotSilentlyFallBackToFullCopy()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Windows volume test.");
        using var d = new TestDirectory();
        if (new DriveInfo(Path.GetPathRoot(d.Path)!).DriveFormat == "ReFS") Assert.Skip("Requires a non-ReFS fixture volume.");
        Assert.Equal("CoWUnsupported", Assert.Throws<WorkspaceException>(() => new RefsWorkspaceStorageProvider().Probe(d.Path)).Code);
    }

    [Fact]
    public async Task RefsSharesLargeExtentsAndPrivateWritesLeaveBaselineAndSiblingUnchanged()
    {
        string? configured = Environment.GetEnvironmentVariable("SPLINTER_TEST_REFS_ROOT");
        if (configured is null) Assert.Skip("Set SPLINTER_TEST_REFS_ROOT to run the real ReFS extent-clone test.");
        var ct = TestContext.Current.CancellationToken;
        string root = Path.GetFullPath(Path.Combine(configured!, "splinter-test-" + Guid.NewGuid().ToString("N")));
        var provider = new RefsWorkspaceStorageProvider(); provider.Probe(root);
        string source = Path.Combine(root, "baseline"), a = Path.Combine(root, "a"), b = Path.Combine(root, "b");
        Directory.CreateDirectory(source);
        long freeAfterSource = 0, freeAfterClones = 0;
        CloneStatistics? first = null, second = null;
        try
        {
            string original = Path.Combine(source, "large.bin"); byte[] buffer = RandomNumberGenerator.GetBytes(1024 * 1024);
            await using (var file = new FileStream(original, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                for (int i = 0; i < 512; i++) await file.WriteAsync(buffer, ct);
                await file.WriteAsync(new byte[] { 3, 7, 11 }, ct); file.Flush(true);
            }
            await File.WriteAllBytesAsync(Path.Combine(source, "aligned.bin"), buffer[..8192], ct);
            await File.WriteAllBytesAsync(Path.Combine(source, "empty.bin"), [], ct);
            Directory.CreateDirectory(Path.Combine(source, "empty-directory"));
            freeAfterSource = new DriveInfo(Path.GetPathRoot(root)!).AvailableFreeSpace;
            first = await provider.CloneAsync(source, a, [], ct); second = await provider.CloneAsync(a, b, [], ct);
            freeAfterClones = new DriveInfo(Path.GetPathRoot(root)!).AvailableFreeSpace;
            Assert.Equal(512L * 1024 * 1024 + 4096, first.ClonedBytes); Assert.Equal(4099, first.CopiedBytes);
            Assert.Equal(first.LogicalBytes, second.LogicalBytes);
            Assert.Equal(buffer[..8192], await File.ReadAllBytesAsync(Path.Combine(b, "aligned.bin"), ct));
            Assert.Equal(0, new FileInfo(Path.Combine(b, "empty.bin")).Length);
            Assert.True(Directory.Exists(Path.Combine(b, "empty-directory")));
            Assert.True(freeAfterSource - freeAfterClones < 256L * 1024 * 1024,
                $"Two 512MiB clones allocated {freeAfterSource - freeAfterClones} bytes; inspect volume activity before claiming savings.");
            using (var change = new FileStream(Path.Combine(a, "large.bin"), FileMode.Open, FileAccess.Write, FileShare.None))
            { change.Position = 12345; change.WriteByte((byte)(buffer[12345] ^ 255)); change.Flush(true); }
            using var sourceRead = File.OpenRead(original); using var siblingRead = File.OpenRead(Path.Combine(b, "large.bin"));
            Assert.Equal(SHA256.HashData(sourceRead), SHA256.HashData(siblingRead));
            using var changedRead = File.OpenRead(Path.Combine(a, "large.bin"));
            sourceRead.Position = 0; Assert.NotEqual(Convert.ToHexString(SHA256.HashData(sourceRead)), Convert.ToHexString(SHA256.HashData(changedRead)));
        }
        finally
        {
            // Exact newly generated child only; the configured root is never removed.
            if (Directory.GetParent(root)?.FullName != Path.TrimEndingDirectorySeparator(Path.GetFullPath(configured!)))
                throw new InvalidOperationException("Unsafe test cleanup path.");
            Directory.Delete(root, true);
        }
        string artifactDirectory = Path.Combine(configured!, "evidence"); Directory.CreateDirectory(artifactDirectory);
        await File.WriteAllTextAsync(Path.Combine(artifactDirectory, "refs-clone.json"), JsonSerializer.Serialize(new
        {
            observedAt = DateTimeOffset.UtcNow, first, second, freeAfterSource, freeAfterClones,
            cloneAllocationBytes = freeAfterSource - freeAfterClones,
            freeAfterRelease = new DriveInfo(Path.GetPathRoot(root)!).AvailableFreeSpace,
            isolatedWrites = true, sourceAndSiblingHashesEqual = true, filesReleased = !Directory.Exists(root)
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }), ct);
    }
}
