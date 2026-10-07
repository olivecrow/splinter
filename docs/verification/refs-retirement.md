<a id="refs-제거-검증"></a>
# ReFS removal verification

[한국어](../ko/verification/refs-retirement.md)

Scope covers code, command, schema, and documentation removal under the [ReFS retirement decision](../plans/refs-retirement.md), plus automated regression of existing snapshot contracts. Historical SR runs for the retired feature do not qualify current functionality.

Removal was closed in the `development` working tree on 2026-10-06. `pwsh ./scripts/build.ps1` completed locked restore and Release build with 0 warnings/errors; the full suite returned **1,058 Passed, 0 Failed, 9 Skipped out of 1,067**. Nine skips are conditions for external-run/child-only tests; retired ReFS tests are no longer included. The [full build log](../../artifacts/refs-retirement-20261006/build-full.log) and [fixed TRX](../../artifacts/refs-retirement-20261006/full.trx) are preserved.

The Release rebuild including the final CLI help wording had 0 warnings/errors; CLI contracts returned **64 Passed, 0 Failed, 0 Skipped**. The [final build](../../artifacts/refs-retirement-20261006/build-final.log), [CLI log](../../artifacts/refs-retirement-20261006/cli.log), and [CLI TRX](../../artifacts/refs-retirement-20261006/cli.trx) are preserved. Actual [default capabilities](../../artifacts/refs-retirement-20261006/product-capabilities.json) and [development capabilities](../../artifacts/refs-retirement-20261006/development-capabilities.json) also exclude retired storage modes and commands. The [final source/evidence manifest](../../artifacts/refs-retirement-20261006/manifest.json) connects the baseline commit with hashes of changed/deleted files and validation results.

| Boundary | Checked behavior |
| --- | --- |
| Feature discovery/execution | Both default/development CLI reject `refs-cow` before source/state access. Development CLI `baseline prepare/seal/release` and `storage probe` removed; absent from schema discovery |
| Storage provider | ReFS Win32 block cloning, full-basis lifecycle, Git clone receipts, common cloning contract, Dev Drive preparation, and optional ReFS tests removed. No related implementation references remain in current src/tools/scripts/contracts |
| Preservation/resumption | Retired storage mode, preparation purpose, and removed fields retaining values are preserved then rejected. Empty historical fields in current v2 are read only as metadata without results; resume uses the same request digest/workspace ID |
| Existing functionality | Full contract/fake, Git, and Windows regressions; existing snapshot, dirty/target/peer, independent-result, release/discard protection |
| Documentation | Optional-route/future-task guidance removed, historical test counts preserved, UTF-8/changed-document links/diff checked |

Raw artifacts, historical ReFS machine evidence, independent results, and existing state/data were neither deleted nor automatically migrated. Actual Unity A/B, service installation/update, and historical snapshot reclamation were not run during this removal task.
