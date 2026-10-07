<a id="splinter-용어집"></a>
# Splinter glossary

[한국어](ko/glossary.md)

Terms needed to read CLI responses and feature documents. Each entry links to the document owning its exact usage conditions. Check command inputs through `schema <command>` before execution.

<a id="작업과-식별자"></a>
## Tasks and identifiers

| Term | Explanation and distinctions | Detailed contract |
| --- | --- | --- |
| original / project | The starting project and explicit target for applying results. The original remains editable while work proceeds. | [Workspaces](vcs-independent-workspaces.md) |
| task | Logical work continuing with the same purpose and fixed key. Ending a conversation or CLI process does not automatically end the task. | [AI start guide](agent-start.md) |
| workspace | Independent path for editing one task's files, together with its state and owned resources. Use response `data.workspace.path`. | [Current foundation](foundation.md) |
| worker | The workspace being edited. In development-view documents, the source workspace exporting candidates. It does not name the AI itself or a background thread. | [Development views](view-workflow.md) |
| peer | Another workspace using the same project or basis. Reclaiming A does not authorize cleanup of B's files, Editor, or results. | [Reclamation and observations](ntfs-workspaces.md#reclamation-and-observations) |
| home | Root directory for a project's task state, task paths, and shared executables. Specify the same home for resumption/reclamation. | [AI start guide](agent-start.md) |
| key / request key | Fixed name for locating the same logical task/request again. Workspace `--key`, publication `requestKey`, and discard `intentKey` belong to their respective commands. Different input with the same key does not become a new request. | [AI start guide](agent-start.md), [candidate publication](source-view.md#same-request-publication-recovery) |
| ID / id | Splinter-assigned identifier for a specific workspace, basis, or other object. Distinct from a user-chosen task key; identify the exact target through status/list. | [Explicit discard](workspace-discard.md) |
| owner | Identity describing which task owns a resource/receipt. Development-workflow generation distinguishes generations of the same owner. | [Development views](view-workflow.md) |
| digest / fingerprint / hash | A summary of input/content. `requestDigest` identifies fixed task inputs; plan digest identifies an application plan; fingerprint verifies content under its contract. They are not interchangeable. | [Original application](plain-folder-original-apply.md), [source verification](source-verification.md) |
| intent | A record fixing target/input before an interruptible operation such as application, discard, or restore. Resume the same intent after interruption, without deleting files or bypassing through new keys. | [Original application](plain-folder-original-apply.md), [discard](workspace-discard.md) |
| receipt | Record of verified target/ownership for creation, registration, publication, reclamation, or another operation. Existence does not mean a mount/process is currently alive. | [Project basis](plain-project-basis.md), [reclamation and observations](ntfs-workspaces.md#reclamation-and-observations) |

<a id="기준과-범위-저장"></a>
## Bases and range storage

| Term | Explanation and distinctions | Detailed contract |
| --- | --- | --- |
| source | Selected source-management boundary: `git`, `plastic`, or `none`. It determines which VCS basis, registration, and metadata are protected, separately from application. | [VCS-free workspaces](vcs-independent-workspaces.md) |
| application | Policy of the application using the project. Currently `unity` or `none`, used for Editor locks, companions, and generated directories. | [Extension boundaries](extensibility-boundaries.md) |
| storage | How basis data is read and changes stored. See storage modes below. It is not another name for source/application. | [Current foundation](foundation.md) |
| base / baseRevision | `--base` requests a basis selection; `baseRevision` identifies the selected fixed version. Git commit, Plastic revision, or other meanings follow the source contract. | [Git basis](git-project-basis.md), [Plastic basis](plastic-project-basis.md) |
| snapshot | Reference/identity for reading an unchanged basis, including VSS, fixed Git objects, Plastic revisions, and managed-directory bases. It does not always mean a whole-project copy. | [Git basis](git-project-basis.md), [Plastic basis](plastic-project-basis.md), [project basis](plain-project-basis.md) |
| basis | Managed basis referenced by multiple tasks and its lifetime records. Library basis handles only Library; project basis handles a VCS-free project/generated data. Task reclamation and final basis restoration are separate. | [Library basis](git-library-reference.md), [project basis](plain-project-basis.md) |
| overlay | File tree showing a task's changes over an immutable basis. It shows task-specific contents without copying or directly editing the basis. | [NTFS workspaces](ntfs-workspaces.md) |
| delta | Changed byte ranges and information such as file lengths, names, and deletions. It does not mean a whole copy of each changed file. | [NTFS workspaces](ntfs-workspaces.md) |
| private | Changed ranges/generated data not shared with original/other tasks. This does not mean encryption or isolation from malicious code execution. | [Current foundation](foundation.md) |
| journal | Ordered record of change operations and required payload, reread to restore task file state. | [Compressed journal](journal-payload-compression.md) |
| SJR2 / overlay v2 / candidate v3 | Names/versions of current journal, overlay, and candidate formats. Separate from workspace state v2; earlier formats are not automatically migrated. | [Compressed journal](journal-payload-compression.md) |
| payload / extent / fragment / block | Payload is actual stored bytes; extent is an applied file range; fragment is its referenced portion; block is a compression/decoding unit. A small valid range may still occupy the whole referenced block's storage. | [Journal instrumentation](source-verification.md#meaning-of-measurements), [codec](workspace-payload-codec.md) |
| whiteout | Record hiding a basis path in one task without deleting the original file. | [Overlay experiment](snapshot-overlay.md) |
| graft | Attach a separate generated-directory basis at a source-basis path, such as an explicitly prepared Library basis on a Git basis. | [Library references](git-library-reference.md) |
| hydration / read cache | Filling task disk with data read from a basis. Current product range storage does not use read disk hydration. | [Git basis](git-project-basis.md), [Plastic basis](plastic-project-basis.md) |
| copy-up | Moving a whole basis file into a private copy for modification. Current product uses no whole-file copy-up fallback. Distinct from file-staging costs of explicit original application. | [Original application](plain-folder-original-apply.md) |
| mount / provider | Mount is the connected filesystem path; provider is the implementation/process supplying that connection or basis reads. Source providers also use this name; read it with the target described in the owning document. | [NTFS workspaces](ntfs-workspaces.md) |
| managed view / managed original | Writable mount at the existing original path after authorized Library/project data moves within the same NTFS volume. Different from an ordinary folder; final explicit restore returns ordinary NTFS. | [Library references](git-library-reference.md), [project basis](plain-project-basis.md) |

| Product storage mode | Basis reads |
| --- | --- |
| `ntfs-snapshot` | NTFS basis fixed through VSS. Requires restricted-privilege service and registered scope. |
| `git-snapshot` | Fixed local Git commit/tree/blob. No VSS service/grant required. |
| `plastic-snapshot` | Recorded Plastic changeset/revision. No VSS service/grant required. |
| `project-snapshot` | Authorized VCS-free managed project basis. No VSS service/grant required. |

`git-checkout`, views, and probes belong to the [development CLI](development-cli.md). Do not use them as automatic fallback from product NTFS failures.

<a id="보존반영회수"></a>
## Preservation, application, and reclamation

| Term | Explanation and distinctions | Detailed contract |
| --- | --- | --- |
| checkpoint | Command preserving results after normal owned-Editor shutdown and stable-source verification. It does not mean compile/test success. | [AI start guide](agent-start.md#editing-editor-use-and-completion) |
| independent result | Source result retained even after the existing snapshot/task mount is reclaimed. Git and file-payload results have different representations. | [NTFS workspaces](ntfs-workspaces.md), [VCS-free workspaces](vcs-independent-workspaces.md) |
| seal | Value comparing later changes against journal sequence, length, hash, etc. at a point in time. It is neither compile acceptance nor user approval. | [Source verification](source-verification.md), [candidates](source-view.md) |
| candidate | Development input separately published from a stopped worker's valid changes. It is separate from later worker edits but still depends on the original snapshot, unlike an independent result. | [Immutable candidates](source-view.md) |
| view / source generation / cache | Development view is a fixed execution path for the selected candidate. Source generation is a source version per selection; cache is generated data retained in the same view. These are not default product task modes. | [Development views](view-workflow.md) |
| integrate / apply | Apply checkpoint results to an explicit target. Distinguish Git branch integration from conditional file publication through `--target original` for ordinary folders/recorded Plastic. | [Current foundation](foundation.md), [file application](plain-folder-original-apply.md), [Plastic application](plastic-original-apply.md) |
| release | Reclaim that task's owned resources after preserving independent results. Original application is optional; this differs from result discard. | [Reclamation and observations](ntfs-workspaces.md#reclamation-and-observations) |
| discard | Explicitly abandon that task's working data and reclaim owned resources without a successful checkpoint. It does not undo content already applied to the original. | [Explicit discard](workspace-discard.md) |
| restore | Return current original contents from Library/project basis to ordinary NTFS after all dependents/readers end. It does not revert original edits to initial preparation time. | [Library references](git-library-reference.md), [project basis](plain-project-basis.md) |
| compact | Explicitly clean superseded journal ranges after normal stop/unmount and seal/budget checks. It is neither task discard nor ordinary file deletion. | [Compressed journal](journal-payload-compression.md) |
| reclaim-pending / discard-reclaim-pending | Waiting for physical-resource reclamation on preservation/discard paths respectively. Not completed shutdown; explicitly retry the same task/request. | [AI start guide](agent-start.md#reading-failures-and-completion) |
| lease / fence / dependency | Lease is access lifetime held during work; fence prevents concurrent read/write/reclamation conflicts; dependency is a relation to an object still using the basis. Expiry/timeouts authorize neither deletion of other tasks nor general ownership takeover. | [Development views](view-workflow.md), [discard](workspace-discard.md) |
| grant / authorization | Grant is user/project/volume/home scope registered with the restricted service. Authorization is privilege/ownership evidence checked for a specific capture/release/discard request. It does not replace user intent or target checks. | [Privilege service](storage-privilege-service.md) |

For example, to finish A while preserving its result, use checkpoint → release. To apply to original, use checkpoint → explicit integrate → release. To abandon failed A, use discard. If B still uses the same project basis, A reclamation does not permit project restore.

<a id="응답과-검증-상태"></a>
## Responses and verification status

| Term | Explanation and distinctions | Detailed contract |
| --- | --- | --- |
| Accepted / Succeeded / Failed | Request accepted, command succeeded, and command failed respectively. Accepted is incomplete; Succeeded does not mean game-feature/test acceptance. Read `terminal`, `error`, `nextActions`, and actual readiness/resource observations together. | [AI start guide](agent-start.md) |
| fake / synthetic test | Tests common contracts through controlled implementations/inputs instead of real OS/VCS/Editor. It is not actual WinFsp/VSS/Unity support evidence. | [Verification guide](verification/README.md) |
| native / real-environment test | Uses actual OS APIs, filesystems, processes, etc. Native file tests alone do not qualify separate Unity execution or all OS failures. | [Verification guide](verification/README.md) |
| Passed / PassedFixture | The specified test/fixture passed its defined conditions. Not a general support declaration for every input, current changed candidate, or other OS. | [Verification matrix](plans/validation-matrix.md) |
| scoped-complete-with-limitations | Predefined scope completed with documented limitations, unmeasured areas, or unexecuted scope remaining. | [Verification guide](verification/README.md) |
| Partial / InProgress / NotRun / unknown | Partially checked, in progress, not executed, and unobservable/undecidable respectively. NotRun is not failure; unknown is neither 0 nor success. | [Verification matrix](plans/validation-matrix.md) |
| M0 / NR·SF·SW / VC·WF·LB, etc. | Traceability IDs locating plan/verification scope. M0 is the first bounded real-use scope; others are units in their owning documents. They are not command names or counts of remaining implementation features. | [Implementation plan](plans/implementation-plan.md), [verification matrix](plans/validation-matrix.md) |

Verification records apply only to specified candidates, inputs, and environments. Historical passes and boundaries not yet executed after current changes remain separately identified.
