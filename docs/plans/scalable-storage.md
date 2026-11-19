# Storage qualification plan

[Product storage](../scalable-storage.md) uses task v4, managed basis v3, segmented overlay v3 and SPM2 metadata pages of up to 64 KiB. Fixed capture and publication bind logical state to physical backing and independent result references. These gates qualify that implementation on the declared input.

Record the exact builds, workload, source mode, and input sizes before a run. Compare complete lifecycles including preparation, capture, original publication and reclamation. Measure logical bytes, physical allocation, peak and retained process memory, handles, and time by phase. Use a fixed work order and report any setup outside the timing window.

The native 100,000-entry lifecycle, 17 GiB workload, repeated cost comparison and OS-failure boundaries need separate qualification. Preserve retained fixtures and their ownership state until a specifically scoped run. The default regression and small Unity maze measure different scopes. [Validation status](../verification/README.md)

## Required gates

| ID | Required result |
| --- | --- |
| SCAL-01 | Reproducible logical identities; reject corrupt/truncated/invalid/cyclic metadata; preserve/refuse old state. |
| SCAL-02 | Stream hierarchical/flat/long-path inputs of 1,024 / 32,769 / 262,145 / 1,048,576 entries through enumeration, lookup, comparison and reopening. |
| SCAL-03 | Exceed previous node/extent/hidden/path/JSON/journal limits without another total-input quota; distinguish modeled large offsets from actual large files. |
| SCAL-04 | Fixed capture under concurrent write/rename/delete/truncate/rotation; frozen retries, subsequent edits and selected publication. |
| SCAL-05 | Original edits after publication cannot alter a workspace or old result; share duplicate content and reclaim only unreferenced owned data. |
| SCAL-06 | Interrupt root/payload/publication/history/reclamation boundaries and inject storage exhaustion; recover the same request without mixed/lost results or duplicate publication. |
| SCAL-07 | Actual editorless 100,000-entry Git/None/Plastic lifecycle; separate small original/A/B Unity runs with two intermediate and one final A publication, preserving original/B Editors. |
| SCAL-08 | Before/after/after/before small-input costs, actual allocation/residuals, metadata/private memory and handles; fix sustained material regressions; final `pwsh ./scripts/build.ps1` and docs checks. |

No beta publication, Unity package, other OS/app support or general performance claim is included. Connected quotas or outstanding required qualification prevent overall completion.
