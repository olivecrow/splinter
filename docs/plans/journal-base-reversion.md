<a id="명시적-저널-정리의-기준본-복귀"></a>
# Basis reversion in explicit journal compaction

[한국어](../ko/plans/journal-base-reversion.md)

The following is the closed scope of the append/v2 compaction intent implementation at `25f4fa1`. [Current-state storage](latest-only-journal.md), following the user's current intent, is a separate scope using v3 intent and SJL2. Earlier figures do not qualify the current candidate.

This is the user-approved implementation and real comparison scope, starting from `development @ d97bd3c`. Only explicit `workspace compact --revert-base-ranges true` removes private ranges matching the fixed immutable basis. Preserve append/individual flush/ACK, accumulation before compaction, and no automatic compaction. Start with default OFF and change it only if all conditions below are met.

<a id="고정-비교와-판정"></a>
## Fixed comparison and decision

- Separate journals/workspaces for OFF/ON. Identical compression, limits, basis, and write order. Three runs in OFF/ON→ON/OFF→OFF/ON order. Do not clear OS cache; report first-run/subsequent warm-cache possibilities and variation.
- Synthetic: seed 61006, 4KiB/1MiB ranges, 32 B→C→base cycles each. Eight distributed ranges, partial reversion, real remaining changes, raw/random and repeated bytes. Compare all bytes, entries, paths/change sequences after normal compaction/replay.
- Real product: inspect a small registered fixture and real Medieval inputs from existing `splinter-unity`, then fix source/Library/file counts. For a large project, use an owned workspace with at least 2GiB of real assets; do not substitute a large synthetic file. Apply identical cycles to fixed 4KiB/1MiB ranges of real assets. Avoid unnecessary repeated Unity cold imports; leave unmeasured preparation time as NotRun.
- Changed boundaries: forcibly terminate actual owned children after append flush before ACK, candidate flush, prepared intent, generation publication, and before/after previous cleanup. Confirm external ACK, same identity/request resumption, and bytes/metadata/seal. Confirm B queries, 64 writes, and an independent result while A compacts/recovers in one run.
- Default ON conditions: both repeated input and large real input must reduce final journal NTFS allocation by at least 20% or total compaction/resumption time by at least 15%, with improvement exceeding twice the repeated variation. No regression above 10% in total write+compaction+resumption time. All selected recovery/peer protection checks pass. Do not select ON from one favorable sample/synthetic result or missing real verification.
- Separate logical/allocation, payload/metadata, final live, old/new overlap, basis-comparison bytes/time, managed allocations/heap, provider/system memory, write/compaction/replay, and total time. Do not convert volume free space/shared VSS/total instantaneous peak into savings.

<a id="안전성과-종료"></a>
## Safety and completion

Distinguish the source structure hash from the expected structure hash after removal. Removal is permitted only for portions read and verified inside BasePath/BaseLength of the exact snapshot, not the current original. Preserve tails truncated by resize, new/deleted-and-recreated files, names/IDs/times/attributes/hidden/change sequences. Reclaim the previous generation only after whole-candidate flush/checksum/replay, expected hash, before/after seals, and atomic publication. Fix the selection in durable intent and reject retries with different options. Preserve and reject old intent formats.

Do not bypass missing/corrupt bases, partial-read failures, or content mismatches by deletion. Preserve unsupported inputs as NotRun/unknown and specify necessary real inputs/installation preparation. Complete required build/schema/fake/contract/documentation checks and record OFF/ON evidence and the default decision in verification documentation. Exclude installation/service/grant/volume changes, power/reboot, non-owned termination, and push/merge/deploy.

<a id="이번-단위-마감"></a>
## Closure of this unit

2026-10-06 **scoped-complete-with-limitations**. Completed three OFF/ON runs each for synthetic and real recorded 2.8GB Unity project inputs, seven forced-child-termination boundaries each against synthetic and real Git bases, and real WinFsp editorless A/B with independent results and both releases. Preserved the initial A runner access denial; after explicit user approval, completed through official resumption using the same ID/home and stored ON selection. Real PNG final journal allocation was -74.76%, but write+compaction+replay was +49.41%; repeated compressed input improved allocation by 0%, failing default ON conditions. **Keep default OFF**. [Verification figures, history, and limits](../verification/journal-base-reversion.md) are authoritative. Unity cold import, WinFsp kill, power/OS/full matrix, total instantaneous space/RAM, and shared attribution remain NotRun/unknown. Do not automatically resume additional A/B runs or a default change for this input.

This implementation was committed as `25f4fa1`. The subsequent user-requested [many-file time optimization](journal-base-reversion-optimization.md) is separate; do not inherit these figures as measurements of the current optimized binary.
