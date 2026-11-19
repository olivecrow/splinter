# Development source views

Use the development CLI for an explicit fixed-source view. An eligible `git-snapshot` worker without a Library graft can publish a candidate. A view selects a candidate into a distinct source generation and binds a private generated-file cache. Normal product workspaces use [their own writable project and Editor](alpha-workspaces.md).

## Execution inputs

Read `pwsh ./scripts/splinter-dev.ps1 schema view.execute`. Supply the absolute project, owner ID and generation, stable request key, action and action-specific identities. Keep the same state home. Submit JSON with `pwsh ./scripts/splinter-dev.ps1 view --input <request-file> --home <home>`. Preserve the input for a retry.

| Action | Ownership and result |
| --- | --- |
| `prepare-worker`, `publish` | Stop the owned worker Editor normally, remove owned bridge resources, flush and verify the source seal. Publication fixes a candidate against that worker and basis. |
| `select`, `start` | Select a candidate into a stopped view, then start the exact generation and Editor. Poll the status request returned in `nextActions` for pending readiness. |
| `status`, `stop`, `resume` | Observe the current bindings and resources, stop the selected generation normally or validate a stopped generation for continuation. |
| `preserve`, `release` | Freeze and verify an independent result for the selected candidate or source generation. Release only its owned journal after dependents close. |
| `compact-cache`, `close-view` | Compact a stopped cache within the supplied overlap budget, or reclaim it once every source generation is preserved and released. |

## Same-request publication recovery

Candidate, generation and cache have distinct identities, owners and seals. Keep the candidate's Git registration, source seal and read fences through publication. Fresh selections create distinct generations, even for the same candidate. The cache remains bound to its compatible project, basis and view. Preserve changed or unowned backing and inspect the returned error.

Retry interrupted operations with identical input, owner generation, home and key. The coordinator checks the ledger, pending intents and native state before advancing. A replayed receipt describes its completed operation. Query status for current execution. Worker release respects incomplete view publication, generation and cache dependencies.
