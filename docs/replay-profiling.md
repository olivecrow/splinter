<a id="offline-replay-계측"></a>
# Offline replay instrumentation

[한국어](ko/replay-profiling.md)

`tools/Splinter.ReplayProbe` is a development tool for comparing Runtime replay of synthetic journals. It does not create product workspaces, VSS/WinFsp mounts, or Unity Editors, or read existing originals. Availability and boundaries are also reported in `capabilities.developmentReplayProbe`. It does not replace evidence of real Unity timing or storage use.

<a id="실행"></a>
## Running the tool

First build with `pwsh ./scripts/build.ps1`. Specify a new input path.

```powershell
dotnet run --project tools/Splinter.ReplayProbe --configuration Release --no-build -- generate append artifacts/replay-example/append
dotnet run --project tools/Splinter.ReplayProbe --configuration Release --no-build -- run artifacts/replay-example/append
```

`generate <append|overlap|file-renames|write-append-4096|write-append-16384|write-overwrite-16384|write-distributed-16384> <new directory>` rejects an existing path. `append` performs 8,192 sequential 4KiB writes; `overlap` performs an initial 2,048 4KiB writes and 6,144 overlapping 1KiB writes with tail shrink/extension; `file-renames` uses 35,000 files and 4,032 renames. The four `write-*` inputs use 1KiB payloads: 4,096/16,384 sequential writes to one file, 16,384 repeated overwrites of one range, and 16,384 total writes distributed across 128 files. Each additional input has a 32MiB journal limit; the existing inputs have a 128MiB limit. Generation creates payloads, frame checksums, identity, independent expected file hashes, and a completion receipt. The final flush during fixture generation does not reproduce the product's acknowledged-write flush policy or write performance.

`run <sealed synthetic directory>` checks the tool's fixed synthetic identity and journal hash. It invokes the private read-only Runtime constructor with the same signature through reflection, allowing existing comparison binaries to be used unchanged. A binary without that constructor is explicitly rejected. This is not a stable product API. Read-only handles are used; even an incomplete input tail is not repaired. Existing inputs are neither overwritten nor automatically cleaned up.

After replay, the tool checks every expected file's length/content hash, the path list and deleted paths, internal state hash, durable sequence and replay frames, and zero write commits/flushes/base payload reads. Finally, it rechecks that the original journal hash is unchanged. It is not intended for general damaged-journal recovery or diagnosis of arbitrary workspaces.

<a id="해석"></a>
## Interpreting results

`elapsedTicks / stopwatchFrequency` measures store creation time. Full input hashing, output-content verification, state-hash calculation, and process startup are separate. `storage.metrics` contains actual Runtime replay counters; see [source verification and instrumentation](source-verification.md) for their per-operation meaning.

`allocatedManagedBytes` is cumulative managed allocation during creation, not simultaneous memory demand or disk space. `managedHeapBytes` estimates heap size immediately after replay and may include objects not yet collected by GC. `peakWorkingSetBytes` is the observed peak working set of the whole process up to that point. Receipt/hash preparation and Runtime initialization are also included in process memory. See [source verification and instrumentation](source-verification.md) for per-range costs and extent distribution in `replayWriteRangeIndex`.

Reading the full hash immediately before each run warms the OS cache. The tool does not forcibly clear it. Record separate processes, run order, repeat count, and matching input/state hashes. Verify file allocation separately, for example with FILE_STANDARD_INFO. [RI verification](verification/replay-index-cost.md) and [WI verification](verification/cumulative-write-range-index.md) own this tool's execution scope and binary comparisons; [VR verification](verification/validation-view-readiness-and-replay.md) and the [RU small-Unity comparison](verification/unity-replay-comparison.md) own real Unity observations. Restarting a real Editor may add journal records, so offline improvement for the same input cannot be used to estimate total resumption time or disk savings.
