<a id="기존-vss-회수와-mcp-응답-복구"></a>
# Existing VSS reclamation and MCP response recovery

[한국어](../ko/plans/vss-and-mcp-remediation.md)

This document preserves approved scope/evidence for limited work performed on 2026-10-05. External Coplay patch creation/operation is excluded from current product scope, and that tool's source was removed. Fixed versions/preparation/application/settings procedures below are neither current requirements nor rerun instructions. Preserve existing external candidates/servers/installations/results and VSS reclamation facts. Follow the [current boundary](../unity-mcp-response-recovery.md).

On 2026-10-05 the user instructed resolution of two issues. Use existing cleanup approval and this instruction as execution approval. Questions: (1) can historical C: Plastic A/B be reclaimed without losing non-owned/active snapshots; (2) can long-execution responses and detailed test results after reload be preserved on exact Editor targets of the same HTTP server?

<a id="고정-범위"></a>
## Fixed scope

- Existing `T:\pc1002` A `c050da8fd660e350f0ef2756` release and B `1ac08fe356c9603d0f13bf83` discard, using existing intent/digest. Do not arbitrarily convert legacy state to current format.
- First read current existence/ownership records/dependencies for C: blocking IDs `{78AD7813-DF73-4845-8513-F14CC6E87331}`, `{15182498-4A5B-4E95-B85F-FBE2EC57AE15}`, `{A16A6070-1AAE-445F-8561-08C86B00A115}`, `{0A89C286-0E9C-43EB-87AA-4CD068E98E7E}`. Preserve other-task results/active resources. Guard removal/global deletion/VSS limit changes are not remedies.
- Administrator work inventories fixed evidence and performs verified owned reclamation. Retain installed service/original/peers. After investigation the user separately approved deletion of `{15182498-4A5B-4E95-B85F-FBE2EC57AE15}`, `{A16A6070-1AAE-445F-8561-08C86B00A115}`, `{0A89C286-0E9C-43EB-87AA-4CD068E98E7E}` by allowing removal of the three existing blocking snapshots. Delete no non-owned snapshots beyond this fixed exception. Preserve the latest DirectX restore point.
- Manage a reproducible external-tool patch at Coplay `10.2.0` fixed revision `30d22075093d1d35dfb0091c1c7550e9ad948577`. Do not directly modify `Library/PackageCache` or uv cache. Add no Unity operation API to Splinter Core/Runtime.
- Use Codex HTTP connection and exact per-call targets. Existing shared server gets only normal restart with same 8080/version; keep logs outside Library. Treat mixed Stdio/HTTP as a separate connection issue.
- Verify one long read-only code execution on existing Git original Editor, small selected tests and one explicit reload, target/current loaded patch/detailed-result equivalence/original-other Editor preservation. Create no new Splinter A/B/support matrix/Play-build. Fix necessary fixture paths/hashes and clean them.

<a id="종료-기준"></a>
## Completion criteria

1. Match latest VSS inventory and blocking IDs' ownership/lifecycle/dependencies; reclaim only eligible owned resources in oldest-first order. Observe exact A/B completion/independent A result preservation/other snapshots-peer protection. Do not claim reclamation complete if external resources block.
2. Fix long commands secretly completing after failed/null responses; obtain one command and verifiable final result from actual read-only execution over 30 seconds. Do not hide duplicate execution as timeout retries.
3. Reread detailed completed selected-test results from the same Editor under the same job ID after reload, matching whole-result hashes. Retain existing A/B namespace isolation qualification; do not expand into new parallel-Editor verification.
4. Document patch application/fixed versions/server-Editor restart/original Codex settings preservation/recovery. Increasing timeout alone does not establish unlimited execution/cancellation/duplicate safety.
5. Run changed-boundary automated tests, required `pwsh scripts/build.ps1`, UTF-8/diff checks. Raw evidence in `artifacts/vss-mcp-remediation-20261005/`; separate verification owns decisions. Exclude HTML/push/automatic service sweeps/VCS-free basis.

<a id="마감과-후속-경계"></a>
## Closure and subsequent boundaries

[VR/MR verification](../verification/vss-and-mcp-remediation.md) closes fixed scope with limited qualification. Confirmed A released/B discarded, independent A result/latest DirectX preservation, one long execution/duplicate blocking, detailed results after reload. Recorded missing env during global Codex settings change blocking conversation submission and separate user recovery; require settings-query checks going forward. After Codex restart, default binding switched to HTTP, but external Editor/server unexpectedly disappeared, so the same version/port were launched independently. Following user Connect, exact target/original PID-start/loaded patch/read-only code result/Console-idle state were confirmed through default HTTP tools, closing connection confirmation too. Cause of earlier unexpected termination remains unresolved; further application-restart survival tests are NotRun. Do not automatically expand new A/B/Play-build/general VSS deletion safety/arbitrary code recovery across Editor-OS failures.
