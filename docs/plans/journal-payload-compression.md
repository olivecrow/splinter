<a id="변경-범위-압축의-제품-연결-범위"></a>
# Product integration scope for changed-range compression

[한국어](../ko/plans/journal-payload-compression.md)

The question is **whether connecting the standalone codec to the actual range journal reduces NTFS journal allocation while preserving content, acknowledged writes, resumption, independent results, and original/peer protection**. This is the next unit in the [storage-first plan](storage-priority-and-editor-modes.md#range-payload-codec-and-next-product-integration), pursued to completion at the user's request. Do not inherit standalone codec sample compression ratios as product savings.

JP-01–06 closed as scoped-complete-with-limitations according to [direct execution, costs, and limits](../verification/journal-payload-compression.md). The following fixes scope and completion criteria for this unit; it does not instruct additional A/B runs or reopening all SF/NR.

<a id="포함과-종료-조건"></a>
## Inclusion and completion criteria

| ID | Inclusion | Completion evidence |
| --- | --- | --- |
| JP-01 | Common stored/decoded length and logical fragment contracts, raw selection, bounded codec integration | Core contracts, literal/fake boundaries, Runtime partial overlap/resize/rename/delete/identical private writes. Compressed and raw both retain one frame per callback, individual durable flush/ACK |
| JP-02 | Current journal write/read/replay, compaction/candidate and budgets | Reject current-frame checksum/truncation/corruption/decoded-length failures; stable content/sequence/seal, same-intent/candidate publication recovery, compaction overlap budgets. Compare physical sizes including 48B compression and external checksum/metadata costs |
| JP-03 | Current single format and discovery/schema | overlay v2, SJR2 frames, candidate v3. Preserve/reject earlier identity/journal/candidate; add no automatic conversion/old-version reader. Keep workspace state v2 and VSS/protocol |
| JP-04 | One small real A/B pair | Existing T: Plastic original/home creation, same-request resumption, independently owned Editors, normal stop, result preservation, B continuing during A release, B subsequent changes/release, final owned snapshot reclamation. Preserve original/management data/registrations/existing results |
| JP-05 | Actual space and memory costs | Compare NTFS file allocation, payload/metadata, live/replaced ranges for current raw and product-compressed frames with identical metadata/logical callback inputs. Separate extra buffers, managed allocations/owned-process observations, and failure costs; unmeasured total peak/shared VSS attribution/native codec RAM remain unknown |
| JP-06 | Regression and documentation | Required locked restore/Release/automated tests, existing interruption tests at necessary changed boundaries, schema/UTF-8/diff and current feature/verification matrix synchronization |

Real input is existing `T:\plastic-fixture-20261002`, home `T:\pt1002`, `--source plastic --application unity --base snapshot --storage ntfs-snapshot`, Unity `6000.3.22f1`, existing warm Library/Unity CLI/Pipeline. Keys are `jp-t-a-20261003` and `jp-t-b-20261003`; journal budget per task is 17,179,869,184B. Do not bypass failures with fresh keys; preserve exact requests, binary hashes, basis/digest/owned PIDs in the execution manifest. Edit only returned paths.

Before execution, check read-only service/doctor, original/management-root hashes, Plastic registrations, existing results/records, original Editor identity, and space. The service already runs; use the existing grant. If installation/service/grant/volume changes are necessary, separate a concrete preparation proposal. Do not elevate normal CLI/Git/Unity or bypass through automatic helpers.

Write only different small source markers for each worker and bounded private inputs under `Library/SplinterPayloadQualification`. Originals of the four Bee file samples are read-only; distinguish them from initial whole-project copying/read hydration/whole-file copy-up. Record actual Editor Library writes and fixed file-client writes separately. Raw comparison journals are bounded measurement artifacts with identical frame metadata/decoded payload, not published workspaces/results. Record explicit scope of compressed journals and raw controls, accounting for measurement artifacts separately.

Exclude a new full harness/failure matrix, additional A/B, new source/home, original integrate/check-in, MCP/package updates, Play/Test/build, large/ARM/OS failures, flush/group commit, new backends, automatic modes/queues. Keep existing C: reclaim-pending/discard-reclaim-pending and non-owned blockers unchanged. Do not weaken reclamation gates or delete other snapshots because the result is small.

Close only this integration unit when direct evidence exists for all JP-01–06. Preserve failed evidence and tasks and fix the relevant boundary if space grows or content/recovery/reclamation fails. Do not inherit earlier native/M0/UH runs as passes for the current journal format.
