<a id="개발-cli와-선택적-저장-경로"></a>
# Development CLI and optional storage paths

[한국어](ko/development-cli.md)

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

Consult the corresponding documents for execution conditions of the existing replay/write tools and journal/kernel/product-write recovery tests. Product capabilities do not advertise this development catalog; they identify only the separate development entry point.

The retained [bounded range-log experiment](plans/structural-throughput-prototypes.md), `tools/Splinter.BoundedLogProbe`, exposes a read-only `capabilities` command. It measures bounded obsolete range history and generation checkpoints on a new explicitly selected fixture/output. It is a development tool, not a workspace storage mode or proof of complete Unity workflow speed.

The [native Bee experiment](verification/bee-internal-native.md) was retired after its complete small lifecycle and storage use were worse than bounded storage. `tools/Splinter.BeeProbe`, its earlier junction mode and `scripts/measure-bee-prototype.py` were removed. Commit `93a7395` preserves their historical implementation; no Bee probe command or optional native storage path remains in the current checkout.

The same log measurement executable also accepts `measure --method bounded --cap-mib 16` to exercise the actual product `JournalDeltaStore`, and `product-selftest --root <new-path>` to terminate owned product-store children at selected durable publication boundaries. `wal` remains the earlier fixed-object prototype. Product-bounded timings include automatic state checkpoints and retain the final bounded suffix as ordinary operation does; they are separate from [SX](verification/structural-throughput-prototypes.md) and belong to [BL](verification/bounded-journal.md).

Product release/discard also protects snapshots referenced by a view or an open cache. Only workspace, candidate, cache, and receipt formats in the current contract are read. Unsupported fields/formats and damaged or incomplete requests are preserved and explicitly rejected. Recovery of an interrupted request uses that request's intent and receipt.

Unity and WF/LB execution records in verification are evidence for the specified candidate and input. They do not automatically qualify changes to external-tool connections or native execution under the current contract.

[Bounded base-reversion measurement](verification/journal-base-reversion.md) uses explicit environment inputs for `JournalBaseReversionEvidenceTests`, `NativeEvidenceTests`, and `ProductEvidenceTests`. Ordinary execution does not automatically activate these gates or real product tests. Run only with fixed inputs and a new evidence path; no separate probe is added to product command discovery.

[Many-file timing measurement](verification/journal-base-reversion-optimization.md) is activated only by the `SPLINTER_BASE_REVERSION_MANY_FILES` JSON input for `JournalBaseReversionManyFileEvidenceTests`. The input fixes the exact source/revision/owner/receipt/output/variant/files/profiles. `before` retains the owned Git basis ref; `after` releases only that ref after verifying content before and after. The required build without this input does not run the real measurement. Fix the [file count, writes, owned basis, and exit criteria](plans/journal-base-reversion-optimization.md) in advance. Do not overwrite existing evidence or automatically reclaim a failed basis.
