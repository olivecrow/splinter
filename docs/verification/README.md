# Validation scope

Splinter's Windows/NTFS validation combines automated contract and recovery tests with native filesystem checks and small Unity runs. Each run qualifies the recorded revision, inputs, and operations. The current storage format and recovery rules are in the [storage guide](../scalable-storage.md).

## Reproducible checks

Run `pwsh ./scripts/build.ps1` for locked restore, Release build, and the default regression suite. `-NoRestore` reuses a locked restore. CI runs the Windows suite and compiles on Linux. Native runtime qualification is Windows-specific.

The default xUnit schedule runs eight concurrent classes with conservative scheduling. Tests that reserve the full process-wide scratch budget run exclusively. The default paged-store and catalog shapes use 32 and 1,024 entries. Set `SPLINTER_LARGE_CATALOG_TESTS=1` only for separately scoped capacity checks at 32,769, 262,145, and 1,048,576 entries.

The recorded complete gate for sealed-reader integration ran in 666.76s and finished with 2,605 passes, 36 conditional skips, and zero failures. The later live metadata repack change has focused native and component evidence. The matched Unity run used one small warm URP project and verified independent Editor execution, compile, scene and Play isolation, both results, and Library restoration. The detailed record identifies the measured binaries and separates completed lifecycle runs, retries, and component checks. [Parallel workspace evidence](parallel-workspace-performance.md)

## Remaining qualification

- **Scale and lifecycle.** SCAL-01–08 remains incomplete. Full native and fault coverage, the 17 GiB workload, complete 100k original publication and reclamation across Git, Plastic, and plain folders, and general peak RSS and matched lifecycle costs remain unqualified. A bounded plain-folder recheck reached capture and a read-only plan. [SCAL plan](../plans/scalable-storage.md)
- **Platform and performance.** The small Git, plain-folder, Plastic, and Unity results apply to their own inputs. General throughput, space and memory savings, parallel build and test performance, comprehensive ARM coverage, other operating systems, reboot, power-loss, and disk-failure durability remain unqualified. End-to-end setup and agent handoff scenarios have partial or unrun qualification.
- **Retained large fixtures.** The three 100k providers stopped normally after slow phases. The managed original still needs restoration, and the original, basis, task, results, request state, and matching historical binaries remain protected. Resuming this work requires its own bounded execution scope.

## Recording evidence

Estimate each phase from its actual scope and stop safely at twice the estimate. Record the revision, input, command, outcome, elapsed time, and any unexecuted scope. Record canceled runs and deadline overruns separately from completed gates. Keep reproducible diagnostics and current limitations here, with prior runs available in Git history. [Contributor instructions](../../AGENTS.md)
