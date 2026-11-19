# Validation scope

Splinter's Windows/NTFS validation combines automated contract and recovery tests with native filesystem checks and small Unity runs. Each run qualifies the recorded revision, inputs, and operations. The current storage format and recovery rules are in the [storage guide](../scalable-storage.md).

## Reproducible checks

Run `pwsh ./scripts/build.ps1` for locked restore, Release build, and the default regression suite. `-NoRestore` reuses a locked restore. CI runs the Windows suite and compiles on Linux. Native runtime qualification is Windows-specific.

The default xUnit schedule runs eight concurrent classes with conservative scheduling. Tests that reserve the full process-wide scratch budget run exclusively. The default paged-store and catalog shapes use 32 and 1,024 entries. Set `SPLINTER_LARGE_CATALOG_TESTS=1` only for separately scoped capacity checks at 32,769, 262,145, and 1,048,576 entries.

The latest November 19 complete Windows gate passed in 470.26s of monotonic wall time. Of 2,652 discovered cases, 2,616 passed and 36 were conditionally skipped. All seven workspace-import cases passed. The 16 Release CLI DLLs matched the timed maze replay. Eight separate Python checks passed for the Unity timing diagnostic, including live-file replacement, domain reload and abnormal shutdown. The earlier publication and changed-Library comparison matched all 45 files in its own CLI build. The default gate also includes the journal recovery cases that reached their 10s limits during the first-start investigation.

The November 19 matched live-publication run on the warm URP project took 42.80s before and 36.19s after from checkpoint to verified original recompile. First shutdown request through both workspace releases took 86.15s and 68.66s. Library preparation through restoration took 679.98s and 597.58s, including the live original and independent checks. The earlier startup-only comparison on the same input recorded B's 590-node Bee graph at 7.372s and 6.445s. A small replay reproduced 14.77s under competing disk writes. [Parallel workspace evidence](parallel-workspace-performance.md)

Two real Git/WinFsp workspaces exercised peer import with a selected file followed by a full update. The producer continued editing after its checkpoint, the consumer kept an unrelated file, and a divergent edit produced a conflict. Repeating the earlier completed import after producer release preserved the consumer's newer edit. Both final results were retained and the original Git worktree remained clean. The local receipts are in `artifacts/maze-20261119/optimized/mz19/peer-import/`. [Workspace import](../workspace-import.md)

## Remaining qualification

- **Scale and lifecycle.** SCAL-01–08 remains incomplete. Full native and fault coverage, the 17 GiB workload, complete 100k original publication and reclamation across Git, Plastic, and plain folders, and general peak RSS and matched lifecycle costs remain unqualified. A bounded plain-folder recheck reached capture and a read-only plan. [SCAL plan](../plans/scalable-storage.md)
- **Platform and performance.** The small Git, plain-folder, Plastic, and Unity results apply to their own inputs. General throughput, space and memory savings, parallel build and test performance, comprehensive ARM coverage, other operating systems, reboot, power-loss, and disk-failure durability remain unqualified. End-to-end setup and agent handoff scenarios have partial or unrun qualification.
- **Retained large fixtures.** The three 100k providers stopped normally after slow phases. The managed original still needs restoration, and the original, basis, task, results, request state, and matching historical binaries remain protected. Resuming this work requires its own bounded execution scope.

## Recording evidence

Estimate each phase from its actual scope and stop safely at twice the estimate. Record the revision, input, command, outcome, elapsed time, and any unexecuted scope. Record canceled runs and deadline overruns separately from completed gates. Keep reproducible diagnostics and current limitations here, with prior runs available in Git history. [Contributor instructions](../../AGENTS.md)
