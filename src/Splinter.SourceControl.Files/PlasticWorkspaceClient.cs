using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Splinter.Core;

namespace Splinter.SourceControl.Files;

// Fixed read-only verbs with ArgumentList; output/error text is never executed
// or persisted. In particular, an exit-zero "Error:" is not an empty inventory.
public sealed class PlasticWorkspaceClient : IPlasticWorkspaceClient, ISourceCompatibilityInspector
{
    private static WorkspaceException Unavailable() => new("PlasticObservationUnavailable", "Plastic local workspace identity could not be verified. Preserve the task; no source fallback is available.");
    public async Task<string> VersionAsync(CancellationToken ct = default)
    {
        string output = (await RunAsync(["version"], ct)).Trim();
        if (!Regex.IsMatch(output, @"\A[0-9]+(?:\.[0-9]+){3}\z")) throw Unavailable();
        return output;
    }
    public async Task<PlasticWorkspaceIdentity> InspectAsync(string path, CancellationToken ct = default)
    {
        string[] fields = (await RunAsync(["getworkspacefrompath", Path.GetFullPath(path), "--format={wkpath}{tab}{guid}{tab}{type}{tab}{dynamic}"], ct)).TrimEnd('\r', '\n').Split('\t');
        if (fields.Length != 4 || fields.Any(f => f.Contains('\r') || f.Contains('\n')) ||
            !Path.IsPathFullyQualified(fields[0]) || !Guid.TryParseExact(fields[1], "D", out _)) throw Unavailable();
        return new(fields[0], fields[1], fields[2], fields[3]);
    }
    public async Task<PlasticWorkspaceLocation[]> ListAsync(CancellationToken ct = default)
    {
        string output = await RunAsync(["workspace", "list", "--format={path}{tab}{wkid}"], ct);
        var result = new List<PlasticWorkspaceLocation>();
        foreach (string line in output.Split('\n').Where(l => l.TrimEnd('\r').Length != 0))
        {
            string[] fields = line.TrimEnd('\r').Split('\t');
            if (fields.Length != 2 || !Path.IsPathFullyQualified(fields[0]) || !Guid.TryParseExact(fields[1], "D", out _)) throw Unavailable();
            result.Add(new(fields[0], fields[1]));
            if (result.Count > 4096) throw Unavailable();
        }
        if (result.Select(r => Path.GetFullPath(r.Path)).Distinct(StringComparer.OrdinalIgnoreCase).Count() != result.Count) throw Unavailable();
        return result.ToArray();
    }
    async Task<SourceCompatibility> ISourceCompatibilityInspector.InspectAsync(string path, CancellationToken ct)
    {
        var identity = await InspectAsync(path, ct); PlasticWorkspaceProvider.RequireIdentity(identity, path);
        return new("plastic", identity.Path, identity.Guid, "regular/static; isolated-file-workers", await VersionAsync(ct));
    }
    private static async Task<string> RunAsync(string[] arguments, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(30));
        using var process = new Process { StartInfo = new("cm") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true } };
        foreach (string argument in arguments) process.StartInfo.ArgumentList.Add(argument);
        try
        {
            if (!process.Start()) throw Unavailable();
            Task<string> output = ReadAsync(process.StandardOutput, timeout.Token);
            Task<string> error = ReadAsync(process.StandardError, timeout.Token);
            await Task.WhenAll(output, error, process.WaitForExitAsync(timeout.Token));
            if (process.ExitCode != 0 || !string.IsNullOrWhiteSpace(await error)) throw Unavailable();
            return await output;
        }
        catch (Exception ex) when (ex is Win32Exception or IOException or OperationCanceledException)
        {
            try { if (!process.HasExited) process.Kill(); } catch (InvalidOperationException) { }
            ct.ThrowIfCancellationRequested(); throw Unavailable();
        }
    }
    private static async Task<string> ReadAsync(StreamReader input, CancellationToken ct)
    {
        var output = new StringBuilder(); char[] buffer = new char[4096];
        int count;
        while ((count = await input.ReadAsync(buffer.AsMemory(), ct)) != 0)
        {
            if (output.Length > 1024 * 1024 - count) throw new IOException("Plastic output exceeds the observation limit.");
            output.Append(buffer, 0, count);
        }
        return output.ToString();
    }
}
