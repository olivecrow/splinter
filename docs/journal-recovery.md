<a id="개발용-저널-중단-시험"></a>
# Development journal-interruption tests

[한국어](ko/journal-recovery.md)

`scripts/test-journal-recovery.ps1` is a development test of a deterministic fake immutable tree using the product `JournalDeltaStore`. Its default is a read-only plan; only `-Execute` creates a new evidence directory and synthetic private journal. It does not overwrite existing directories or delete results. First prepare Release test binaries with `pwsh ./scripts/build.ps1`.

```powershell
pwsh ./scripts/test-journal-recovery.ps1 -OutputPath artifacts/ntfs-recovery/<new-run-id>
pwsh ./scripts/test-journal-recovery.ps1 -OutputPath artifacts/ntfs-recovery/<new-run-id> -Execute
```

Execution invokes the Release test `JournalInterruptionTests.EvidenceRun`. The test child creates/writes alpha 64KiB and beta 4KiB and flushes an independent content oracle for acknowledged sequence 4 to an external file. It attempts one additional write/rename/replace/truncate/create/delete and stops at the declared boundary. The parent compares request nonce, boundary, PID, and start time, forcibly terminates only that exact child, confirms termination, and reopens the journal with the same identity. It does not invoke ordinary workspace commands, VSS, WinFsp, Git, Unity, or service installation.

| Boundary | Coverage |
| --- | --- |
| Before append, after header, after metadata, after checksum, before/after durable flush, after index application, before/after external ACK following call return | 9 cases for each of 6 operations. |
| After half the payload, after the whole payload | 2 write cases. |
| Before/after explicit `Flush()` | 2 cases for an already acknowledged prefix. |

There are 58 cases in total. Payload boundaries for operations without payload are not counted as duplicate tests. `commit.before-flush` and after-checksum interruptions occur at different call locations even if file state is identical. OS instructions inside flush, termination at every partial header/checksum byte, and power loss are excluded.

After confirming the owned child has exited, only journal guard/open is re-observed for up to 5 seconds on sharing violations 32/33. This development-harness rule waits for the previous writer's teardown and records retry counts per run. Retries do not conceal access denial, absence, corruption, or replay errors.

The common `IJournalMutationObserver` is connected only through an explicit constructor argument. Ordinary Runtime/Windows composition has no observer. Observation callbacks do not read the journal, query Length, Flush, or Dispose. Payload is split in two for development boundaries only when an observer is connected; ordinary writes retain the existing single payload append. Callback failure locks the store into reconnect-required state, preventing further writes on the same instance after a partial append.

Every normal transaction still applies its index and returns after checksum append and `Flush(true)`. Only an incomplete final frame can be truncated during reconnect. Complete frames with damaged checksum, sequence, or JSON are preserved/rejected under the existing `DeltaCorrupt` contract. A complete transaction that did not return may survive process termination, but mixed namespaces or partial application are prohibited. New content after durable flush and external ACK must survive. The explicit-flush test's prefix was already acknowledged by individual commits.

`contracts/v1/journal-recovery-probe-{request,signal,result}.schema.json` owns request, gate, and trial-result formats. Gate/ACK files are published by flushing a new temporary file in the same directory and moving without overwrite. Existing trials are not appended to. After termination and before replay, record a raw journal copy, before/after hashes and lengths, sequence, namespace/length/content hashes, and invariance of a second replay. Initial payload copies, read cache, and base payload reads must all be 0. These values apply only to the fixture's synthetic private writes.

[NR recovery verification](verification/ntfs-recovery.md) owns actual candidate hashes, pass/fail results, and limitations. Internal journal-boundary tests do not qualify actual mount callback responses, mmap flush, VSS, reboot, power loss, or physical-space failures.
