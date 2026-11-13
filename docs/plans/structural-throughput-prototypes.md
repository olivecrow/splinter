# Bounded journal probe

`tools/Splinter.BoundedLogProbe` is a development tool for comparing serial journal operations on a generated basis and checking recovery at selected publication boundaries. Discover its arguments with `capabilities`.

| Command | Scope |
| --- | --- |
| `measure --root <new-directory> --method bounded` | Exercise the product's bounded SJR2 journal with automatic checkpoints and a retained suffix |
| `measure --root <new-directory> --method sjl4` | Exercise the current SJL4 latest-state journal |
| `measure --root <new-directory> --method wal` | Compare a fixed-object log prototype with an explicit log capacity |
| `selftest --root <new-directory>` | Check reconnect and selected process interruption boundaries for the SJL4 and prototype paths |
| `product-selftest --root <new-directory>` | Check the product journal at selected durable publication boundaries with owned child processes |

For timing comparisons, use the same inputs and fixed tool builds, and run the arms serially without competing builds or tests. Include checkpoint overhead and distinguish sampled temporary file bytes from actual NTFS allocation. Each run requires a fresh output directory. The probe covers individual journal operations on generated data. Complete workspace and Unity lifecycles and OS-failure durability have separate qualification. [Bounded journal](../bounded-journal.md) · [Validation scope](../verification/README.md)
