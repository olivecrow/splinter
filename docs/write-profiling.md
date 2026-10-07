<a id="live-journal-쓰기-계측"></a>
# Live journal write instrumentation

[한국어](ko/write-profiling.md)

`tools/Splinter.WriteProbe` is a development tool for comparing fixed small synthetic write costs in Runtime. It does not create product workspaces, VSS, or Unity instances. Its scope follows the [JW plan](write-profiling.md).

<a id="고정-입력과-사용법"></a>
## Fixed inputs and usage

Build with the standard `pwsh ./scripts/build.ps1`, then specify an output directory that does not yet exist.

```powershell
dotnet run --project tools/Splinter.WriteProbe --configuration Release --no-build -- run serial-1024 artifacts/write-probe/new-serial
dotnet run --project tools/Splinter.WriteProbe --configuration Release --no-build -- run parallel-4x256 artifacts/write-probe/new-parallel
dotnet run --project tools/Splinter.WriteProbe --configuration Release --no-build -- run metadata-1024 artifacts/write-probe/new-metadata
```

| profile | Actual Runtime calls |
| --- | --- |
| serial-1024 | 1,024 sequential 4KiB writes to one file. |
| parallel-4x256 | 4 dedicated threads each write 4KiB to their own file 256 times. |
| metadata-1024 | 1,024 changes to one file's last-write time. |

Only a synthetic snapshot identity with no original is used, with a 16MiB journal limit per run. Existing paths are rejected; output journals/identities are not automatically deleted. Independent expected content hashes/metadata are checked in live state and read-only replay, and the journal hash must match before and after replay. Any attempt to read original payload fails.

The timer includes the call loop and thread preparation/start after fixture-file creation, and excludes the final explicit Flush, content verification, and replay. `storage.metrics` contains cumulative provider values, including file creation and the final explicit Flush. `allocatedManagedBytes` is cumulative managed allocation during the call loop; `peakWorkingSetBytes` is the process's highest observed value through replay verification. These do not describe the same interval or disk cost.

Record hashes of the same probe DLL and baseline/instrumented Runtime/Core, and repeat in new output directories with changed run order. New journals can have different hashes because IDs/times differ. Judge content equivalence between implementations by `contentHash`, and input preservation within each run by its own `journalHash`. Measure file allocation separately through Windows instrumentation. Do not convert small synthetic differences into real Unity timing or physical-space savings.

<a id="제품-관측의-해석"></a>
## Interpreting product observations

The contract for `OverlayIoMetrics.liveJournalWrites` follows [source verification and instrumentation](source-verification.md). Only the fixed 6 live-commit operations are counted; replay is excluded. Per-operation stage times are costs after obtaining the gate, while gate waits are cumulative per-call values. Waiter times can overlap, so do not add them to Editor wall time.

Counts of other mutation calls before/after flush are samples of calls waiting ahead of the Runtime gate. They exclude reads, waits inside WinFsp/OS, and peak concurrency between samples. Waiting calls may later finish without changes or with rejected input, so they do not guarantee a number of durable commits that can be grouped. Every current acknowledged commit completes its own durable flush before responding.

Windows `WinFspOverlayFileSystem` applies a separate `mutationGate` around the entire mutation callback. Writes through this adapter are therefore serialized before entering Runtime. A zero Runtime wait sample does not mean there were no adapter/OS waits. Concurrency in `parallel-4x256`, which calls Runtime directly, differs from real Unity concurrency. Do not assume flush grouping can simply be added inside Runtime without reviewing this boundary, which protects composite checks of append position, constrained I/O, and file length.
