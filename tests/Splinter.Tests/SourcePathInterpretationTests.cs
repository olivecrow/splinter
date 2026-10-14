using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Splinter.Core;

namespace Splinter.Tests;

public sealed partial class SourceFingerprintTests
{
    [Theory]
    [InlineData("ntfs-snapshot")]
    [InlineData("git-snapshot")]
    public async Task AddedPathsKeepExistingConversionProofsWithoutReopeningUnchangedPayload(string storage)
    {
        using var f = await Fixture.Create(1024 * 1024); var ct = TestContext.Current.CancellationToken;
        f.Workspace.StorageMode = storage;
        File.WriteAllText(f.Path(".gitattributes"), "* text=auto splinter-note=\n*.bin -text\n"); f.Tracker.Mark(".gitattributes");
        var before = await f.Provider().InspectAsync(f.Workspace, ct);
        JsonObject prior = ManifestValue(f);
        Directory.CreateDirectory(f.Path("new-paths"));
        long addedBytes = 0;
        for (int i = 0; i < 73; i++)
        {
            string name = $"new-paths/file-{i:D2}.cs", content = $"// new {i}\n";
            File.WriteAllText(f.Path(name), content); f.Tracker.Mark(name);
            addedBytes += Encoding.UTF8.GetByteCount(content);
        }
        SourceState current;
        using (var locked = new FileStream(f.Path("large.bin"), FileMode.Open, FileAccess.Read, FileShare.None))
            current = await f.Provider().InspectAsync(f.Workspace, ct);
        Assert.Equal(addedBytes, current.Inspection!.PayloadBytesHashed);
        Assert.Equal(storage == "git-snapshot" ? 0 : addedBytes, current.Inspection.GitHashInputBytes);
        Assert.Equal(storage == "git-snapshot" ? 0 : 1, current.Inspection.GitHashProcesses);
        Assert.Equal(before.Inspection!.FilesHashed, current.Inspection.FilesReused);
        Assert.Equal(await f.Oracle(), current.Fingerprint);
        JsonObject final = ManifestValue(f);
        Assert.Equal(3, final["version"]!.GetValue<int>());
        Assert.Equal(prior["configuration"]!.GetValue<string>(), final["configuration"]!.GetValue<string>());
        foreach (JsonNode? file in prior["files"]!.AsArray())
        {
            string path = file!["path"]!.GetValue<string>();
            Assert.Equal(file["interpretation"]!.GetValue<string>(), ManifestFile(final, path)["interpretation"]!.GetValue<string>());
            Assert.Equal(file["gitHash"]!.GetValue<string>(), ManifestFile(final, path)["gitHash"]!.GetValue<string>());
        }
        TestContext.Current.TestOutputHelper!.WriteLine(JsonSerializer.Serialize(new
        {
            scenario = "73-added-paths-with-unchanged-1MiB-file-exclusively-held", storage,
            initial = before.Inspection, addedPaths = 73, addedBytes, current = current.Inspection
        }));
    }

    [Fact]
    public async Task NestedAttributeAdditionRehashesOnlyAffectedExistingFilesAndTheNewAttributeFile()
    {
        using var f = await Fixture.Create(); var ct = TestContext.Current.CancellationToken;
        Directory.CreateDirectory(f.Path("nested"));
        foreach (string path in new[] { "nested/a.txt", "outside.txt" })
        { File.WriteAllText(f.Path(path), "line\r\n"); f.Tracker.Mark(path); }
        File.WriteAllText(f.Path(".gitattributes"), "*.txt text eol=lf\n"); f.Tracker.Mark(".gitattributes");
        await f.Provider().InspectAsync(f.Workspace, ct);
        JsonObject before = ManifestValue(f);
        const string added = "*.txt -text\n";
        File.WriteAllText(f.Path("nested/.gitattributes"), added); f.Tracker.Mark("nested/.gitattributes");
        SourceState current;
        using (var locked = new FileStream(f.Path("large.bin"), FileMode.Open, FileAccess.Read, FileShare.None))
            current = await f.Provider().InspectAsync(f.Workspace, ct);
        Assert.Equal(Encoding.UTF8.GetByteCount(added), current.Inspection!.PayloadBytesHashed);
        Assert.Equal(Encoding.UTF8.GetByteCount(added) + 6, current.Inspection.GitHashInputBytes);
        JsonObject after = ManifestValue(f);
        Assert.NotEqual(ManifestFile(before, "nested/a.txt")["gitHash"]!.GetValue<string>(), ManifestFile(after, "nested/a.txt")["gitHash"]!.GetValue<string>());
        Assert.Equal(ManifestFile(before, "outside.txt")["interpretation"]!.GetValue<string>(), ManifestFile(after, "outside.txt")["interpretation"]!.GetValue<string>());
        Assert.Equal(await TestDirectory.Git(f.Workspace.Path, "hash-object", "--path=nested/a.txt", "--", "nested/a.txt"),
            ManifestFile(after, "nested/a.txt")["gitHash"]!.GetValue<string>());
        Assert.Equal(await f.Oracle(), current.Fingerprint);
    }

    [Fact]
    public async Task ExternalInfoAttributesChangeInvalidatesConversionWithoutAJournalContentChange()
    {
        using var f = await Fixture.Create(); var ct = TestContext.Current.CancellationToken;
        File.WriteAllText(f.Path("code.cs"), "return 1;\r\n"); f.Tracker.Mark("code.cs");
        File.WriteAllText(f.Path(".gitattributes"), "*.cs text eol=lf\n"); f.Tracker.Mark(".gitattributes");
        var before = await f.Provider().InspectAsync(f.Workspace, ct);
        string common = Path.GetFullPath(await TestDirectory.Git(f.Workspace.Path, "rev-parse", "--git-common-dir"), f.Workspace.Path);
        Directory.CreateDirectory(Path.Combine(common, "info"));
        File.WriteAllText(Path.Combine(common, "info", "attributes"), "code.cs -text\n");
        SourceState current;
        using (var locked = new FileStream(f.Path("large.bin"), FileMode.Open, FileAccess.Read, FileShare.None))
            current = await f.Provider().InspectAsync(f.Workspace, ct);
        Assert.Equal(0, current.Inspection!.PayloadBytesHashed); Assert.Equal(11, current.Inspection.GitHashInputBytes);
        Assert.Equal(before.Inspection!.Sequence, current.Inspection.Sequence);
        Assert.Contains("code.cs", current.ChangedPaths);
        Assert.Equal(await TestDirectory.Git(f.Workspace.Path, "hash-object", "--path=code.cs", "--", "code.cs"),
            ManifestFile(ManifestValue(f), "code.cs")["gitHash"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("core.autocrlf", "input")]
    [InlineData("filter.splinter-path-test.clean", "cat")]
    public async Task GlobalConfigurationChangesInvalidateEveryConversionProof(string key, string value)
    {
        using var f = await Fixture.Create(); var ct = TestContext.Current.CancellationToken;
        var before = await f.Provider().InspectAsync(f.Workspace, ct);
        long total = ManifestValue(f)["files"]!.AsArray().Sum(file => new FileInfo(f.Path(file!["path"]!.GetValue<string>())).Length);
        await TestDirectory.Git(f.Workspace.Path, "config", key, value);
        var current = await f.Provider().InspectAsync(f.Workspace, ct);
        Assert.Equal(0, current.Inspection!.PayloadBytesHashed);
        Assert.Equal(total, current.Inspection.GitHashInputBytes);
        Assert.Equal(before.Inspection!.FilesHashed, current.Inspection.FilesReused);
        Assert.Equal(before.Fingerprint, current.Fingerprint);
    }

    [Fact]
    public async Task PathScopedConversionReuseStillRejectsNewlyEffectiveFiltersWithoutReplacingTheManifest()
    {
        using var f = await Fixture.Create(); var ct = TestContext.Current.CancellationToken;
        await f.Provider().InspectAsync(f.Workspace, ct);
        byte[] prior = File.ReadAllBytes(f.Tracker.CachePath(f.Workspace));
        await TestDirectory.Git(f.Workspace.Path, "config", "filter.splinter-path-test.clean", "cat");
        File.WriteAllText(f.Path(".gitattributes"), "code.cs filter=splinter-path-test\n"); f.Tracker.Mark(".gitattributes");
        Assert.Equal("SnapshotFilterUnsupported", (await Assert.ThrowsAsync<WorkspaceException>(() => f.Provider().InspectAsync(f.Workspace, ct))).Code);
        Assert.Equal(prior, File.ReadAllBytes(f.Tracker.CachePath(f.Workspace)));
    }

    [Fact]
    public async Task AddedPathDoesNotHideASameSizeSameTimestampChangeInAnExistingFile()
    {
        using var f = await Fixture.Create(); var ct = TestContext.Current.CancellationToken;
        await f.Provider().InspectAsync(f.Workspace, ct);
        var modified = File.GetLastWriteTimeUtc(f.Path("code.cs"));
        File.WriteAllText(f.Path("code.cs"), "return 9;\n"); File.SetLastWriteTimeUtc(f.Path("code.cs"), modified); f.Tracker.Mark("code.cs");
        File.WriteAllText(f.Path("new.cs"), "new\n"); f.Tracker.Mark("new.cs");
        var current = await f.Provider().InspectAsync(f.Workspace, ct);
        Assert.Equal(14, current.Inspection!.PayloadBytesHashed); Assert.Equal(14, current.Inspection.GitHashInputBytes);
        Assert.Contains("code.cs", current.ChangedPaths); Assert.Contains("new.cs", current.ChangedPaths);
        Assert.Equal(await f.Oracle(), current.Fingerprint);
        string revision = await f.Provider().CheckpointAsync(f.Workspace, "preserve existing and added content", ct);
        Assert.Equal("return 9;", await TestDirectory.Git(f.Workspace.Path, "show", revision + ":code.cs"));
        Assert.Equal("new", await TestDirectory.Git(f.Workspace.Path, "show", revision + ":new.cs"));
    }

    [Theory]
    [InlineData("configuration")]
    [InlineData("path-interpretation")]
    public async Task DamagedCurrentInterpretationMetadataRequiresExplicitAuditAndPreservesRejectedBytes(string damage)
    {
        using var f = await Fixture.Create(); var ct = TestContext.Current.CancellationToken;
        await f.Provider().InspectAsync(f.Workspace, ct);
        string path = f.Tracker.CachePath(f.Workspace);
        JsonObject envelope = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        JsonObject value = envelope["value"]!.AsObject();
        if (damage == "configuration") value["configuration"] = "not-a-digest";
        else ManifestFile(value, "code.cs")["interpretation"] = null;
        envelope["sha256"] = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value, new JsonSerializerOptions(JsonSerializerDefaults.Web))));
        string damaged = envelope.ToJsonString(); File.WriteAllText(path, damaged);
        Assert.Equal("SourceCacheRevalidationRequired", (await Assert.ThrowsAsync<WorkspaceException>(() => f.Provider().InspectAsync(f.Workspace, ct))).Code);
        Assert.Equal(damaged, File.ReadAllText(path));
        var audited = await f.Provider(true).InspectAsync(f.Workspace, ct);
        Assert.Equal("audit", audited.Inspection!.Mode); Assert.Equal(0, audited.Inspection.FilesReused);
        Assert.Equal(await f.Oracle(), audited.Fingerprint);
        Assert.Equal(damaged, File.ReadAllText(Directory.GetFiles(f.Directory.Path, "source.json.rejected-*").Single()));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    public async Task UnsupportedSourceManifestVersionsRemainUnchangedEvenDuringAudit(int version)
    {
        using var f = await Fixture.Create(); var ct = TestContext.Current.CancellationToken;
        await f.Provider().InspectAsync(f.Workspace, ct);
        string path = f.Tracker.CachePath(f.Workspace);
        JsonObject envelope = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        envelope["value"]!["version"] = version;
        string unsupported = envelope.ToJsonString(); File.WriteAllText(path, unsupported);
        foreach (bool audit in new[] { false, true })
        {
            Assert.Equal("SourceCacheVersionUnsupported", (await Assert.ThrowsAsync<WorkspaceException>(() => f.Provider(audit).InspectAsync(f.Workspace, ct))).Code);
            Assert.Equal(unsupported, File.ReadAllText(path));
        }
        Assert.Empty(Directory.GetFiles(f.Directory.Path, "source.json.rejected-*"));
    }

    private static JsonObject ManifestValue(Fixture f) =>
        JsonNode.Parse(File.ReadAllText(f.Tracker.CachePath(f.Workspace)))!["value"]!.AsObject();
    private static JsonObject ManifestFile(JsonObject value, string path) =>
        value["files"]!.AsArray().Single(file => file!["path"]!.GetValue<string>() == path)!.AsObject();
}
