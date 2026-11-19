# Contributor verification

The [Unity maze benchmark](parallel-workspace-performance.md) compares three isolated Splinter Editors with three agents sharing a Unity Agent Coordination FIFO Editor. It covers one Windows machine, one Unity version and one warm project.

## Reproducible checks

Run `pwsh ./scripts/build.ps1` for locked dependency restore, Release build and the default Windows regression suite. The verified run took 469.80 s. Of 2,689 discovered cases, 2,653 passed and 36 were conditionally skipped. All 16 CLI Release DLLs matched the measured Splinter build. Coverage includes interrupted cross-partition moves, Library restoration, read-only source flushing and concurrent access to Unity compilation symbols. Twelve separate timing diagnostic checks passed with `python -m unittest discover -s tests -p test_unity_workflow_timing.py`.

The default xUnit schedule runs eight classes concurrently, with exclusive collections for process-wide resources. Default paged-store and catalog fixtures use 32 and 1,024 entries. `SPLINTER_LARGE_CATALOG_TESTS=1` enables separate capacity checks at 32,769, 262,145 and 1,048,576 entries. CI builds on Linux. Native runtime validation uses Windows and NTFS.

Use native checks for changed side-effect boundaries and reproduced defects. Tie coverage to the platforms, inputs, failure cases, and workloads that ran. Measure per-task changes, shared bases and Library, results, temporary files, and resource peaks separately. Report unknown quantities explicitly.

## Remaining qualification

The full Git, Plastic and plain-folder 100,000-entry lifecycle, the 17 GiB workload, general peak memory and throughput, other platforms and OS/power-loss recovery remain unqualified. Retained large fixtures include a managed original awaiting restoration. Its project, bases, results, request state and matching binaries must remain available for separately authorized recovery. [Scale and fault checks](../plans/scalable-storage.md)

Record the exact input, binaries, elapsed time and outcome for each qualification. Preserve incomplete state and keep source and result ownership intact on interruption. Block affected operations if evidence shows acknowledged-write loss or damage to the original or peers. [Contributor instructions](../../AGENTS.md)
