# Git result collection and reclamation diagnosis

[한국어](../ko/verification/git-result-collection-diagnosis.md)

**GCD-01, 2026-10-30:** the continuing-task Git reader repeatedly launches Git to check basis availability while walking and reading a frozen result. Final release performs that collection again before reclaiming files. This diagnosis identifies the current small-input mechanism; it implements no optimization and establishes no old/new speedup or regression percentage.

[Structured measurements](../../reports/benchmarks/git-result-collection-diagnosis-20261030.json) pin source `f91e470`, product assembly hashes, exact inputs, phase counters, historical event intervals and preservation evidence. The existing [RI-05](repeated-original-apply.md) run remains the source of actual Unity command timings. Earlier [LP/CO profiling](checkpoint-release-profiling.md) covered repeated source inspections in a different collection path; [BD/GR](unity-lifecycle-bottleneck-diagnosis.md) covered preparation and payload retention. Those findings do not substitute for this continuing-task diagnosis.

## Existing actual-run attribution

| RI-05 Git A operation | Full command, s | Capture intent → result publication, s |
| --- | ---: | ---: |
| First intermediate capture | 76.975 | 75.619 |
| Second intermediate capture | 43.113 | 41.897 |
| Final explicit capture after normal stop | 49.408 | 48.283 |
| Release after final publication | 53.632 | **45.171** for a new closure generation |

The last interval accounts for **84.2%** of release. From `release-unmount` to `released`, the actual log records **5.640s**, including **1.736s** between `release-delta` and `release-snapshot`. Deleting the private data is therefore a small portion of this observed release. The remainder includes full seals, prior result verification, ownership checks and dispatch; it is not assigned to a single measured cause.

The final published generation and release's new generation have the **same source identity**. [`SealGenerationForReleaseAsync`](../../src/Splinter.Runtime/WorkspaceGenerations.cs) nevertheless requests `release:<journal hash>` through the common capture engine. [`ReleaseSnapshotWorkspaceAsync`](../../src/Splinter.Runtime/SnapshotWorkspaces.cs) selects this path for generation-capable adapters instead of the older unchanged-result reuse path. Required shutdown writes, infrastructure removal and pre/post seals still matter: identical source after the fact is not permission to skip those guards.

## Bounded diagnostic reproduction

No Editor, mount, provider process, new workspace or Library copy was created. A small independent Git object store was populated from the retained RI Git repository. Source ranges were reconstructed from result 2 because historical journal pins had already been reclaimed. Production `SourceCaptureTree`, `GitObjectSnapshotTree`, the private `FileWorkspaceProvider.ScanTree` method and `GitSourceResult` ran from unmodified current Release assemblies. The supplied health callback times the exact default `GitObjectSnapshotProvider.ObserveAsync` call and classifies callers; it does not cache availability or weaken a check.

The basis has **27 files / 75,034 B**; result 2 has **33 files / 87,259 B**, plus three directories. Both reproductions match every retained result path/kind/length/content hash and the **exact Git commit fingerprint**. Diagnostic owner, paths, backing layout and basis timestamp metadata differ; this is not a replay of historical physical I/O or metadata identity.

| Diagnostic phase | Elapsed, s | Basis checks | Check time, s | Git commands from checks |
| --- | ---: | ---: | ---: | ---: |
| Separate initial basis scan | 13.659 | 85 | 13.473 | 170 |
| Result scan after that scan, warm reader | 19.381 | 122 | 19.359 | 244 |
| Result scan with a fresh reader, later sample | 19.872 | 122 | 19.682 | 244 |
| Git tree construction + first result verification, fresh-reader sample | 3.005 | 14 | 2.203 | 28 |

Availability checks consume **99.89% / 99.04%** of the two result scans. Warm/cold refer to the product's proof/payload cache, not OS caches. Warm reads hit all 27 basis payloads and decode **0 additional B**. Fresh reads prove **75,034 B** once, with two reader processes. Both scans have zero decoder waits, repeated payload decoding and capacity evictions. These data identify the small-input availability path; they do not exclude payload/metadata costs on larger inputs.

The fresh-reader sample's second Git graph verification takes **0.156s**; verifying three retained Git graphs takes **0.470s**. These are Git graph components, not complete per-generation verification or whole-release timings. Four small Git command categories, sampled in both runs, take approximately **0.074–0.080s each**, including process startup. Loader, scheduling, Git execution and security-scanner shares were not separated; no antivirus cause is established.

## Call chain and amplification

1. [`OpenSourceCaptureAsync`](../../src/Splinter.Platform.Windows/NtfsSourceGenerations.cs) opens a new basis through [`WorkspaceSnapshotBasis.OpenTree`](../../src/Splinter.Platform.Windows/WorkspaceSnapshotBasis.cs) without supplying the health observation maintained by the live provider. The live provider supplies its observation callback in [`WinFspOverlayServer`](../../src/Splinter.Platform.Windows/WinFspOverlayServer.cs).
2. The default [`GitObjectSnapshotProvider.OpenTree`](../../src/Splinter.SourceControl.Git/GitObjectSnapshotProvider.cs) callback executes `ObserveAsync` on every `EnsureAvailable`: one `for-each-ref` for the exact owned basis and one `rev-parse --verify` for its fixed commit tree. Each is a separate process.
3. [`SourceCaptureTree`](../../src/Splinter.Runtime/JournalSourceCapture.cs) checks availability for each list/read. The underlying [`GitObjectSnapshotTree`](../../src/Splinter.SourceControl.Git/GitObjectSnapshotTree.cs) also checks on read entry, reader admission and before publishing proved bytes, including cache hits.
4. [`PreserveGenerationAsync`](../../src/Splinter.SourceControl.Files/FileSourceGenerations.cs) hashes the entire frozen source for its result manifest. In this scan, 37 capture-level list/read checks + 4 basis-list checks + 27 read-entry checks + 27 admission checks + 27 before-publication checks = **122 checks / 244 Git processes**. A cached unchanged basis file still causes eight commands across the capture and basis layers for its one read.
5. [`GitSourceResult.PreserveAsync`](../../src/Splinter.SourceControl.Git/GitSourceResult.cs) walks the tree again. Besides the 28 availability commands measured in that phase, this retained-ref path issues 15 construction/verification commands: one `ls-tree`, six `hash-object`, four `mktree`, one `commit-tree`, one ref lookup and two graph checks. New-ref publication adds `update-ref`. The first generation additionally hashes the basis before the result; subsequent generations retain that basis manifest.

## Closure and next implementation boundary

The first target is to scope/refine availability proof reuse under an exact reader lease, fixed basis identity and appropriate revalidation boundaries. Simply remembering `available=true` would lose ref-loss/change detection. A second target is unchanged manifest/content proof reuse, preserving metadata and rename/delete/revert semantics. Release-result reuse must prove final source equivalence while retaining full seals, last shutdown writes and infrastructure removal. Retained results, conditional original publication and owned-resource fences remain mandatory. None of these optimizations was implemented or measured here.

The diagnostic helper built with zero warnings/errors. Both exact-content and exact-commit comparisons passed. Original HEAD, index, all refs and **38 current original source files** stayed byte-identical. All three private diagnostic basis refs, including one setup attempt rejected by the backing-path guard before measurement, were released; capture pins were removed. Small object stores, metadata, helper and raw evidence remain in ignored `.local/ri-git-diagnosis-20261030/` and `artifacts/ri-git-diagnosis-20261030/`.

The samples are ordered, once each, include observer overhead and use reconstructed backing. Their 19-second scan must not replace the historical 43-second CLI command or become promised savings. No Unity lifecycle rerun, optimization A/B, full regression gate, OS-crash experiment, large-input or RAM/storage reduction qualification occurred. Product source is unchanged; only diagnosis/evidence/navigation documents were added or updated.
