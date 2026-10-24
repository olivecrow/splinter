# Workspace creation throughput verification

[한국어](../ko/verification/workspace-creation-throughput.md)

This record owns WC-01–05 under the [fixed scope](../plans/workspace-creation-throughput.md). The baseline is the LU-01 Library-parallel candidate with its bounded-discard repair; previous working-tree changes remain in both variants. The new product changes combine bounded forward retention during the first Git blob proof with one large-file read-ahead task during initial Git index refresh. Raw evidence is under `artifacts/workspace-creation-20261024/`.

## Reader evidence

The first matched component observations read all 1,602 files / 2,804,769,932 bytes of real Unity source commit `fd611140aa0c22d66782c19b007f1a68c00af2a6`, verifying every returned file SHA-256 against the independent manifest. The input repository is read only; its refs, settings and files are not changed by this probe. Before: **27.844s**, **5,570,158,047 decoded bytes**; candidate: **19.659s**, **2,792,956,972 decoded bytes**. Identical blobs share proofs, so unique decoded bytes can be below total file bytes. Both publish 1,563 full blob proofs and start only 2 reader processes.

Retained chunk payload peaks at **16,777,216 → 267,815,217 bytes**, within the respective 16MiB and 256MiB bounds. Managed allocations are approximately 2.913 → 2.912GB across the entire traversal, not retained-memory measurements. Each candidate full proof may retain a forward window up to 128MiB; all pending/requested/read-ahead chunks share the 256MiB/4,096-entry LRU. The full object hash/framing still gates publication, and cache hits hash current retained bytes against the chunk proof. The decoder, proof limits, 30-second no-progress timeout, disk-hydration prohibition and individual journal durability are unchanged.

These are component measurements with a constant health observer; WinFsp, Git index refresh, Library and Editor costs are excluded. Native creation evidence below determines adoption, rather than promoting this pair to an end-to-end gain.

## Protection coverage

The final focused reader suite passed **47 / 0 failed / 1 opt-in skip**. It covers loose/packed sequential and alias reads, bounded eviction and over-window reads, full-proof failure, corrupted read-ahead RAM, proof eviction, failed-peer publication exclusion and close/drain with read-ahead enabled. Earlier request-only/session tests explicitly disable speculative retention to continue exercising uncached decoder paths.

An earlier broad Git run had 1 expected-fixture mismatch: the corruption test's tiny formerly-uncached suffix now fit in the larger immutable cache, so it returned the original verified bytes. The test now explicitly disables read-ahead for that storage-corruption path; separate tests cover cached-byte corruption and fixed cached bytes after object-store changes. No product guard was loosened to make the test pass. Initial command-line filter errors performed no tests and are not counted as evidence.

## Candidate selection

The cache-only native ABBA produced **74.080 / 65.376 / 65.305 / 73.863s**: means **73.972 → 65.340s**, saving **8.631s (11.7%)**. Mount connection stayed at 17.8–17.9s; attach/inspection changed from 48.1–48.4s to 39.5–39.6s. This alone was too small for the requested priority. All four tasks passed content/mtime, independent initial source fingerprint, private edit detection/original isolation, explicit discard and provider/delta/registration/ref cleanup checks. Common Library prepare/restore took 43.833/21.134s and are outside creation timing. Final Library bytes and times matched; all owned providers exited.

A private larger-pipe prototype verified the same 1,563 distinct blobs / 2,787,030,306 bytes in 15.381 → 11.409s. The approximately 4s component saving did not justify adding a different Windows process transport. It is excluded from product code and is not an end-to-end result.

WC-05 instead overlaps one task reading the first byte of each regular file at least 1MiB with mandatory initial index refresh. The existing immutable reader performs its whole-blob proof and retains the bounded window; Git's following content reads reuse it. `Task.WhenAll` joins both operations on success, failure and cancellation. Only first-index/unchanged-source attach enables this hint; existing-index recovery and changed candidates retain mandatory refresh without read-ahead. Outstanding index locks remain preserved. A same-input pilot took **51.778s**, with **25.995s** in attach/inspection and **18.014s** in mount connection. Its private-edit/isolation, discard, Library restoration and final exact-input/provider-exit checks passed. This one pilot motivated the final controlled comparison; it is not included in its means.

## Native creation and final gate

WC-03 uses one common fixed Library basis (42,038 files / 3,105,800,655 bytes), one workspace at a time, before → after → after → before. Full source/Library content and modification-time checks sit outside `workspace ensure` timing. Each initial source fingerprint is independently checked; a post-timing code edit must be visible in Git while the original remains unchanged, then the exact task is discarded. Common Library preparation/restoration is reported separately. No Editor is launched, and no old LU/SD/BS workflow is resumed.

The required `pwsh -NoProfile -File scripts/build.ps1` passed locked restore, build (0 warnings/errors), and **1,580 passed / 0 failed / 26 opt-in skipped / 1,606 total**. 5 new initial-read-ahead cases cover real Git refresh success/failure, admitted-reader drain, injected read failure and existing-index retry, changed candidates, and cancellation. The first gate stopped at 7 test cancellation-token analyzer errors; those were corrected before this passing gate. Raw logs/TRX are retained.

WC-03-final completed on the final tested source/runtime. With the common Library basis already prepared:

| Trial | Version | Creation (`workspace ensure`) | Mount connection | Git attach + initial inspection |
| --- | --- | ---: | ---: | ---: |
| 1 | Before | 74.162s | 17.868s | 48.374s |
| 2 | After | 51.893s | 18.000s | 25.919s |
| 3 | After | 51.937s | 18.052s | 25.942s |
| 4 | Before | 73.745s | 17.847s | 48.066s |
| Mean | Before → after | **73.953 → 51.915s** | 17.857 → 18.026s | **48.220 → 25.931s** |

Creation saved **22.038s (29.8%)** in this matched two-per-version series. The gain is concentrated in Git attach/inspection. Mount connection, including Library admission, is unchanged; it and the remaining Git scan are the largest creation costs. The combined candidate is adopted. Cache-only and pilot observations above are not pooled into these means.

Common Library preparation/restoration took **43.672 / 21.157s**, outside creation timing. Private edits and discard were also outside that interval; mean discard times were **7.788 → 7.560s**, with no cleanup speed claim. All four tasks passed independent initial source-fingerprint checks, private-edit detection/original isolation, explicit discard, absent mount/delta, exact owned-provider exit, and restored Git registrations/refs/configuration. Complete source/Library bytes and file modification times matched before/after every trial and after the final Library restore. Every measured task and Library provider exited. The disposable experiment checkout was subsequently removed; receipts, raw evidence and the clone's hard-linked Git metadata are retained without changing shared object attributes.

The [machine-readable report](../../reports/benchmarks/workspace-creation-throughput-20261024.json) records exact times, input identities, runtime hashes, source hashes, validation and closure. The two product source files are `GitObjectSnapshotTree.cs` and `GitOverlayRegistration.cs`; dependent runtime assemblies were rebuilt as recorded. WC-01–05 are complete within this scope. There are no new commands, schemas, durable formats or background services.

Limits: one Windows machine with 12 logical processors, two observations per version, OS caches not cleared, and one fixed real Unity source/Library input. No concurrent builds/tests ran during timing. This is **Git-based editorless creation after Library preparation**, not an updated full Unity lifecycle or universal project/backend speedup. Original integration, Editor startup/compile/Play/Test/build, changed-original Library restoration, other VCS/backend performance, whole-process peak memory and OS crash qualification are **NotRun** for this change.
