using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using static Splinter.SourceControl.Files.PlasticRevisionClient;

namespace Splinter.SourceControl.Files;

// A tree-owned, private stdio child. No listening socket, command API or
// revision payload file is created. Only one revision stream owns the child.
internal sealed class PlasticRevisionStreamSession : IPlasticRevisionReader, IDisposable
{
    private readonly SemaphoreSlim streamLease = new(1, 1);
    private Process? process;
    private Task? errors;
    private string? fixedTree;
    private bool disposed;

    private void Start(string treeSpec)
    {
        if (fixedTree is not null && fixedTree != treeSpec) throw Failure("PlasticBasisRequestInvalid", "A read session retains its exact fixed tree.");
        if (process is not null)
        {
            if (fixedTree != treeSpec || process.HasExited) throw Failure("PlasticBasisUnavailable", "The owned fixed-tree reader is unavailable.");
            return;
        }
        if (!OperatingSystem.IsWindows()) throw Failure("PlasticBasisUnsupported", "The recorded revision bridge requires Windows and the qualified cm client.");
        using var resource = typeof(PlasticRevisionClient).Assembly.GetManifestResourceStream("Splinter.PlasticRevisionBridge.cs")!;
        using var reader = new StreamReader(resource);
        string command = "$ErrorActionPreference='Stop'; $types=Add-Type -PassThru -TypeDefinition @'\n" + reader.ReadToEnd() +
            "\n'@; $bridge=$types | Where-Object FullName -eq 'Splinter.SourceControl.Files.PlasticRevisionBridge'; $bridge.GetMethod('Run').Invoke($null,@())";
        var info = new ProcessStartInfo("pwsh") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true,
            RedirectStandardOutput = true, RedirectStandardError = true, StandardInputEncoding = new UTF8Encoding(false) };
        foreach (string arg in new[] { "-NoProfile", "-NonInteractive", "-EncodedCommand", Convert.ToBase64String(Encoding.Unicode.GetBytes(command)) }) info.ArgumentList.Add(arg);
        try { process = Process.Start(info) ?? throw Failure("PlasticBasisUnavailable", "The private revision reader did not start."); }
        catch (System.ComponentModel.Win32Exception) { throw Failure("PlasticBasisUnavailable", "PowerShell 7 is required for the private revision reader."); }
        errors = Drain(process.StandardError); fixedTree = treeSpec;
        process.StandardInput.WriteLine(JsonSerializer.Serialize(new { repository = treeSpec[(treeSpec.IndexOf('@') + 1)..] }));
        process.StandardInput.Flush();
    }
    private static async Task Drain(StreamReader reader)
    { char[] scratch = new char[4096]; while (await reader.ReadAsync(scratch) != 0) { } }

    public Stream OpenRevision(string treeSpec, string revision)
    {
        RequireTreeSpec(treeSpec);
        if (!Numeric(revision)) throw Failure("PlasticBasisRequestInvalid", "An exact revision ID is required.");
        if (!streamLease.Wait(TimeSpan.FromSeconds(30))) throw Failure("PlasticBasisUnavailable", "The previous revision stream is still active.");
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this); Start(treeSpec);
            process!.StandardInput.WriteLine(revision); process.StandardInput.Flush();
            return new RevisionStream(process.StandardOutput.BaseStream, complete => { if (!complete) Stop(); streamLease.Release(); });
        }
        catch { streamLease.Release(); Stop(); throw; }
    }
    public Stream OpenOwnedRevision(string treeSpec, string revision) => new RevisionStreamOwner(OpenRevision(treeSpec, revision), this);
    private void Stop()
    {
        if (process is null) return;
        try
        {
            process.StandardInput.Close();
            if (!process.HasExited && !process.WaitForExit(1000)) { process.Kill(entireProcessTree: true); process.WaitForExit(); }
            try { errors?.GetAwaiter().GetResult(); } catch (IOException) { }
        }
        finally { process.Dispose(); process = null; }
    }
    public void Dispose() { if (disposed) return; disposed = true; Stop(); }

    internal sealed class RevisionStream(Stream pipe, Action<bool> onClose) : Stream
    {
        private readonly CancellationTokenSource deadline = new(TimeSpan.FromSeconds(120));
        private readonly byte[] header = new byte[4];
        private int remaining;
        private bool complete, closed;
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (closed) throw new ObjectDisposedException(nameof(RevisionStream));
            if (count == 0 || complete) return 0;
            try
            {
                if (remaining == 0)
                {
                    pipe.ReadExactlyAsync(header, deadline.Token).AsTask().GetAwaiter().GetResult();
                    remaining = BinaryPrimitives.ReadInt32LittleEndian(header);
                    if (remaining == 0) { complete = true; return 0; }
                    if (remaining == -2) throw Failure("PlasticBasisUnsupported", "The private reader requires the qualified Windows cm 11.0.16.9915 installation; preserve the task.");
                    if (remaining is < 0 or > 65536) throw Failure("PlasticBasisUnavailable", "The private revision reader failed or returned an invalid frame.");
                }
                int read = pipe.ReadAsync(buffer.AsMemory(offset, Math.Min(count, remaining)), deadline.Token).AsTask().GetAwaiter().GetResult();
                if (read == 0) throw new EndOfStreamException();
                remaining -= read; return read;
            }
            catch (Exception ex) when (ex is IOException or OperationCanceledException)
            { throw Failure("PlasticBasisUnavailable", "The private revision stream failed or timed out; preserve the task."); }
        }
        public override int Read(Span<byte> buffer)
        {
            if (buffer.Length == 0) return 0;
            byte[] staging = ArrayPool<byte>.Shared.Rent(Math.Min(buffer.Length, 65536));
            try { int n = Read(staging, 0, Math.Min(buffer.Length, staging.Length)); staging.AsSpan(0, n).CopyTo(buffer); return n; }
            finally { ArrayPool<byte>.Shared.Return(staging); }
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing && !closed)
            {
                closed = true; deadline.Dispose();
                // An abandoned/failed body cannot become the next revision.
                onClose(complete);
            }
            base.Dispose(disposing);
        }
        public override bool CanRead => !closed;
        public override bool CanSeek => false; public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException(); public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { } public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException(); public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
    private sealed class RevisionStreamOwner(Stream stream, PlasticRevisionStreamSession owner) : Stream
    {
        public override int Read(Span<byte> buffer) => stream.Read(buffer);
        public override int Read(byte[] buffer, int offset, int count) => stream.Read(buffer, offset, count);
        protected override void Dispose(bool disposing) { if (disposing) { stream.Dispose(); owner.Dispose(); } base.Dispose(disposing); }
        public override bool CanRead => stream.CanRead; public override bool CanSeek => false; public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException(); public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { } public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException(); public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
