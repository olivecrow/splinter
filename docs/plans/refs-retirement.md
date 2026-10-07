<a id="refs-기준본-경로-폐기"></a>
# Retirement of the ReFS basis path

[한국어](../ko/plans/refs-retirement.md)

Status is **retired**. Do not retain it as an optional development path, support-expansion candidate, or fallback for NTFS failures.

Removed contracts and existing-path regressions follow [removal verification](../verification/refs-retirement.md).

ReFS block cloning required separately preparing a full source and Library basis before creating workspaces. For large projects, the initial full basis's storage and preparation time themselves impose substantial costs, conflicting with storage-first product requirements. Current snapshot-basis paths combine an immutable basis with per-task changed ranges without cloning the whole original, eliminating that preparation stage.

Retired scope includes the `refs-cow` storage provider/common cloning/basis contracts, Runtime basis prepare/seal/delete, Git basis cloning/receipts, development CLI `baseline prepare/seal/release`, `storage probe`, `--baseline`, related schema/fake/actual ReFS tests, and Dev Drive preparation scripts. Neither default nor development CLI provides this feature.

Current workspace state remains v2. Preserve request digests and resumption/preservation/reclamation contracts for existing snapshot tasks; preserve and reject retired storage modes or basis-preparation states. Do not automatically migrate or delete historical ReFS state/data into snapshots.

Preserve the [limited ReFS execution record from that time](../verification/storage-and-resources.md) only as evidence for historical inputs. It does not approve deleting existing TRX/raw artifacts/independent results/snapshots, and is neither a current feature nor next development task. Current storage paths follow [NTFS workspaces](../ntfs-workspaces.md), [Git basis](../git-project-basis.md), [Plastic basis](../plastic-project-basis.md), and [VCS-free managed basis](../plain-project-basis.md).
