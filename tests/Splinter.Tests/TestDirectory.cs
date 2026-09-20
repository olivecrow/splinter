using System.Diagnostics;

namespace Splinter.Tests;

internal sealed class TestDirectory : IDisposable
{
    private readonly string parent = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "splinter-tests");
    public string Path { get; }

    public TestDirectory()
    {
        Path = System.IO.Path.GetFullPath(System.IO.Path.Combine(parent, Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(Path);
    }

    public void Dispose()
    {
        // Only delete this freshly-created test directory, never a caller-provided path.
        if (Directory.GetParent(Path)?.FullName != System.IO.Path.GetFullPath(parent))
            throw new InvalidOperationException("Unexpected test cleanup root.");
        if (Directory.Exists(Path))
        {
            // Git object files are read-only on Windows. Only touch files in our owned fixture.
            foreach (string entry in Directory.EnumerateFileSystemEntries(Path, "*", SearchOption.AllDirectories))
                if ((File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException("Refusing test cleanup through a reparse point.");
            foreach (string file in Directory.EnumerateFiles(Path, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            Directory.Delete(Path, recursive: true);
        }
    }

    public static async Task<string> Git(string path, params string[] args)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = path, UseShellExecute = false,
            RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true
        };
        foreach (var name in start.Environment.Keys.Where(key => key.StartsWith("GIT_", StringComparison.OrdinalIgnoreCase)).ToArray())
            start.Environment.Remove(name);
        start.Environment["GIT_TERMINAL_PROMPT"] = "0";
        foreach (string arg in args)
            start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            throw;
        }
        string error = await stderr;
        Assert.True(process.ExitCode == 0, $"git {string.Join(' ', args)}: {error}");
        return (await stdout).TrimEnd('\r', '\n');
    }
}
