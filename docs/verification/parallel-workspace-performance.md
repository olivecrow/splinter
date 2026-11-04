# Restore-handle qualification and actual Unity recheck

**The per-file retained-handle candidate was rejected:** it is **4.20 times slower** on the small component check, despite avoiding the second payload hash. No file-evacuation product route was added. Six regression cases and a live-index catch-up correctness fix were retained instead. **The new matched actual Unity performance block is incomplete**, so no current whole-workflow speedup or below-half-public-time achievement is claimed.

The last complete matched measurement remains the preceding 908a5d3 result: 315.26s current versus 324.67s frozen public mean, with 24.67s versus 25.45s Library restoration. Its below-half target was missed. Those results are historical context and do not qualify the new metadata-recovery fix automatically; detailed earlier measurements remain in Git.

## Small retained-handle reproduction

A root handle does not recursively freeze its descendants. Keeping payload handles open prevents the same NTFS root rename. To preserve the exact root and directory identities while retaining each file fence, the prototype evacuates leaves to fixed staging names, moves the directory tree, then places those same leaves back. It performs two renames per file and no payload copy.

Both methods use 1,025 files of 65,536 bytes, 16 populated directories plus empty/nested directories, fixed full SHA-256/native-identity manifests and up to eight workers. ABBA order is root/file/file/root. Fixture construction and the identical independent final full byte/identity/inventory proof are excluded.

| Mean component cost | Root move with fresh reproof | File evacuation with retained handles |
| --- | ---: | ---: |
| Wall | 0.389s | 1.635s |
| Own process CPU | 2.367 CPU-s | 2.500 CPU-s |

The candidate consumes **5.6% more CPU**. Added namespace operations outweigh the avoided warm payload read. This candidate-friendly component comparison removes redundant full ancestor rescans while retaining exact parent type/path/identity and file guards; it omits production paged metadata, final metadata restoration and exact journal reclamation. It does not establish the fastest possible per-file implementation. It is not production durability or actual Unity performance qualification. No benefit is manufactured by omitting those obligations or weakening checks.

Twelve small safety scenarios pass: restart/lost response during evacuation, after evacuation, after root movement, during placement, before verified publication and after its response; write/delete/rename attempts are blocked at four live-handle boundaries; added source/target children are detected and preserved without successful final acknowledgement; same-byte replacement, missing staged files and changed bytes on restart are refused. Retries freshly hash/reidentify all retained files. Existing file fences cannot prohibit new children, and an added child can accompany the root before detection; complete inventory checks remain required.

Two initial sandbox executions failed with invalid native handles and are excluded. An initial native assertion looked for an injected child at its pre-rename path; the child was preserved at the moved path. Correcting that assertion produced the passing 12-scenario/ABBA run in 14.55s against 30s estimate/60s stop. Scratch states remain retained. OS/power-loss behavior and a bounded production evacuation planner were not qualified.

Three added product cases verify every leaf remains fenced through metadata and verified publication, additions after full physical proof or file metadata restoration are refused before acknowledgement/reclamation, and failure releases fences. Existing full byte/native identity, unknown-child, replacement, page backing, restart and reclamation guards remain.

## Actual result-collection defect and fix

The initial current trial hf-c1 passed compilation, scene isolation, Play and normal Editor stops but failed B result collection with JournalRepackMismatch. Same-request retry reproduced it. The accepted writer and copied shadow differed because three legitimate effects are not source mutation frames: process-local basis read admissions after restart, completed range-adoption receipts fixed by a later checkpoint, and replaced data-free basis reads removed when closed.

Small live/restarted reproductions first exposed admission/completion differences. Adding replace/close reproduced the remaining node mismatch in all three cases. The final three-case check passes in 1.85s, including missing-owned-proof refusal and exact-request recovery.

Recovery seeds read admissions once from the complete fixed node cursor; durable nodes still come from replay. Cache retirement requires a deleted/unlinked deterministic basis ID, absence from current and durable inventories, unchanged basis length and no private ranges. New completion receipts must extend the exact accepted checkpoint root, retain every prior receipt, bind the owned capture/adoption identity and pass complete adoption-map and ordinary backing/page/graph copy proof. All original seals, logical roots, accounting, native backing and same-point publication comparisons remain. The intent is not reset or replaced.

The expanded focused selection passes **105 tests with one subprocess-only skip** in **67.98s**, against 180s/360s. It includes existing native corruption/replacement, killed-process, reader and reclamation refusal coverage. The failed final-b request then resumes in 30.67s without manual authority edits; both source results and all original source/Library hashes match, and owned resources are absent. Library recovery takes **24.58s**. This recovery is not a successful timing sample.

## Matched Unity attempt and environment limit

The input and driver are unchanged: Windows, Unity 6000.3.27f2, URP 17.3.0, 75 source files, 17,863 generated Library files / 1,568,757,165 bytes, input commit 7ee96cd995bcb77b345f0028cdcdd351a7a34417. Public is the frozen c4fa5063964e7b8456de4ea7735c042fb9af8930 binary, not a new live GitHub-tip claim. Fresh project/home roots use C:/sp/hf-*/{p,h}. Each trial proves every input hash/length, clean Git state, all 53 package DLL hashes through the installed Editor's Mono and a 252-character maximum package path.

The unchanged driver overlaps A/B creation, source reads, Editor readiness, edits/refresh, scene work, Play and preservation/reclamation. Stops run A then B. Exact PID/project receipt checks, changed assembly MVIDs and code values A(11,20)/B(10,22), distinct saved scenes, at least15 actual Update frames, complete independent result hashes, all original hashes/HEAD/clean state/one worktree/ordinary Library and resource reclamation remain mandatory. Fixture/schema/input proof, package preflight, independent result proof and final out-of-band original proof are excluded identically from accounted time.

| Attempt | Outcome | Timing qualification |
| --- | --- | --- |
| hf-p1, public | Complete workflow and original proof pass | Accounted **339.96s**; Library **27.96s**; one observation |
| hf-c1, initial current | Result collection fails; fixed same-request recovery fully passes | Excluded |
| hf-c3, fixed current | Compile/scene pass; Play exceeds 60s while A shows a shared-preferences error | Excluded |

hf-c3 A displayed Opening file failed for the user-global Unity Preferences/Overlays/CanvasesSaveData.asset. PackageCache preflight passes and neither task has PackageCache missing-file/path errors. The modal was canceled once through the owned A window; no Force Quit was used. B stops normally; A initially returns EditorStopBlocked during Play. A's late response then reports 19 frames and B 20, both not playing. Both logs show normal cleanup and both exact PIDs disappear. Computer Use is completed and reset immediately after inspection/cancel.

Unity's [6000.3 overlay source](https://raw.githubusercontent.com/Unity-Technologies/UnityCsReference/6000.3/Editor/Mono/Overlays/OverlayCanvasesData.cs) places this asset under PreferencesFolder and saves on disable; [ScriptableSingleton](https://raw.githubusercontent.com/Unity-Technologies/UnityCsReference/6000.3/Editor/Mono/ScriptableSingleton.cs) resolves that to the shared Editor preferences folder. A cross-Editor file replacement race is an inference, not an identified writer. The file exists after shutdown. No global settings/cache deletion, permission change, user-Editor stop or unsupported preference override is applied.

hf-c3 source/results and all original hashes are fully preserved/restored, with absent owned resources; Library recovery takes **24.98s**. Late completion and UI intervention do not validate its performance sample. No current full-lifecycle mean can be calculated from these attempts, and the recovery times are not a matched-performance substitute. Changing Play scheduling would require a separately recorded method applied to both versions. The existing parallel-Play method has not been silently weakened or given larger limits.

The original four-case estimate is 2,100s/stop 4,200s. Individual limits remain fixture 90s/180s, preparation 45s/90s, creation 45s/90s, readiness 90s/180s, compilation 60s/120s, scene 20s/40s, Play 30s/60s, normal stops 120s/240s, capture 180s/360s, restoration 240s/480s, mandatory excluded package proof 30s/60s. Failed stages stop new trial work, and cleanup uses its separately recorded bounds; no in-flight limit is extended.

## Complete regression and limits

The required **pwsh ./scripts/build.ps1** passes locked restore and Release compilation with zero warnings/errors: **2,629 discovered = 2,593 passed + 36 conditional skips**, zero failures/errors/timeouts/aborts. Whole-command time is **673.40s**, estimate 780s/stop 1,560s, based on the previous complete 2,623-case 632.26s gate plus six small cases. All ten runtime DLL hashes match the frozen hf-c3 runtime. This hash match does not turn the failed performance block into a pass.

The conservative eight-class schedule, individual test timeout/cancellation and large-catalog opt-out remain unchanged. General Plastic/None performance, large games, Unity Test Runner/Player builds, complete native fault matrices, peak RSS/allocation, disk saturation and OS/power-loss behavior remain unqualified. No whole-pipeline optimality or below-half-public-time claim is made. Retained 100k/17GiB fixtures are not resumed.

Ignored evidence is under artifacts/handle-move-20261104/: small-summary.json, optimized prototype source/results, focused.xml, failed-before/passing-after reproductions, matched-plan.json, exact case schemas/responses/hashes/CPU, recovery-proof.json, complete-gate logs and final source/process/commit proof. Previous package-failure recovery remains under artifacts/catalog-physical-20261104/ and retained .local/ or C:/sp state. Ignored status never authorizes workspace/basis/result/request deletion. See [validation scope](README.md) and [Library contracts](../git-library-reference.md).
