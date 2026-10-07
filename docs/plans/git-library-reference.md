<a id="git-소스원본-library-참조와-mcpram-검증"></a>
# Git source, original Library references, and MCP/RAM verification

[한국어](../ko/plans/git-library-reference.md)

On 2026-10-04 the user requested that Git projects also reference original Library data, store each parallel task's changes separately, and verify actual A/B MCP connections and RAM savings. Existing cold Git A/B [verification](../verification/git-unity-validation.md) is comparison evidence, not a pass for the new warm path. Produce no HTML.

<a id="고정-요구와-발견한-경계"></a>
## Fixed requirements and observed boundaries

- Git source retains current fixed commit/tree/blob and source/target/dirty/peer guards.
- Read from the existing original Library basis without bypassing through initial full copies, read hydration, whole-file copy-up, or reintroduced VSS. Each task's Library writes use a separate range journal.
- Starting-generation Library bytes must remain stable while the original continues changing. Do not claim immutability from mtime/length, watch notifications, or hardlinks alone.
- Current WinFsp handles only writes through the mount. Direct original NTFS writes and mmap overwrites bypass the current provider. Git ignored Library has no objects to restore earlier contents.
- [Oplocks](https://learn.microsoft.com/en-us/windows/win32/fileio/types-of-opportunistic-locks) notify access/write conflicts; they do not replace implementation for preserving earlier changed-range contents.

<a id="검토-가능한-두-실행-경로"></a>
## Two reviewable execution paths

<a id="a-원본-library만-관리형-mount로-연결"></a>
### A: Connect only the original Library through a managed mount

The user approved this path by **allowing a managed mount for Library only**. Product/source project root, Assets, Packages, and ProjectSettings stay at the same NTFS locations. Original Library access remains `<project>/Library`, but WinFsp provides that directory. Only this Library filesystem/physical-layout change is an exception to existing requirements.

1. Verify exact original Editor/PID/start time/dirty scenes/MCP/server ownership. Initial connection requires saving and normal original Editor shutdown. Do not automatically discard dirty changes.
2. **Rename** original Library to a fixed base directory under an owned home on the same NTFS volume. Preserve initial bytes without a full data copy. Example test home is `.local/glr1004/home`, base `library-bases/<id>/base`. Do not move original source paths.
3. Attach a separate writable overlay at the original Library path. Original Editor writes ranges through this path, so it cannot overwrite the A/B fixed base. A/B use the same immutable Library basis plus respective private deltas. Keep every owner/basis/compatibility key and dependency in durable receipts.
4. First verify initial move/mount, interruption resumption, identical requests, and basis-reader protection with fakes/small real folders. Perform the user's first original transition only after this reviewed implementation is ready.
5. After final peer reclamation, apply the original's final changed ranges to the basis and restore ordinary NTFS Library. Preserve/reject restoration with other-task/original changes or incomplete publication intent. Do not restore through new full Library materialization/whole-file copy-up.

This path introduces a new lifecycle making original Library a writable managed path. Do not implement it as mutable-source fallback in existing `git-snapshot` or by hiding the original mount. Do not treat initial normal original Editor shutdown and cache-location/filesystem changes as existing read authorization.

<a id="b-원본-library의-ntfs-위치파일시스템을-완전히-유지"></a>
### B: Fully retain original Library NTFS location/filesystem

A separate filesystem filter must preserve needed previous ranges/metadata before original NTFS writes, paging I/O, truncate, rename/delete. WDK, driver signing/installation, paging I/O, and power/interruption preservation are not part of connecting existing normal-user WinFsp code. First establish development/installation prerequisites and bounded tests; do not automatically install a driver on the current PC.

<a id="구현과-검증의-종료-기준"></a>
## Implementation and verification completion criteria

After path selection, connect common immutable generated-data basis/composed tree/per-task range store/owner/dependency/release/discard. Update current schema/fake/conformance, capabilities/feature documentation/AGENTS together; do not automatically migrate legacy state.

| ID | Question | Completion evidence |
| --- | --- | --- |
| GL-01 | Safely reference original Library basis without copying? | Exact Git commit+Library generation identity, existing A/B bytes retained during original subsequent range writes, zero payload copying/read hydration, separate private writes |
| GL-02 | Protect original/peer and basis lifetimes? | Same-request recovery, basis reader/dependent guards, B continuing during A reclamation, final reclamation and original Library restoration or filter detachment, independent results/original refs retained |
| GL-03 | Exact MCP target for each Editor after reload? | Actual project/PID/start per call, prior refresh/reload completion and rediscovery, lost-job checking, no unconditional write repetition, full results externally saved |
| GL-04 | Actual RAM costs reduced? | Same source/Unity/package/scene/task comparison, Editor/provider/reader per idle/work interval, private/shared residency/PSS estimates, process identity fencing. Report no savings/unknown unchanged |
| GL-05 | Final results/owned resources preserved/reclaimed? | Independent results, absence of exact A/B editors/providers/mounts/registrations/deltas/basis refs, original Editor/shared MCP/existing refs/config protected, required build/UTF-8 |

Distinguish Private Bytes, working set, shared-page attribution, and total instantaneous peak in RAM measurement. Library references alone do not establish shared Unity managed heap/scene/graphics memory. Prepare new read-only process measurements without original writes/stops. Actual A/B and original Library transition follow the selected path's contract/recovery implementation.

<a id="종료-판정"></a>
## Completion decision

Approved path A implementation, GL-01–05, and one actual Git/Unity A/B closed as scoped-complete-with-limitations in [verification](../verification/git-library-reference.md). Confirmed zero initial payload copying/read cache, common basis/per-task range writes, exact MCP/reload/resources/scenes/current-state tests, independent results/owned reclamation, original NTFS restoration/reopening. Actual RAM resembled cold runs, so savings were unconfirmed; preparation/restoration costs, lost MCP failure details, and unknown total peak/shared attribution remain for future decisions. Do not expand this result to general RAM savings or additional VCS/environment support.
