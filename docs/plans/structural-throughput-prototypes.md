# Structural throughput prototypes

[한국어](../ko/plans/structural-throughput-prototypes.md)

This user-authorized experiment follows [TP-06](../verification/unity-throughput-optimization.md). It compares two small, development-only implementations before any further complete Unity workflow. It does not select a new product backend or change the current journal format, workspace lifecycle, or default durability policy.

## Fixed scope

| ID | Experiment | Acceptance evidence |
| --- | --- | --- |
| SX-01 | Expose only an owned `Library/Bee` directory through native NTFS; retain the real range overlay for source and other Library paths | Real mounted file operations, exact target identity/path protection, source and peer isolation, independent source export, normal owned closure, equal graph output where actual Bee replay is possible |
| SX-02 | Bounded range log with a RAM current-range index and generation checkpoints | Flush before each successful write response; fixed log bound; complete current-range recovery and independent result; incomplete-tail, corruption, owner, and interrupted-publication checks |
| SX-03 | Matched small timing and storage comparison | Same operations and input, serial runs without competing builds/tests; separate cold preparation, checkpoint/cleanup, final file allocation, sampled temporary file bytes and write amplification; do not call sampled bytes an NTFS allocation peak |
| SX-04 | Decide whether larger validation is justified | Large component gains must retain the bounded safety gates; otherwise stop without a whole-project rerun or a Unity speed claim |

The Bee fixture is new and isolated. A preserved three-assembly, zero-package Unity 6000.3.22f1 build input is used read-only to replay the actual build-graph generator. If the generated graph has no IL post-processing nodes, also run cold, unchanged and one-source-change Csc builds and inspect the compiled constant. This does not measure IL post-processing, Editor readiness, or Play. Historical evidence and the sibling Unity repository are not edited. Native Bee starts without an inherited payload cache; only the small explicit build-input recipe is supplied to both variants. No original project or whole Library is copied, moved, or converted.

The native target and namespace are owned by the experiment. A mount-point prototype is not an OS security sandbox. Native file handles and mapped images require their own closure checks; zero overlay contexts alone is insufficient. An unknown handle or failed normal close preserves the output rather than forcing deletion.

The log fixture uses fixed generated input, approximately 48MiB of live private data, 20,000 logical objects, and bounded 4–16KiB changes, reduced for safety tests. Compare the current SJL4 implementation with a log that retains obsolete ranges only up to its explicit cap. Checkpoints write current private ranges, never the immutable basis; temporary checkpoint duplication and its time are included. This experiment permits bounded obsolete history, unlike the product's latest-only-at-every-ACK contract. It does not permit RAM-only acknowledgements. Group commit is a separate, unimplemented follow-up unless explicitly measured.

## Execution and limits

Implementation and read-only workload inspection may proceed in parallel. Timing runs are serialized after correctness checks. New outputs must reject existing directories. Freeze source/runtime identity before measurement and retain raw results and failed attempts. Run the repository's required build once after integration; do not overlap it with timed probes.

Recovery tests use owned child-process interruption and independently observed successful writes. They do not establish OS power-loss durability. Product source/result guards, original/peer protection, explicit cleanup, and external-tool boundaries remain unchanged. No Editor pool, external package modification, service update, ReFS backend, or original-project benchmark is included.

Any successful small result remains a component result. The previous shared-Editor whole workflow was 313.1508 seconds; only a later same-workload successful cold run including setup, integration, preservation and restoration could establish a complete speed advantage.

Recorded outcome: the attempted native Csc backend cannot atomically publish state through the graft (`ERROR_NOT_SAME_DEVICE`). The final matched Bee timing therefore covers graph generation only; `--compile` remains a diagnostic reproduction, not a supported build workflow. See the [verification record](../verification/structural-throughput-prototypes.md). Copy/delete would change the publication guarantee. A second namespace architecture remains unimplemented.
