using System.Buffers.Binary;
using Splinter.Core;
using Splinter.SourceControl.Files;

namespace Splinter.Tests;

public sealed class PlasticRevisionStreamTests
{
    [Fact]
    public void PrivateFramesPreserveBinaryAndBomAcrossSmallReadsAndCloseOnce()
    {
        byte[] content = [0xef, 0xbb, 0xbf, 0, 0x80, 0xff, 42];
        using var pipe = new MemoryStream(); using var writer = new BinaryWriter(pipe, System.Text.Encoding.UTF8, true);
        writer.Write(3); writer.Write(content, 0, 3); writer.Write(4); writer.Write(content, 3, 4); writer.Write(0); pipe.Position = 0;
        var closed = new List<bool>(); var stream = new PlasticRevisionStreamSession.RevisionStream(pipe, closed.Add);
        using var output = new MemoryStream(); byte[] part = new byte[2]; int n;
        while ((n = stream.Read(part)) != 0) output.Write(part, 0, n);
        Assert.Equal(content, output.ToArray()); stream.Dispose(); stream.Dispose();
        Assert.Equal([true], closed); Assert.True(pipe.CanRead);
    }
    [Theory]
    [InlineData(-1, 0, "PlasticBasisUnavailable")]
    [InlineData(-2, 0, "PlasticBasisUnsupported")]
    [InlineData(65537, 0, "PlasticBasisUnavailable")]
    [InlineData(4, 2, "PlasticBasisUnavailable")]
    public void FailedOversizedAndTruncatedBodiesCannotBeReused(int length, int bytes, string code)
    {
        using var pipe = new MemoryStream();
        byte[] header = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(header, length);
        pipe.Write(header); pipe.Write(new byte[bytes]); pipe.Position = 0;
        bool? complete = null;
        using (var stream = new PlasticRevisionStreamSession.RevisionStream(pipe, value => complete = value))
            Assert.Equal(code, Assert.Throws<WorkspaceException>(() => stream.CopyTo(Stream.Null)).Code);
        Assert.False(complete);
    }
    [Fact]
    public void AbandoningAPartialRevisionInvalidatesTheChild()
    {
        using var pipe = new MemoryStream([3, 0, 0, 0, 1, 2, 3, 0, 0, 0, 0]);
        bool? complete = null;
        using (var stream = new PlasticRevisionStreamSession.RevisionStream(pipe, value => complete = value)) Assert.Equal(1, stream.ReadByte());
        Assert.False(complete);
    }
}
