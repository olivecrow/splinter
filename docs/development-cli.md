# Development CLI and optional storage paths

The product entry point, `scripts/splinter.ps1`, provides NTFS workspace creation/resumption, source preservation/integration, reclamation/discard, and start/status/stop for the owned Unity Editor. Agents and external tools perform compilation, refresh, Play, tests, builds, capture, and acceptance decisions. `checkpoint` preserves stable data without declaring the work successful.

Discover development and optional features through the separate `scripts/splinter-dev.ps1` and `tools/Splinter.Development`. The same required build prepares them; installation, execution, and cleanup are not automatic.

```powershell
pwsh ./scripts/build.ps1
pwsh ./scripts/splinter-dev.ps1 capabilities
pwsh ./scripts/splinter-dev.ps1 schema view.execute
```

| Development path | Scope |
| --- | --- |
| `workspace ensure --storage git-checkout` | Explicit Git checkout diagnostic path. Do not use it as a fallback when a product snapshot fails. |
| `view --input` (`view.execute`) | Explicit lifetime management for [immutable source and a shared Library](view-workflow.md). No duplicate path for internal refresh/validation collection, acceptance decisions, or preservation of worker results. |
| `snapshot-spike --input` | [Independent overlay experiment](snapshot-overlay.md) and candidate publication, separate from product workspace commands. |
| `preparation-probe --input` | Bounded gate for [development preparation-interruption tests](ntfs-workspaces.md#development-preparation-interruption-tests). |
| `scripts/capture-unity-startup.ps1 -Capabilities` | Read-only, bounded before-start/live attribution capture for an already owned Unity workspace; BO scope. Separate script, not a product CLI command. |

Consult the corresponding documents for execution conditions of the existing replay/write tools and journal/kernel/product-write recovery tests. Product capabilities do not advertise this development catalog; they identify only the separate development entry point.

## Bounded Unity startup evidence

After editorless ensure and before first start, capture `before-start`. After the agent independently verifies readiness, capture `live` before release/discard. Use the returned `data.controlPath` and new output directories with existing parents:

```powershell
pwsh ./scripts/capture-unity-startup.ps1 -Capabilities
pwsh ./scripts/capture-unity-startup.ps1 -Stage before-start -ControlPath <control> -OutputPath <new-before-output> -InputManifest <fixed-input-manifest>
pwsh ./scripts/capture-unity-startup.ps1 -Stage live -ControlPath <control> -OutputPath <new-live-output> -BeforeCapture <before-output>
```

The script checks current task/mount/provider/Editor ownership, copies selected Bee graph/input/response/trace files and logs, and retains sanitized provider counters including `writeAdmission`/`gitReads` plus optional `git-preparation.json`. Default bounds are 2,048 files, 32 MiB per file and 128 MiB total, with at most 16,384 enumerated entries. Limit, changed-file, ownership or reparse failures retain partial evidence with `failure.json` and no successful `capture.json`. Outputs never overwrite prior evidence or overlap source/workspace/control.

Before/live correlation is the caller-dispatched interval between captures, **not an automatically measured Editor startup interval**. Observations are per-file, not an atomic Library snapshot or compilation proof; bounded live logs may be prefixes. Capture overhead must be separate from normal timing. No profiler arguments are injected and no Editor, external package, server, callback or source is started/stopped/modified. See BO verification for executed fixtures and remaining actual-Unity attribution.

## Bounded journal probe

`tools/Splinter.BoundedLogProbe` runs journal comparisons and selected interruption checks on fresh, explicitly selected fixtures. Its `capabilities` command lists the available methods and recovery checks. [Probe scope and usage](plans/structural-throughput-prototypes.md)

Product release/discard also protects snapshots referenced by a view or an open cache. Only workspace, candidate, cache, and receipt formats in the current contract are read. Unsupported fields/formats and damaged or incomplete requests are preserved and explicitly rejected. Recovery of an interrupted request uses that request's intent and receipt.

Unity and WF/LB execution records in verification are evidence for the specified candidate and input. They do not automatically qualify changes to external-tool connections or native execution under the current contract.

Bounded base-reversion measurement uses explicit environment inputs for `JournalBaseReversionEvidenceTests`, `NativeEvidenceTests`, and `ProductEvidenceTests`. Ordinary execution does not automatically activate these gates or real product tests. Run only with fixed inputs and a new evidence path; no separate probe is added to product command discovery.

Many-file timing measurement is activated only by the `SPLINTER_BASE_REVERSION_MANY_FILES` JSON input for `JournalBaseReversionManyFileEvidenceTests`. The input fixes the exact source/revision/owner/receipt/output/variant/files/profiles. `before` retains the owned Git basis ref; `after` releases only that ref after verifying content before and after. The required build without this input does not run the real measurement. Fix the file count, writes, owned basis, and exit criteria in advance. Do not overwrite existing evidence or automatically reclaim a failed basis.
