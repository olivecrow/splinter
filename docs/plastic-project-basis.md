# Unity Version Control / Plastic project basis

Use `--source plastic --storage plastic-snapshot` for a regular, static Plastic workspace at its registered root. Specify a numeric changeset, repository and server. Admission fixes the workspace identity, management fingerprint, root revision and file inventory. Partial/Gluon, dynamic or mixed/nested management roots are unsupported. Workers are independent file workspaces. The original retains its Plastic registration.

```powershell
pwsh ./scripts/splinter.ps1 doctor --project <original> --source plastic --application unity
pwsh ./scripts/splinter.ps1 schema workspace.ensure
pwsh ./scripts/splinter.ps1 workspace ensure --project <original> --key <task-key> --source plastic --application unity --storage plastic-snapshot --base <changeset@repository@server> --home <home> --library-basis <prepared-id>
```

## Fixed revision reads

The Windows adapter uses the supported installed `cm` client (11.0.16.10484 libraries). It reads a fixed listing into a paged catalog and obtains payloads through a private, read-only helper. Small revisions can share a bounded request of up to 64 records and 4 MiB. Large files stream in segments. Checks cover revision IDs, segment order, lengths, MD5 and durable SHA-256 proofs. A valid local manifest does not guarantee continued access to the server revision. Missing or changed content preserves the task and its existing basis.

## Publish local files

Use `workspace checkpoint` with a stable request key, then plan full or selected paths with `workspace integrate --target original --result <id> --plan`. Check conflicts and pass the reviewed digest and intent on apply. Publication verifies the exact original registration, administration and target files under locks. A running original Editor needs the external cooperation permit. The operation changes selected local project files. Check-in, server locks and repository updates remain under the user's Plastic workflow. [Publication and retry](repeated-original-apply.md)

Release drops only owned local references after result and resource verification. Splinter does not pin server history. A shared [Library basis](git-library-reference.md) remains available while dependents or readers use it.
