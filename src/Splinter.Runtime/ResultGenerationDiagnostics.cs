using System.Diagnostics;
using System.Text.Json;
using Splinter.Core;

namespace Splinter.Runtime;

// Optional attribution only. This log is never a receipt, root, recovery input
// or success gate, and diagnostics failures do not alter the source operation.
public sealed class ResultGenerationDiagnostics
{
    private readonly string path;
    private ResultGenerationDiagnostics(string path) => this.path = path;
    public static ResultGenerationDiagnostics? Open(string control, string id)
    {
        if (Environment.GetEnvironmentVariable("SPLINTER_RESULT_GENERATION_DIAGNOSTICS") != "1") return null;
        if (id.Length != 24 || id.Any(value => !char.IsAsciiHexDigit(value))) return null;
        try
        {
            string directory = Path.Combine(control, "diagnostics"); WorkspaceStore.EnsurePlainPath(directory); Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "generation-" + id + ".jsonl"); WorkspaceStore.EnsurePlainPath(path);
            return new(path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or WorkspaceException) { return null; }
    }
    public IDisposable Measure(string stage) => new Timing(this, stage);
    private void Write(string stage, long started)
    {
        try
        {
            WorkspaceStore.EnsurePlainPath(path);
            byte[] json = JsonSerializer.SerializeToUtf8Bytes(new { stage, seconds = Stopwatch.GetElapsedTime(started).TotalSeconds });
            using var output = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
            output.Write(json); output.WriteByte((byte)'\n');
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or WorkspaceException) { }
    }
    private sealed class Timing(ResultGenerationDiagnostics owner, string stage) : IDisposable
    {
        private readonly long started = Stopwatch.GetTimestamp();
        private bool disposed;
        public void Dispose() { if (disposed) return; disposed = true; owner.Write(stage, started); }
    }
}
