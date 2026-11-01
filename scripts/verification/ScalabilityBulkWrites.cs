using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

// Functional 100k-input driver. Small matched cost trials keep their original
// sequential driver. Parallel.For drains all admitted writes before returning.
public static class ScalabilityBulkWrites
{
    public static long Run(string root, int count, int directories, DateTime timestamp)
    {
        long completed = 0;
        Parallel.For(0, count, new ParallelOptions { MaxDegreeOfParallelism = 8 },
            () => new byte[32], (index, loop, bytes) =>
            {
                int variant = (index + 1) % 16;
                for (int j = 0; j < bytes.Length; j++) bytes[j] = (byte)((variant * 17 + j * 7) % 256);
                string relative = $"d{index % directories:D2}/f{index:D7}.bin";
                string path = Path.Combine(root, relative);
                File.WriteAllBytes(path, bytes);
                File.SetLastWriteTimeUtc(path, timestamp);
                Interlocked.Increment(ref completed);
                return bytes;
            }, _ => { });
        return completed;
    }
}
