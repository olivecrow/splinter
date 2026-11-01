namespace Splinter.Runtime;

// Demand-driven resource ceilings, not a reservation or a process working-set
// guarantee. Leave headroom for Git/decoder buffers, native handles and the GC.
public static class StorageResources
{
    public const long MaximumWorkingTargetBytes = 4L * 1024 * 1024 * 1024;
    public static long WorkingTargetBytes { get; } = Target(GC.GetGCMemoryInfo().TotalAvailableMemoryBytes);
    public static long MetadataCacheBytes => WorkingTargetBytes * 5 / 8;
    public static long ScratchBytes => WorkingTargetBytes / 8;
    public static int SortRunBytes => (int)Math.Min(128L * 1024 * 1024, ScratchBytes / 2);
    public static int SpoolBytes => (int)Math.Min(64L * 1024 * 1024, ScratchBytes / 4);
    public static int HashWorkers => Math.Clamp(Environment.ProcessorCount, 1, 32);
    public static int IoWorkers => Math.Clamp(Environment.ProcessorCount * 2, 8, 32);
    public static int MetadataReaders => Math.Clamp((int)(MetadataCacheBytes / PagedMetadataStore.ContainerTargetBytes) + 8, 16, 64);
    internal static readonly ScratchBudget Scratch = new(ScratchBytes);
    internal static long Target(long available) => Math.Clamp(available > 0 ? available / 8 : 512L * 1024 * 1024,
        128L * 1024 * 1024, MaximumWorkingTargetBytes);
    public static long PublicationStagingBytes(long largest, long freeBytes)
    {
        if (largest < 0 || freeBytes < 0) throw new ArgumentOutOfRangeException(nameof(largest));
        long parallel = largest > long.MaxValue / IoWorkers ? long.MaxValue : largest * IoWorkers;
        // Existing large stages may already occupy disk. Always permit their
        // single-file recovery; additional concurrent stages use only headroom.
        return Math.Max(largest, Math.Min(parallel, Math.Min(WorkingTargetBytes / 4, freeBytes / 4)));
    }
}

// Aggregate accounting across concurrent and nested sorts/spools. Only charge
// memory immediately about to be used; denied growth falls back to owned disk.
internal sealed class ScratchBudget(long limit)
{
    private readonly long capacity = limit;
    private readonly object gate = new();
    private long used;
    internal long Used { get { lock (gate) return used; } }
    internal Lease Open() => new(this);
    internal sealed class Lease(ScratchBudget owner) : IDisposable
    {
        private long bytes;
        private bool disposed;
        internal bool TryResize(long desired)
        {
            if (desired < 0) throw new ArgumentOutOfRangeException(nameof(desired));
            lock (owner.gate)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                long delta = desired - bytes;
                if (delta > owner.capacity - owner.used) return false;
                owner.used += delta; bytes = desired; return true;
            }
        }
        public void Dispose()
        {
            lock (owner.gate)
            { if (disposed) return; disposed = true; owner.used -= bytes; bytes = 0; }
        }
    }
}
