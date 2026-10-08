# Startup profile follow-up optimization

[한국어](../ko/verification/startup-profile-optimization.md)

**SU-01–05** implements the user-mode findings from [UP-01](unity-startup-profile.md). UP measured 766.9024 seconds to initial readiness, including approximately 447 seconds of scanning; it is the previous candidate's evidence. The new candidate's full Unity A/B preparation time remains **NotRun**. No UAC, installed-service update, external-package change, or original-project modification is needed for these changes.

## Independent Git decoder progress — SU-01

The Git reader leases at most two independent decoder sessions. The shared gate protects proof admission, LRU ownership and final publication; decoding and hashing run outside it. Aliases of the same blob serialize. Active reads pin their proofs and count new proof reservations before allocation, retaining the 4MiB/4,096-entry proof budget and 256KiB total payload window. A third blob waits when both sessions are active. Whole-blob and requested-chunk hashes, fixed identity, no-progress timeout and staged output remain mandatory.

A failed reader prevents later publication by another active reader, drains both sessions, then clears their proofs and processes. Concurrent close callers reject new reads and wait for in-flight work. Each admitted request stages at most 1MiB; staging returns before its lease admits a replacement, and waiting callers allocate none. The exact known blob header is read in bounded spans instead of byte-by-byte timer/pipe calls; short/missing/malformed headers still fail without waiting for a nonexistent suffix. Two-chunk retention and backward-read decoding remain unchanged. There is no disk payload cache or hydration.

The first integrated suite exposed a shutdown hang in the new two-reader/two-Dispose regression. A short trace and a separate plain-thread reproduction showed the failing reader retaining the bookkeeping monitor after its shutdown exception. Exception classification now runs inside the catch body after nested-lock unwinding, rather than in an outer filter. The focused suite checks monitor ownership after both shutdown and unexpected completion exceptions. The interrupted full run and diagnostic evidence remain failures, not passes.

## Reuse pinned journal ancestry — SU-02

Product WinFsp journals inject a portable `IJournalDirectoryGuard` whose Windows implementation holds ordinary-directory handles from the volume root through the exact SJL4 object directory. Handles deny write/delete sharing, preventing ancestor replacement and reparse conversion. Subsequent object accesses inspect the leaf and owned namespace rather than walking every ancestor again. Ordinary child file writes remain possible.

The proof is tied to one object-directory generation. Constructor failures and close release it only after active reads/writes drain. Foreign paths, preexisting junctions, injected pending-file junctions and corrupt state are preserved and rejected. Unguarded, offline and sealing paths retain full ancestry checks. Format, individual durable flush/ACK, publication, recovery and composite seals are unchanged.

## Avoid no-op Cleanup serialization — SU-03

Non-deleting WinFsp Cleanup only decrements its handle count once and no longer enters the mutation gate. A retained file context remains live until Close. Deleting Cleanup and Close still serialize with mutations; unlink reclamation waits for the final retained context. Concurrent regression cases hold a write before its durable effect, demonstrate non-delete progress, and verify that delete/Close wait and a second retained context can still read the unlinked file.

## Isolate the owned bridge assembly — SU-04

`Assets/__Splinter__/Editor/Splinter.Workspace.Editor.asmdef` places the bridge in an Editor-only assembly with `autoReferenced: false`. The current infrastructure marker is **version 2**, binding both C# and assembly-definition hashes to the workspace owner. Identical resources and their Unity-created `.meta` files/timestamps are reused after complete validation; only changed, verified owned resources are rewritten. Missing/older markers, modified definitions, unexpected descendants and reparse paths are preserved and refused, without migration.

A tiny ordinary NTFS project was actually opened in Unity **6000.3.22f1**, normally closed, then reopened at the same path with the bridge added. Its runtime and predefined Editor assembly hashes remained identical. The only Csc action in the second log compiled `Splinter.Workspace.Editor.dll`; the bridge loaded from that assembly with one source file, neither predefined assembly referenced it, and the fixture observed a matching ready heartbeat before requesting normal shutdown. The second log's CompileScripts was **1.181846s**. Cold initial setup and a warm second start are different inputs, so their total times do not establish a startup speedup. This proves this small assembly/lifetime boundary, not the large A/B workload, Play/Test/build or all package post-processing.

## Content-proven source timestamps — SU-05

New Unity Library preparation at an ordinary Git root captures a fixed commit/tree catalog. An eligible file is read and hashed under the same native handle that supplies its timestamps, with write/delete sharing denied and ordinary ancestors pinned. Only an exact raw blob hash admits metadata. CRLF-different, changed, missing or busy files receive no reused timestamp. All source handles close after capture; the original remains mutable afterward. Linked `.git`-file roots do not select this optimization, and incomplete/nonlocal ordinary object stores fail closed before any possible lazy fetch.

The atomically published, bounded `source-metadata.json` v1 includes a required canonical `contentHash`. Its full-file digest and complete Library identity enter the generated-directory reference and Git receipt/snapshot identity. Retry after an interrupted pre-state publication checks the existing intent, paired Library manifest/proof and map integrity before reusing frozen bytes. Damaged/foreign/missing pairs are preserved and rejected. This is corruption/ownership checking, not authentication against malicious filesystem writers.

Opening a reader validates the fixed map once; it never consults mutable source files and its hot read-health callback does not reread the map. Only identical path/blob/length entries use captured times, including unchanged files in a later commit; unmatched entries and directories keep ordinary capture time. Existing bases without this map are not backfilled. Payloads still come from raw Git objects. This costs one source-content pass at initial Library preparation; it does not copy data, add a payload cache, weaken byte verification or substitute Bee caches/captured ILPP endpoints/external-tool state.

## Validation and costs

The final `pwsh -NoProfile -File scripts/build.ps1` completed locked restore and Release build with **0 warnings/errors**, then **1,362 passed / 0 failed / 20 conditional skips** in 258.697s of test execution. The Git concurrency class also passed all 13 focused cases, and the opt-in journal cost case subsequently passed separately. The conditional skips do not qualify their external/native inputs. No implementation changes followed the final build.

Measurements ran alone after the full suite. Each comparison used two samples per mode in balanced before/after/after/before order; these small, warm samples are bounded cost evidence, not statistical or whole-Unity speedup claims.

| Measured operation | Baseline mean | Optimized mean | Reduction |
| --- | ---: | ---: | ---: |
| Two packed 8MiB Git blobs, overlapping 4KiB requests | 0.268366s | 0.149933s | 44.13% |
| 512 × 4KiB journal appends with 32 peer files | 3.847348s | 2.953998s | 23.22% |

The Git comparison loads the frozen UP runtime and final runtime against the same packed tree. Its timer includes reader barriers, decoding and range publication, but excludes fixture setup, final hash checking and close. Every run reassembled identical bytes, decoded 32MiB for 16MiB of requests, started two readers, retained at most 256KiB of payload and 8KiB of proof, and reported zero owned readers after close.

The journal comparison uses the same final binary with full ancestry walks or pinned ancestry, each in a fresh deep NTFS fixture. It times only the append loop. All four runs retained exactly **512 durable flushes**, identical logical appended/journal byte counts, correct content for the growing file and all 32 peers, and the same seal after reconnect within that fixture. This is not a previous-release comparison, physical-I/O reduction, or a reason to omit a flush.

Read-only timestamp capture against the actual original Unity source examined **1,602 catalog entries / 2,804,673,377 bytes** in **5.489146s**, including map validation; the catalog/content-read portion was 5.438930s. **1,568 files, including all seven C# files**, matched their raw Git blob. The diagnostic map with a synthetic Library tuple was **397,543 bytes**. This single warm sample measures added one-time preparation work. It did not prepare/move/mount the Library, publish state or run an Editor, and does not measure the later scan savings.

The small actual Unity bridge run qualifies SU-04 only. Full Unity A/B preparation, checkpoint/release and final restoration with all SU changes remain **NotRun**; UP-01's 766.9024s is not a current result. The original Unity tree remained clean, 27 unrelated dirty files retained their exact pre-task hashes, and no Git staging, UAC, service or external-package change occurred.

[Machine-readable results](../../reports/benchmarks/startup-profile-optimization-20261008.json) include input/runtime hashes, individual samples and validation boundaries. Raw logs, harnesses and the failed-run diagnosis remain in ignored `artifacts/startup-read-followup-20261008/`.
