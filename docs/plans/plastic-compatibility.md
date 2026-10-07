<a id="unity-vcs-사용-환경의-splinter-병렬-작업"></a>
# Splinter parallel work in Unity VCS environments

[한국어](../ko/plans/plastic-compatibility.md)

On 2026-10-02 the user requested execution of the next development unit. The goal is **Splinter itself providing parallel work regardless of external VCS, while Unity VCS/Plastic SCM management data/registrations/locks do not obstruct original/peer protection or storage/memory/parallel work**. VCS feature expansion or successful check-in is not completion criteria.

<a id="이번-범위"></a>
## This scope

First input is a small Unity project on Windows x64/local NTFS, installed Plastic CLI `11.0.16.9915`, Unity `6000.3.22f1`. Preserve `splinter-unity` as existing Git fixture. The user prepared `C:\Users\olive\Desktop\Development\splinter-unity-plastic`; exact root/GUID `f3e87fff-e66c-4820-aec7-ebe2a633962f`/regular-static registration were verified read-only. Do not use other existing registered projects as verification inputs.

- Fix explicit Plastic environment selection in request/state/digest. No fallback to plain folders or internal Git initialization. Preserve current state v2 and existing Git/none guards.
- Starting with common contracts/fakes, connect creation/same-task resumption/independent editing/owned Editors/stable checkpoints/independent results/release/explicit discard. Put no Plastic-specific API in Core.
- Prevent A/B sharing original `.plastic` management data/registrations/locks. First candidate verifies original VCS registration read-only and connects unregistered file workspaces using immutable source excluding management regions plus private deltas. Block management-root reading/enumeration/creation/rename and reject inherited VCS/overlapping Plastic registrations for home. Verify through actual client/Unity project-discovery behavior. Do not bypass through original/global settings/server/authentication/existing-registration changes.
- Add no initial full checkout/copy/read hydration/whole-file copy-up. Distinguish creation/read payload costs from explicit independent-result preservation; observe extra management data/indexes/deltas/results/temporary allocation/owned working set. Shared VSS per-task attribution remains unknown.
- Update input/output schemas, provider/fake conformance, CLI/Host/Windows privilege boundaries, capabilities, AI guidance, feature documentation, and verification matrix together.

Exclude original application, Plastic update/checkout/check-in/branch/merge automation, Gluon/partial/dynamic workspaces, mixed/nested VCS, full large/ARM/OS failure matrices, and development view expansion. Explicitly reject original application before side effects; do not inherit existing plain-folder AP qualification for Unity/Plastic. Editor compile/Play/Test/build and full tool-handoff qualification are separate AI/external-tool verification.

<a id="실제-실행의-질문과-종료-기준"></a>
## Actual execution question and completion criteria

The question is **whether Splinter A/B work independently on a small Plastic-managed project without whole-original copies, while the original stays editable and A result/reclamation and B continuing work/discard finish safely**. Create no new failure harness/full matrix.

1. Verify read-only client version/original workspace path-GUID-type/Unity patch/original content-management regions/existing registrations-running Editors/project-home/existing service grants-ledger-snapshots/free space. Plastic account/repository creation/check-in is user preparation.
2. After automated conformance for current changed contracts, execute one actual A/B run. Confirm returned A/B paths/distinct keys-IDs/immutable bases/private deltas/exact owned Editors/same-request resumption. Preserve subsequent original changes and peers.
3. Confirm original Plastic management regions cannot be read/changed from workspaces, and workspace discovery does not select existing original/other Plastic workspaces. If candidate creates no client registrations/locks/shared caches/full payload copies, verify their absence. Do not promote assumptions or fixture-only success to actual qualification.
4. After A normal stop/checkpoint/release, verify actual independent-result bytes and B continuing editing/Editor. Explicitly discard failed or dirty B with exact intent. Verify result seal/mount-provider-delta/owned snapshot-resource reclamation and original-peer-existing VCS registration protection.
5. Pass existing Git/none/AP automated regressions and required build; record first-candidate costs/success-failure/retries/limits. If input/service changes are needed, plan exact packages/paths/retained registrations-protected records/writes before administrator Apply. Fix original/peer damage or acknowledged-write loss on supported paths; state remaining unqualified boundaries.

Connected product CLI/automated conformance and executed A/B under the fixed [actual C: plan](plastic-validation.md). Existing C: non-owned VSS protection leaves snapshot reclamation for A release/B discard Partial, preserving task records/snapshots. After the user's next execution request, one [separate T: plan](plastic-reclamation-validation.md) run confirmed creation/management regions/same requests/Editors/independent results/B continuing/A release-B discard/final snapshot reclamation, closing this unit **scoped-complete-with-limitations**. The [verification record](../verification/plastic-compatibility.md) separates decisions/costs for each input. Do not claim resolution of C: pending states; external Unity CLI A/B discovery failure remains unqualified. Full MCP/CLI handoff and original application are separate follow-ups.
