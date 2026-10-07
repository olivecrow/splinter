<a id="확장-계약-검증"></a>
# Extension-contract verification

[한국어](../ko/verification/extensibility-boundaries.md)

Inputs are extension-boundary changes on `ef97330` and repository automated fixtures. This scope does not implement actual other OSes/apps or run new Unity/VSS/service/product A/B. The current contract follows [OS and application extension boundaries](../extensibility-boundaries.md).

| Boundary | Evidence | Qualification |
| --- | --- | --- |
| Path policy | Independent paths, move, hide, replay, candidates, and rejection of policy mismatch in a case-sensitive fake tree | Automated conformance; actual POSIX mount NotRun |
| Permission metadata | Git executable-file mode, journal metadata, candidate roundtrip | Automated conformance; actual Unix ACL/owner/publication NotRun |
| Application selection/original protection | Injected fake policy's separate companion-file guard, denial of original access, lease reclamation, rejection of unregistered policy | Fake policy + ordinary-permission Windows file-publication tests; other apps NotRun |
| Existing Unity behavior | Existing Plastic original-apply tests for `.meta`, UnityLockfile, fixed intent, registration/lock protection, owned cleanup of temporary reservation | Automated file regression; new Unity Editor execution NotRun |
| Generated directories | Injected Cache descriptor, fixed application/kind, preservation/rejection of v1, existing prepare/restore regressions | Automated file conformance; new native mount/A/B NotRun |

On 2026-10-05, `pwsh ./scripts/build.ps1` completed locked restore and Release build with 0 warnings/errors. Full regression was **1,052 tests: 1,042 passed, 0 failed, 10 conditionally skipped**, taking 4 minutes 44 seconds. TRX is `artifacts/test-results/Splinter.Tests_net10.0_x64.trx`. Skips reflect conditions for backend/external-run/child-only entry points at that time; they are not marked as new support passes.

Focused checks covered 56 filesystem/Git/candidate tests, 34 generated-directory/project/restore tests, 21 application-policy/Plastic original-apply tests, and 59 CLI contracts. These sets overlap the full regression and are not summed. Final full regression also includes added invalid-projection-path, corrupted-permission-metadata cases, and the response capabilities schema.

Document UTF-8 checks and `git diff --check` passed. Existing verification's native qualification is not inherited as an actual execution pass for this candidate.
