# Retiring the VSS storage path

[한국어](../ko/plans/vss-retirement.md)

Decision: remove `ntfs-snapshot` and all Splinter-owned VSS execution paths. The remaining product modes are `git-snapshot`, `plastic-snapshot`, and `project-snapshot`. VSS is neither a development option nor a fallback. This decision does not remove common immutable snapshot contracts, NTFS/WinFsp range storage, shared Library bases, independent results, or ownership and recovery guards.

## Why project-scoped bases replaced VSS

The user's 2026-10-04 decision identified a scope mismatch: work concerns one project, while VSS captures its entire volume, such as C:. The [project-scoped basis plan](project-scoped-basis-and-reclamation-review.md) therefore chose existing Git/Plastic versions first. Changes outside the project and volume-wide retention are outside a task's useful storage scope. A project path inside a volume snapshot does not make the snapshot project-scoped.

There is also a separate reclamation limitation. The [2026-09-26 isolated reproduction](../verification/storage-recovery.md#vss-cascading-deletion) observed loss of an older, non-target snapshot after deleting a later snapshot, including through the direct VSS API with `bForceDelete=false`. The protective implementation refused deletion while older, equal-age, or unknown-age snapshots existed. This preserved other snapshots in the defined protection test, but could not guarantee both arbitrary task completion order and immediate independent physical reclamation. It never authorized deleting another task's or backup program's snapshots to unblock a task. The exact driver thresholds remain unconfirmed; this is evidence from the recorded environment, not a universal claim about all VSS providers.

Retirement is based on this project/volume mismatch and reclamation constraint. It is **not** a claim that a VSS speed benchmark failed a threshold. ReFS retirement at `d97bd3c` is a different decision, caused by the initial full-basis preparation burden for large projects; see [its decision](refs-retirement.md).

## Retained architecture

| Mode | Fixed project basis | Existing implementation milestone |
| --- | --- | --- |
| `git-snapshot` | Local commit/tree/blob, owned reachability reference and reader fence | `d401677`, 2026-10-04 |
| `plastic-snapshot` | Recorded changeset and raw file revisions, ordinary-user reader | `7d90b3a`, 2026-10-05 |
| `project-snapshot` | Explicit managed VCS-free project basis; same-volume moves and writable original view | `3781769`, 2026-10-05 |

These paths require no VSS service. Git/Plastic may reference a separately prepared Library; the whole-project managed basis includes its generated data. Source and Library preparation/restoration keep their exact identity, dependency, normal original-Editor shutdown, and explicit restore requirements. No initial whole-project copy, read hydration, whole-file copy-up fallback, or mutable-source fallback is introduced.

## Removal and preservation boundary

- Remove VSS providers/readers, capture/observe/release PowerShell, StorageAuthority service/SCM/IPC/client and service-specific guards, install/package/grant/status entry points, service schemas/capabilities, and VSS-only test/harness code, including volume-budget and snapshot-loss helpers. No service-specific package dependency is needed by the remaining implementation.
- Keep common path/identity guards, atomic publication, read leases, individual durable ACKs, seals, independent results, original/peer/dirty/target protection, and same-version interruption recovery for supported modes. Development candidate/cache readers use common basis composition; their native qualification is separate. The development Git view rejects a Library graft it cannot represent; ordinary task Library sharing remains supported.
- Reject `ntfs-snapshot` creation and durable state. Runtime reports `StorageModeRetired`; current request schemas reject the mode. A legacy VSS overlay is rejected with `SnapshotProviderUnsupported` before provider launch/mount. No automatic migration, new capture, result rewriting, release, discard, or ledger cleanup occurs.
- Preserve old workspace state, independent results, protected ledgers, raw evidence, reports and VHDX. Historical verification stays a record of its original candidate/input. Old service/CLI examples in historical documents are not current instructions.
- Do not alter Windows VSS, system restore points, backup software, installed service configuration, or snapshots. Machine cleanup is outside this source change.

The starting checkout was clean at `2201579`. That commit already contained the prior request-scoped StorageAuthority path-pin fix and lifetime tests, including the UA-05 documentation. Its service-only implementation is retired here; common WinFsp/journal/managed-basis path defense remains. The cancelled service reinstallation and new live VSS verification are not resumed, and `artifacts/storage-authority-idle-20261023/` is not operated or modified.

## Completion evidence

[Retirement verification](../verification/vss-retirement.md) records the required build, retained regression coverage, retired-entry-point checks and documentation/UTF-8 checks. No new VSS, Unity, large-project, reboot/power-loss, or physical-space/RAM qualification is implied. Prior bounded results remain in their owning verification documents.
