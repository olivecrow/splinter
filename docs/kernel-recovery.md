<a id="개발용-로컬-kernel-복구-시험"></a>
# Development local kernel-recovery tests

[한국어](ko/kernel-recovery.md)

`scripts/test-kernel-recovery.ps1` connects the product `JournalDeltaStore` and `WinFspOverlayFileSystem` to a real WinFsp `FileSystemHost`. The basis is a deterministic fake immutable tree with alpha 64KiB and beta 4KiB. This does not test real VSS providers/servers, workspace CLI, Git, or Unity. It uses the existing local WinFsp installation and does not change VIVOBOOK, installations, services, or volume settings.

The default is a read-only plan. Only `-Execute` creates new evidence and mount home, which must be separate trees. Existing paths are neither reused nor deleted. `-Cases` runs a subset of declared case names and is not reported as a full-matrix pass. Prepare Release test binaries with `pwsh ./scripts/build.ps1`.

```powershell
pwsh ./scripts/test-kernel-recovery.ps1 -OutputPath artifacts/ntfs-recovery/<new-run-id>
pwsh ./scripts/test-kernel-recovery.ps1 -OutputPath artifacts/ntfs-recovery/<new-run-id> -Execute
# Development reproduction of one case: only a new run is allowed.
pwsh ./scripts/test-kernel-recovery.ps1 -OutputPath artifacts/ntfs-recovery/<new-smoke-id> -Cases 'write:callback.before-effect' -Execute
```

| Operation | Termination boundary | Count |
| --- | --- | --- |
| write / rename / replace / truncate / explicit flush | Before callback, after effect but before success response, after external-client ACK | 3 each, 15 total. |
| mmap | Before view flush, before/after file-flush callback, after view/file flush completion and external ACK | 4. |

The common `IOverlayMutationObserver` is used only through an explicit development constructor argument. Ordinary providers run without an observer. Callback boundaries preserve the mutation gate and existing individual journal-flush policy. Execution can stop at observation points before returning callback success, but does not interrupt every instruction inside the WinFsp driver/OS.

The external client writes/flushes a 4KiB prefix to the actual mount and flushes an independent oracle and ACK to the evidence path. The provider enables the gate after observing this prefix. The parent terminates only its own child whose request digest, nonce, PID, and start time match. After checking journal replay and byte invariance of a second replay, confirm the actual mount is absent and open the same path with the same delta/identity through a new provider. Following read verification and a new 4KiB write/flush to beta, check normal stop/unmount and absence of handles, retained contexts, and children. Private delta and results are preserved.

An mmap view flush alone is not acknowledged. Publish the external ACK only after both `MemoryMappedViewAccessor.Flush()` and `FileStream.Flush(true)` on the same file complete. This follows the distinction between view flushing and file metadata/cache flushing in [Microsoft's FlushViewOfFile contract](https://learn.microsoft.com/en-us/windows/win32/api/memoryapi/nf-memoryapi-flushviewoffile). Actual tests cover process termination, not a guarantee against power loss. Unacknowledged dirty pages may be absent or survive; content after completed flush/ACK must remain.

While stopped after a write callback's effect, check whether a separate beta reader process responds. The reader opens the file before the write gate, publishes bytes/hash from `ReadExactly` externally during the gate, then closes the file. Distinguish read response from Close/process exit. Existing `Cleanup` also acquires the mutation gate, so confirm close completion after provider termination. This does not prove concurrent completion of new file opens or atomic concurrent writes to the same file/range. Callback before-effect requires no new content; after-effect and external ACK require completed new content. Flush cases concern the already acknowledged prefix.

Requests, signals, and trial results follow `kernel-recovery-probe-{request,signal,result}.schema.json`. Control/ACK files are published by flushing a create-new temporary file in the same directory and moving without overwrite. After termination, only journal guard/open is re-observed for up to 5 seconds on sharing violations 32/33; access denial, absence, or corruption are not concealed. Still-mounted/unknown paths are not deleted. A new host removing the previous mount's exact empty ordinary directory is limited to the mount-creation contract.

Initial payload copies and read cache must be 0. Distinguish `baseReadBytes` for normal basis reads, journal private writes, and raw evidence-copy costs. [NR recovery verification](verification/ntfs-recovery.md) owns candidates, actual execution, failures, and limitations. Do not extend this qualification to real VSS reconnect, ARM/VIVOBOOK, power/reboot, or physical-space failures.
