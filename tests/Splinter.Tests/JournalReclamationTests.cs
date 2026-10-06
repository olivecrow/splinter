using System.Security.Cryptography;
using Splinter.Core;
using Splinter.Workflows.Windows;

namespace Splinter.Tests;

public sealed class JournalReclamationTests
{
    [Theory]
    [InlineData("intent")][InlineData("journal-removed")]
    public void InterruptedReclamationRetriesOnlyItsVerifiedFiles(string boundary)
    {
        using var root = new TestDirectory(); var (directory, receipt, seal) = Fixture(root.Path);
        Assert.Throws<IOException>(() => JournalReclamation.Reclaim(directory, seal, ["identity.json"], receipt,
            reached => { if (reached == boundary) throw new IOException("interrupted"); }));
        Assert.True(File.Exists(receipt));
        JournalReclamation.Reclaim(directory, seal, ["identity.json"], receipt);
        Assert.False(Directory.Exists(directory));
        JournalReclamation.Reclaim(directory, seal, ["identity.json"], receipt);
    }

    [Theory]
    [InlineData("unknown")][InlineData("metadata")]
    public void RetryPreservesNewOrChangedFilesAfterTheJournalWasRemoved(string damage)
    {
        using var root = new TestDirectory(); var (directory, receipt, seal) = Fixture(root.Path);
        Assert.Throws<IOException>(() => JournalReclamation.Reclaim(directory, seal, ["identity.json"], receipt,
            stage => { if (stage == "journal-removed") throw new IOException("interrupted"); }));
        string file = Path.Combine(directory, damage == "unknown" ? "user.txt" : "identity.json");
        File.WriteAllText(file, "preserve this");
        Assert.Throws<WorkspaceException>(() => JournalReclamation.Reclaim(directory, seal, ["identity.json"], receipt));
        Assert.Equal("preserve this", File.ReadAllText(file));
    }

    [Fact]
    public void ActiveWriterAndWrongSealCannotAuthorizeUnlink()
    {
        using var root = new TestDirectory(); var (directory, receipt, seal) = Fixture(root.Path);
        string journal = Path.Combine(directory, "delta.journal");
        using (var writer = new FileStream(journal, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
            Assert.Throws<IOException>(() => JournalReclamation.Reclaim(directory, seal, ["identity.json"], receipt));
        Assert.False(File.Exists(receipt)); Assert.True(File.Exists(journal));
        Assert.Equal("ViewSourceChanged", Assert.Throws<WorkspaceException>(() =>
            JournalReclamation.Reclaim(directory, seal with { JournalHash = new string('0', 64) }, ["identity.json"], receipt)).Code);
        Assert.True(File.Exists(journal)); Assert.False(File.Exists(receipt));
        JournalReclamation.Reclaim(directory, seal, ["identity.json"], receipt);
        Assert.False(Directory.Exists(directory));
    }

    [Fact]
    public void FileAtDirectoryPathIsPreservedInsteadOfReportedAbsent()
    {
        using var root = new TestDirectory(); string path = Path.Combine(root.Path, "cache"); File.WriteAllText(path, "owned-or-unknown");
        Assert.Equal("ViewReleaseUncertain", Assert.Throws<WorkspaceException>(() => JournalReclamation.Reclaim(path,
            new(0, 0, new string('0', 64)), ["identity.json"], Path.Combine(root.Path, "receipt.json"))).Code);
        Assert.Equal("owned-or-unknown", File.ReadAllText(path));
    }

    private static (string Directory, string Receipt, SnapshotDeltaSeal Seal) Fixture(string root)
    {
        string directory = Path.Combine(root, "source"); Directory.CreateDirectory(directory);
        byte[] bytes = "sealed acknowledged bytes"u8.ToArray();
        File.WriteAllBytes(Path.Combine(directory, "delta.journal"), bytes); File.WriteAllText(Path.Combine(directory, "identity.json"), "identity");
        return (directory, Path.Combine(root, "reclamation.json"), new(1, bytes.Length, Convert.ToHexStringLower(SHA256.HashData(bytes))));
    }
}
