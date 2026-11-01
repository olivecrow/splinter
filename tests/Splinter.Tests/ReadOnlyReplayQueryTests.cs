using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using Splinter.Core;
using Splinter.Protocol;

namespace Splinter.Tests;

public sealed class ReadOnlyReplayQueryTests
{
    [Fact]
    public void UnknownReplayScopesRemainUntouchedAndUseRootBoundCrossScopeSafePages()
    {
        using var files = new TestDirectory(); var ct = TestContext.Current.CancellationToken;
        string id = new('a', 24), control = Path.Combine(files.Path, "records", id);
        var w = new WorkspaceRecord { Id = id, RequestKey = "query", ProjectPath = Path.Combine(files.Path, "source"),
            Path = Path.Combine(files.Path, "mount"), SourceReference = "files:" + id, BaseReference = "snapshot",
            SourceKind = "none", ApplicationKind = "none", StorageMode = "project-snapshot",
            Snapshot = new() { Reference = new SnapshotOverlayTests.FakeSnapshotTree().Snapshot } };
        string parent = Path.Combine(control, "read-only-replays"); Directory.CreateDirectory(parent);
        string first = Path.Combine(parent, new string('1', 32)), second = Path.Combine(parent, new string('2', 32));
        Directory.CreateDirectory(first); Directory.CreateDirectory(second);
        string unknown = Path.Combine(first, "foreign.bin"); byte[] bytes = [1, 2, 3, 4]; File.WriteAllBytes(unknown, bytes);
        var one = Read(w, control, files.Path, null, 1, ct);
        Assert.Equal(2, one.GetProperty("count").GetInt64()); Assert.Single(one.GetProperty("entries").EnumerateArray());
        string cursor = one.GetProperty("nextCursor").GetString()!, root = one.GetProperty("root").GetString()!;
        Assert.NotEmpty(cursor);
        var two = Read(w, control, files.Path, cursor, 1, ct);
        Assert.Equal(root, two.GetProperty("root").GetString()); Assert.Single(two.GetProperty("entries").EnumerateArray());
        Assert.True(!two.TryGetProperty("nextCursor", out var end) || end.ValueKind == JsonValueKind.Null);
        Assert.Equal("CursorInvalid", Assert.Throws<WorkspaceException>(() => Read(w, control, files.Path, cursor, 1, ct, "journal-packing")).Code);
        Directory.CreateDirectory(Path.Combine(parent, new string('3', 32)));
        Assert.Equal("CursorInvalid", Assert.Throws<WorkspaceException>(() => Read(w, control, files.Path, cursor, 1, ct)).Code);
        Assert.Equal(bytes, File.ReadAllBytes(unknown)); Assert.True(Directory.Exists(second));
    }

    private static JsonElement Read(WorkspaceRecord w, string control, string home, string? cursor, int size,
        CancellationToken ct, string scope = "read-only-replay")
    {
        var method = typeof(Splinter.Cli.Program).Assembly.GetType("Splinter.Cli.WorkspaceReadCommands", true)!
            .GetMethod("ReadDeferred", BindingFlags.Static | BindingFlags.NonPublic)!;
        try { return JsonSerializer.SerializeToElement(method.Invoke(null, [w, control, home, scope, cursor, size, ct, null]), ApiJson.Options); }
        catch (TargetInvocationException error) when (error.InnerException is not null)
        { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
    }
}
