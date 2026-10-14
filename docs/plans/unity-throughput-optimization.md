# Unity workflow throughput

[한국어](../ko/plans/unity-throughput-optimization.md)

This follow-up addresses the slow cold preparation and finalization measured in [MW-01](../verification/unity-maze-workflow.md). Its acceptance criterion is an honest comparison with one shared Editor, including preparation, integration, normal shutdown and final restoration. Storage savings alone do not establish a useful speed advantage.

## Implementation and admission

1. Reuse unchanged Git conversion hashes by configuration and per-path attribute interpretation. Retain the complete operation proof, HEAD/index, membership and journal-generation barriers. The new manifest is version 3; old versions are preserved and rejected, including audit requests.
2. Reduce repeated capsule metadata opens/validation within one protected commit preparation. Retain current physical-byte validation, final retirement checks and one durable flush per acknowledged mutation.
3. Evaluate private-read contention with bounded in-flight encoded data and the existing decoded cache. A read must keep one generation, drain before close and reject a concurrent poisoned writer.
4. Test native Windows cache retention on a small mount before considering a product change. Buffered and mapped writes, explicit final flush, rename/unlink, seal equality and complete drain are gates. A faster read loop alone is insufficient.
5. Evaluate initial Git admission and a bounded capsule-container design before changing their contracts. Avoid moving full reads to finalization or replacing current content proof with timestamps. Unbounded obsolete history, RAM-only acknowledgements and full-file copy-up remain excluded.

The stages are independent enough for parallel implementation; builds and native probes are coordinated. Existing documentation work and historical evidence remain intact. No automatic Editor pool, original-Editor sharing, package changes, service update or global settings change is included.

## Verification and comparison

Use targeted correctness and bounded cost checks first, followed by `pwsh -NoProfile -File ./scripts/build.ps1`. A full Unity comparison requires a frozen candidate, exact original/source/Library inventories and the same fixed A/B maze payload as MW-01. Use the repaired shared-Editor scene handoff for both paths and wait for the owned normal-exit receipt. Run the explicit combined checkpoint/release commands concurrently for A/B. Preserve independent results and verify original restoration and zero owned active resources.

Report cold end-to-end cost and individual phases. Do not add overlapping branch/counter durations, call prewarming free, extrapolate small fixture gains, or present the old shared-Editor modal delay as useful work. If component gains are insufficient to justify an expensive full rerun, record that limit and the remaining bottleneck rather than claiming a speed win.

## Outcome

TP-01–04 are retained. The native-cache candidate passed small mapped-file checks but failed normal reclamation in the real maze workflow: eleven linked Burst executable-image contexts remained per workspace after both Editors exited normally. Exact no-write existing-file opens released only idle image sections; source, DLL metadata and complete delta-byte inventories remained equal, and the same workspace IDs were reclaimed. Kernel-cache retention is withdrawn, and cleanup purge remains enabled. Recovery is not a successful end-to-end benchmark. The shared-Editor comparison and the remaining performance limit are recorded in TP-06; no additional large Splinter replay is justified by a partial-phase improvement alone.
