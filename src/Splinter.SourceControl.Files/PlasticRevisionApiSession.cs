using System.Buffers;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using static Splinter.SourceControl.Files.PlasticRevisionClient;

namespace Splinter.SourceControl.Files;

// A lazy tree-owned cm helper, not a shared server or daemon. Only fixed
// revision blob GETs are issued. Console output is discarded, never payload.
internal sealed class PlasticRevisionApiSession : IPlasticRevisionReader, IDisposable
{
    private readonly HttpClient http = new(new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false, MaxResponseHeadersLength = 16 });
    private Process? process; private Task? output, error; private int port; private bool disposed;
    private void Start()
    {
        ObjectDisposedException.ThrowIf(disposed, this); if (process is not null) return;
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
        var info = new ProcessStartInfo("cm") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        info.ArgumentList.Add("api"); info.ArgumentList.Add("--port=" + port);
        try { process = Process.Start(info) ?? throw Failure("PlasticBasisUnavailable", "The owned loopback revision helper did not start."); }
        catch (System.ComponentModel.Win32Exception) { throw Failure("PlasticBasisUnavailable", "The revision helper is unavailable."); }
        output = Drain(process.StandardOutput); error = Drain(process.StandardError);
    }
    private static async Task Drain(StreamReader reader)
    { char[] scratch = new char[4096]; while (await reader.ReadAsync(scratch) != 0) { } }
    public Stream OpenRevision(string treeSpec, string revision)
    {
        RequireTreeSpec(treeSpec); if (!Numeric(revision)) throw Failure("PlasticBasisInvalid", "An exact revision ID is required.");
        Start(); using var startup = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        string repository = treeSpec[(treeSpec.IndexOf('@') + 1)..];
        var uri = new Uri("http://localhost:" + port + "/api/v1/repos/" + Uri.EscapeDataString(repository) + "/revisions/" + revision + "/blob");
        while (true)
        {
            if (process!.HasExited) throw Failure("PlasticBasisUnavailable", "The owned revision helper exited; preserve the task.");
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                var response = http.Send(request, HttpCompletionOption.ResponseHeadersRead, startup.Token);
                if (response.StatusCode != HttpStatusCode.OK || process.HasExited)
                { response.Dispose(); throw Failure("PlasticBasisUnavailable", "The exact revision blob is unavailable."); }
                return new RevisionStream(response);
            }
            catch (HttpRequestException ex) when (ex.InnerException is SocketException { SocketErrorCode: SocketError.ConnectionRefused })
            {
                try { Task.Delay(100, startup.Token).GetAwaiter().GetResult(); }
                catch (OperationCanceledException) { throw Failure("PlasticBasisUnavailable", "The owned loopback helper did not become ready."); }
            }
            catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
            { throw Failure("PlasticBasisUnavailable", "The fixed revision request failed or timed out."); }
        }
    }
    public Stream OpenOwnedRevision(string treeSpec, string revision) => new RevisionStreamOwner(OpenRevision(treeSpec, revision), this);
    public void Dispose()
    {
        if (disposed) return; disposed = true; http.Dispose();
        if (process is null) return;
        try
        {
            if (!process.HasExited)
            {
                try { process.StandardInput.WriteLine(); process.StandardInput.Flush(); } catch (IOException) { }
                if (!process.WaitForExit(2000)) { process.Kill(entireProcessTree: true); process.WaitForExit(); }
            }
            try { Task.WhenAll(output!, error!).GetAwaiter().GetResult(); } catch (IOException) { }
        }
        finally { process.Dispose(); }
    }
    private class RevisionStream : Stream
    {
        private readonly HttpResponseMessage response; private readonly Stream stream;
        private readonly CancellationTokenSource deadline = new(TimeSpan.FromSeconds(120)); private bool closed;
        public RevisionStream(HttpResponseMessage response) { this.response = response; stream = response.Content.ReadAsStream(); }
        public override int Read(byte[] buffer, int offset, int count)
        {
            try { return stream.ReadAsync(buffer.AsMemory(offset, count), deadline.Token).GetAwaiter().GetResult(); }
            catch (OperationCanceledException) { throw Failure("PlasticBasisUnavailable", "The fixed revision body timed out."); }
        }
        public override int Read(Span<byte> buffer)
        {
            if (buffer.Length == 0) return 0;
            byte[] staging = ArrayPool<byte>.Shared.Rent(Math.Min(buffer.Length, 65536));
            try { int n = Read(staging, 0, Math.Min(buffer.Length, staging.Length)); staging.AsSpan(0, n).CopyTo(buffer); return n; }
            finally { ArrayPool<byte>.Shared.Return(staging); }
        }
        protected override void Dispose(bool disposing)
        { if (disposing && !closed) { closed = true; response.Dispose(); deadline.Dispose(); } base.Dispose(disposing); }
        public override bool CanRead => !closed; public override bool CanSeek => false; public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException(); public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { } public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException(); public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
    private sealed class RevisionStreamOwner(Stream stream, PlasticRevisionApiSession owner) : Stream
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
