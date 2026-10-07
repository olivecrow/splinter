<a id="windows-제어-파일-게시읽기-경합"></a>
# Windows control-file publication/read races

[한국어](../ko/verification/control-file-read-races.md)

IPC-01–03 are **scoped-complete-with-limitations**. The records below validate actual Windows file replacement/reading and synthetic overlay-client state; the current handling contract follows [NTFS workspaces](../ntfs-workspaces.md). Actual WinFsp/VSS `prepare-worker` and Unity execution for a subsequent fixed candidate were checked separately in [WF-09–12](product-validation-unelevated.md). The heartbeat publication errors from that run and the unconfirmed cause of historical t2 remain recorded; success in this synthetic test does not qualify them.

<a id="수정-전-재현"></a>
## Reproduction before the fix

The baseline was `62a84c4`, Windows x64 10.0.26200, and .NET 10.0.12. Product `WindowsProcessWorkspaceProvider.WriteAsync/ReadAsync` published a complete 8 KiB body 3,000 times while 8 readers read concurrently. The writer continued using a flushed temporary file in the same directory and `File.Replace`. There was no destination deletion, content overwrite, or forced fault injection.

[Raw results before the fix](../../artifacts/storage-foundation/ipc-read-20260928/before-native/result.json): 3,000 publications; among 80,173 normal returns, 9,333 null returns; 2,278 `FileNotFoundException` exceptions; 2,419 `IOException` exceptions with sharing error 32; 0 corrupted reads; final sequence 3,000; 0 remaining temporary files; elapsed time 23.27 seconds. Normal returns include null; exceptions are counted separately. Null does not mean the file was actually removed.

Stacks confirmed file absence at both `CheckPath`'s `File.GetAttributes` and `FileStream` open, and sharing errors at open. The same file was also confirmed present immediately after some exceptions. Since `File.Exists` returning false does not distinguish an error from absence, it cannot determine the observation path's state.

The same failure type as the file-not-found message immediately after normal unmount in [WF t2](product-validation-cold-cost.md) was reproduced. That historical response lacks the reader's internal stack/HRESULT, so the exact location and external factors of that instance are not established. The initial sandbox run stopped on writer access denial and had no complete result, so it is excluded from the totals above. Raw files were preserved. Subsequent real-environment reproduction and verification used non-administrator Windows execution.

<a id="현재-처리와-보존-경계"></a>
## Current handling and preservation boundaries

The current contract follows [NTFS workspaces](../ntfs-workspaces.md). Observation-reader guard/open operations retry for a bounded period and parse JSON from the already-open stream. JSON corruption is not hidden by rereading. Only optional stop/flush/source-request polling permits immediate missing results. Readers do not modify files, attributes, or journals.

Additional regression tests cover completeness/sequence during actual concurrent replacement, waiting for first publication, a short exclusive handle, retrying the same read after persistent sharing failure, cancellation, actual absence, optional polling, JSON corruption, access denial, and a reparse parent. Separate client tests check repeating the same stop call after a read failure in synthetic unmounted state, preserving journal/state, rejecting another owner, and retaining unknown for unresolved launch. Synthetic process/snapshot receipts are not evidence of actual WinFsp mount, VSS, or Unity support.

A 500-replacement test has reached the 30-second limit when overlapping other flush loads in the full suite. The Library implementation candidate's [full run](../../artifacts/storage-foundation/library-20260929/automatic-first.trx) also failed at this limit, while [running only those 9 tests](../../artifacts/storage-foundation/library-20260929/control-file-read-recheck.trx) passed in 5.10 seconds. This time-bound test collection runs serially with other collections. Its 4 concurrent readers, 500 publications, 30-second limit, and the product's 1-second recovery contract remain unchanged; this does not establish the cause of the historical Unity failure.

<a id="수정-후-판정"></a>
## Assessment after the fix

[Raw results after the fix](../../artifacts/storage-foundation/ipc-read-20260928/after-native/result.json), with the same 3,000 publications, 8 readers, and 8 KiB body, show 22,831 reads, 0 nulls, 0 exceptions, 0 corruptions, final sequence 3,000, and 0 temporary files. Elapsed time was 17.29 seconds. One race reproduction before and after does not establish throughput improvement or maximum-load limits. Product DLLs and probes from both runs were preserved separately.

Locked restore, Release build, and the full automated suite through `pwsh -NoProfile -File scripts/build.ps1` produced **527 Passed, 0 Failed, and 1 Skipped test excluded at that time**. The 12 added tests passed; a separate actual concurrent-replacement regression used 500 publications and 4 readers. Persistent sharing errors remained exceptions after about 1 second, and repeating the same stop request retained state/journal bytes and unknown snapshot observations. Preservation for JSON corruption, access denial, reparse, another owner, and unresolved launch was also checked.

The [build log](../../artifacts/storage-foundation/ipc-read-20260928/build.log), [final TRX](../../artifacts/storage-foundation/ipc-read-20260928/final-candidate.trx), [cross-verification summary](../../artifacts/storage-foundation/ipc-read-20260928/verification-summary.json), and [source/binary/evidence manifest](../../artifacts/storage-foundation/ipc-read-20260928/evidence-manifest.json) are preserved. Document UTF-8 and change-diff checks also passed. External schema, current version, Core fake contracts, publication writer, and journal flush/seal did not change.

Establishing actual file absence adds up to about 1 second of retry waiting per observation read. Persistently absent optional stop/flush/source requests omit this wait. This does not forcibly interrupt OS delays in file opening, and the first error does not trigger arbitrary ownership takeover or data reclamation.

SF-02/03 remain Partial; SF-04–06 remain NotRun. The next actual product-preparation test must separately check normal unmount/the same-request path for the new candidate; this synthetic client success does not count as passing that test.
