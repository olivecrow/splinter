<a id="반복-uac-없는-저장소-권한-경계"></a>
# Storage privilege boundary without repeated UAC

[한국어](../ko/plans/storage-privilege-service.md)

The usage goal is normal workspace creation/reconnection/reclamation without pressing UAC after initial installation/privilege configuration. [Service code and installation tools](../storage-privilege-service.md) are connected; actual completion follows [UA verification](../verification/storage-privilege-service.md). Disabling UAC or running the entire CLI/Unity/agent as administrator is not the default route.

<a id="목표-구조"></a>
## Target architecture

Place a narrow privilege service in the Windows storage adapter. Distinguish administrator approval for machine changes such as installation/update/removal from repeated operations within already approved scope. Normal CLI/Git/Editor run under current-user permissions; the service handles only work necessary for VSS capture/observe/release. This does not plan converting Runtime to a daemon/DB writer or automatically allocating tasks. Core retains provider-neutral contracts.

Keep service executables/registrations and privileged state where normal users cannot change them. The service verifies requester user identity, registered project/volume/workspace ownership, fixed commands/input schemas, request IDs/retries, independent results/final dependencies, and actual snapshot identities. Provide no interface executing arbitrary user-supplied PowerShell/executables/file paths as administrator. Separately verify IPC access controls/peer authentication/reparse/path substitution/other-user requests/resumption after interruption.

If the service is missing/incompatible, return a state requiring installation/recovery. Do not automatically show repeated UAC or bypass through administrator CLI. Classify the current exact-helper method as diagnosis/manual recovery. Snapshot deletion blocking and `reclaim-pending` still apply with a privilege service; obtaining privileges does not approve deleting another task.

| Verification unit | Completion criteria | Status |
| --- | --- | --- |
| UA-01 installation/lifecycle | Restricted service registration after initial approval, restart discovery, updates/removal preserving active results | Passed: installation/same-package update/active-state removal rejection/restart |
| UA-02 request boundary | User/ownership/exact ID/schema/path verification, reject arbitrary execution and other-task requests | Passed: automated boundaries, six rejection types in installed SYSTEM service, protected paths, SCM peer |
| UA-03 actual repeated operations | Normal-user CLI A/B creation/reconnection/reclamation without additional UAC, exact result/resource evidence | Passed: dedicated NTFS/Git A/B, same-snapshot reconnection, independent results/reclamation, 0 additional UAC |
| UA-04 failure/recovery | No duplicate capture or false completion with service interruption/lost responses/restart/incompatibility | Passed: normal CLI stop error/resumption, restart/identical response after lost Observe response body, version rejection; capture interruption is automated testing |

This decision is limited to [UA verification](../verification/storage-privilege-service.md)'s Windows x64, registered project/home, and small fixture. Do not expand to forced termination during native VSS calls, actual reboot/power failures, general Unity/ARM support. This work fulfills privilege UX requirements in the [per-conversation installation/usage plan](session-workflow-and-installation.md); overall product installation/automatic binding are separate. Do not add the historical limited administrator session for delayed reclamation tests to product-service evidence.

Windows design references are Microsoft's [administrator broker model](https://learn.microsoft.com/en-us/windows/win32/secauthz/administrator-broker-model) and [OS service model](https://learn.microsoft.com/en-us/windows/win32/secauthz/operating-system-service-model). This document defines completion criteria; actual installation uses explicit setup entry points.
