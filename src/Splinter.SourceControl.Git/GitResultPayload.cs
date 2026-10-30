using System.Diagnostics;
using Splinter.Core;

namespace Splinter.SourceControl.Git;

public static partial class GitSourceResult
{
    public static Stream OpenPayload(string project, string reference, string path)
    {
        if (!reference.StartsWith("refs/splinter/validation-results/", StringComparison.Ordinal) ||
            reference["refs/splinter/validation-results/".Length..].Length != 24 ||
            reference["refs/splinter/validation-results/".Length..].Any(c => !char.IsAsciiHexDigit(c)) ||
            path.Split('/').Any(p => p is "" or "." or "..") || path.Contains(':') || path.Contains('\\'))
            throw new WorkspaceException("GitResultIncomplete", "Require an owned immutable Git result and ordinary relative payload path.");
        var start = new ProcessStartInfo("git") { WorkingDirectory = project, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string key in start.Environment.Keys.Where(k => k.StartsWith("GIT_", StringComparison.OrdinalIgnoreCase)).ToArray()) start.Environment.Remove(key);
        foreach (string arg in new[] { "cat-file", "blob", reference + ":" + path }) start.ArgumentList.Add(arg);
        return new Payload(Process.Start(start) ?? throw new WorkspaceException("GitResultIncomplete", "Cannot open the owned Git payload reader."));
    }
    private sealed class Payload : Stream
    {
        private readonly Process process;
        private readonly GitProgressReadStream input;
        private readonly Task<string> error;
        private bool ended;
        internal Payload(Process process)
        {
            this.process = process; error = process.StandardError.ReadToEndAsync();
            input = new(process.StandardOutput.BaseStream, () => { try { if (!process.HasExited) process.Kill(); } catch (InvalidOperationException) { } });
        }
        public override int Read(Span<byte> buffer)
        {
            if (buffer.IsEmpty) return 0;
            int n = input.Read(buffer);
            if (n == 0 && !ended)
            {
                if (!process.WaitForExit(30000)) { process.Kill(); throw new WorkspaceException("GitResultIncomplete", "The owned payload reader did not exit."); }
                error.GetAwaiter().GetResult(); ended = true;
                if (process.ExitCode != 0) throw new WorkspaceException("GitResultIncomplete", "The independent Git payload is unavailable.");
            }
            return n;
        }
        public override int Read(byte[] b, int offset, int count) => Read(b.AsSpan(offset, count));
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                input.Dispose(); if (!process.HasExited) { process.Kill(); process.WaitForExit(); }
                process.StandardOutput.Dispose(); process.Dispose();
            }
            base.Dispose(disposing);
        }
        public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long o, SeekOrigin s) => throw new NotSupportedException();
        public override void SetLength(long l) => throw new NotSupportedException();
        public override void Write(byte[] b, int o, int n) => throw new NotSupportedException();
    }
}
