using System.Text.Json;
using Splinter.Core;

namespace Splinter.Runtime;

public sealed record OperationDiskSpaceRequirement(string Operation, long AdditionalPayloadBytes,
    long MinimumControlBytes, long MinimumRequiredBytes, long AvailableBytes, string Volume);

// This is an operation-derived lower bound, not a project/journal quota or a
// fixed free-space reserve. Concurrent external writes and additional COW pages
// can still exhaust the device; the caller keeps its durable recovery intent.
public static class OperationDiskSpace
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static OperationDiskSpaceRequirement Require(string operation, string path, long finalPayloadBytes,
        long alreadyAllocatedBytes, long minimumControlBytes)
    {
        string volume = Path.GetPathRoot(Path.GetFullPath(path)) ?? throw new WorkspaceException("StorageSpaceUnknown", "The operation has no backing volume.");
        long available;
        try { available = new DriveInfo(volume).AvailableFreeSpace; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        { throw new WorkspaceException("StorageSpaceUnknown", "Preserve this operation until its backing volume's free space can be observed."); }
        var requirement = Calculate(operation, finalPayloadBytes, alreadyAllocatedBytes, minimumControlBytes, available, volume);
        if (requirement.MinimumRequiredBytes > available)
            throw new WorkspaceException("StorageSpaceInsufficient", "Preserve the fixed request and owned stages; free space before retrying this operation.", requirement);
        return requirement;
    }
    public static OperationDiskSpaceRequirement Calculate(string operation, long finalPayloadBytes, long alreadyAllocatedBytes,
        long minimumControlBytes, long availableBytes, string volume)
    {
        if (finalPayloadBytes < 0 || alreadyAllocatedBytes < 0 || minimumControlBytes < 0 || availableBytes < 0)
            throw new WorkspaceException("StorageSpaceInvalid", "Operation space observations must be nonnegative.");
        long payload = Math.Max(0, finalPayloadBytes - alreadyAllocatedBytes);
        long required = checked(payload + minimumControlBytes);
        return new(operation, payload, minimumControlBytes, required, availableBytes, volume);
    }
    public static long ControlBytes(object value)
    { using var output = new Counter(); JsonSerializer.Serialize(output, value, Json); return output.Length; }
    private sealed class Counter : Stream
    {
        private long length;
        public override void Write(ReadOnlySpan<byte> buffer) => length = checked(length + buffer.Length);
        public override void Write(byte[] buffer, int offset, int count) => length = checked(length + count);
        public override long Length => length;
        public override long Position { get => length; set => throw new NotSupportedException(); }
        public override bool CanRead => false; public override bool CanSeek => false; public override bool CanWrite => true;
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
