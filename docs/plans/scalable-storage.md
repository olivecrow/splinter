# Scalable source storage and repeated publication

[한국어](../ko/plans/scalable-storage.md) · [Implementation](../scalable-storage.md) · [Verification](../verification/scalable-storage.md)

Status: **in progress**, authorized by the user's implementation request. SCAL-01–08 own this program; earlier closed performance programs are not reopened. Baseline: development `b68eaaa9e7e78930ff7b4b0b368f137bc3e528c6`.

The user reduced verification on 2026-11-01. Large synthetic runs were stopped safely with retained state; remaining 100k/17GiB/ABBA/full-gate obligations are open, not passed. [The slowdown record](../verification/scalable-storage-throughput-diagnosis.md) separates observed phase times, confirmed fixes and unmeasured causes. Do not restart those large runs automatically. Implementation activation and focused qualification do not establish the full completion criteria below.

The remaining serialization/traversal candidates are implemented as [ST-36–40](../verification/scalable-storage-throughput-followup.md#st-3640-remaining-serialization-and-traversal-costs): grouped durable progress, initial namespace/Create batches, bounded exact graph caches and planning merges. Small 64-test qualification does not close the large/full gates or replace the one-key-per-node format.

## Accepted contract

Remove project-wide entry/path/manifest/journal quotas throughout Git/None/Plastic preparation, editing, capture, original publication and reclamation. Keep bounded processing pages, lazy caches and concurrency; real filesystem capacity and metadata restrictions still apply. Preserve immutable independent generations, same-request recovery, original/peer isolation and all-target native publication locks. Never substitute a mutable original path for required preserved content.

Bulk metadata uses directory/component records, immutable binary pages no larger than 64KiB and shared append containers. Unchanged pages and content are reused. The 2026-11-01 resource authorization replaces the initial 32MiB cache with a demand-driven process-local target up to 4GiB: five eighths for metadata cache and one eighth for pooled sort/spool scratch. This is not preallocation, an OS RSS cap or an aggregate multi-process guarantee. Bounded sorting/indices retain disk fallback instead of project-sized unbounded arrays/dictionaries; the [implementation](../scalable-storage.md) owns the exact policy. Small request/receipt/CLI objects remain JSON. Do not add a DB, daemon or serialization dependency.

All original publication targets are checked/pinned before mutation. The resource policy selects 8–32 workers for independent files, with topology/companion dependencies, serialized durable progress and failure/cancellation drain. Beyond one largest payload for recovery, parallel staging is limited by worker count, one quarter of the working target and one quarter of observed free disk space; per-file space checks remain. Native handle costs remain proportional to the pinned target set.

## Delivery sequence

1. Common contracts/fakes, immutable page store, directory metadata, interval indices, corruption and interruption tests.
2. Paged Git/Plastic/input/Library metadata and live node/extent/hidden/change indices; frozen capture roots and durable reader protection.
3. Result/request/apply history indices, content reuse, independent results and precise reference-based reclamation.
4. Full-lock bounded parallel publication, cursor CLI/schema and actual backing-capacity reporting. Remove `--max-journal-bytes` and connected quotas only after their replacement paths are ready.
5. Native/Unity qualification, matched small-input cost comparison, bilingual docs and the final full gate.

The candidate now writes task v4, managed basis v3 and product overlay identity v3. Preserve/refuse older tasks/bases/overlays and use their matching binaries; no migration, fallback or reclamation is added. Activation in candidate code is not SCAL completion: actual lifecycle, fault, cost, documentation and final-gate evidence remain mandatory. Explicit development views/candidates retain their separate recorded formats.

## Candidate connection and remaining qualification

Current code connects component/page provider catalogs, segmented live/capture indices, independent shared content/ranges, paged history/plans/selection and full-lock bounded workers. It also exposes root-bound `workspace read` and exact-key/scope `workspace maintain`; metadata collection uses authoritative roots/reader fences and native ownership receipts. Capture/apply maintenance scheduling is a processing decision, while final release forces collection. Explicit development overlay2/candidate3 diagnostics are separate from product overlay3.

Finish current live-index/backing reclamation recovery, the prescribed native/Unity input set, cost/regression measurements, final source-map hashes and build/docs gates. Report each gate from new candidate evidence; no skip or earlier pass substitutes for a mandatory case. Unknown protected residue is explicit data, not an automatic deletion target. See the verification owner for current results and unresolved rows.

## Required gates

| ID | Required result |
| --- | --- |
| SCAL-01 | Reproducible logical identities; reject corrupt/truncated/invalid/cyclic metadata; preserve/refuse old state. |
| SCAL-02 | Stream hierarchical/flat/long-path inputs of 1,024 / 32,769 / 262,145 / 1,048,576 entries through enumeration, lookup, comparison and reopening. |
| SCAL-03 | Exceed previous node/extent/hidden/path/JSON/journal limits without another total-input quota; distinguish modeled large offsets from actual large files. |
| SCAL-04 | Fixed capture under concurrent write/rename/delete/truncate/rotation; frozen retries, subsequent edits and selected publication. |
| SCAL-05 | Original edits after publication cannot alter a workspace or old result; share duplicate content and reclaim only unreferenced owned data. |
| SCAL-06 | Interrupt root/payload/publication/history/reclamation boundaries and inject storage exhaustion; recover the same request without mixed/lost results or duplicate publication. |
| SCAL-07 | Actual editorless 100,000-entry Git/None/Plastic lifecycle; separate small original/A/B Unity runs with two intermediate and one final A publication, preserving original/B Editors. |
| SCAL-08 | Before/after/after/before small-input costs, actual allocation/residuals, metadata/private memory and handles; fix sustained material regressions; final `pwsh ./scripts/build.ps1` and docs checks. |

No beta publication, Unity package, other OS/app support or general performance claim is included. Connected quotas or outstanding required qualification prevent overall completion.
