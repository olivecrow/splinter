<a id="독립-범위-payload-codec-검증"></a>
# Independent range payload codec verification

[한국어](../ko/verification/workspace-payload-codec.md)

This prerequisite unit from 2026-10-03 is the **common codec before product integration** in the [storage plan](../plans/storage-priority-and-editor-modes.md#range-payload-codec-and-next-product-integration). It added `IWorkspacePayloadCodec`, Runtime raw/Brotli representations, literal fake/conformance, development discovery information, and output schema. That candidate was not connected to journal/workspace; qualification is limited to the independent codec. Subsequent product integration is owned by [separate JP](journal-payload-compression.md). Current block/byte schema and buffer lifetimes follow the [feature document](../workspace-payload-codec.md).

<a id="자동-검증의-경계"></a>
## Automatic verification boundaries

`WorkspacePayloadCodecTests` applies the same common contract to a separate literal fake and Runtime. It verifies empty/small/4KiB/64KiB ranges, repeated input up to 1MiB, original immutability, buffer prefix/partial/EOF reads, negative/upper bounds, and buffer overlap. Incompressible input does not exceed the same codec's raw representation; raw reads do not rent scratch. 8 simultaneous callers of one codec also retain their own input and encoded/output bytes.

Checksum corruption, truncation/extra bytes, a truncated Brotli stream with valid checksum, bytes after the stream, and declared lengths too small or large are not considered success. Unknown version/encoding, reserved values, and excessive declared lengths are rejected even for empty/EOF reads. Output remains identical to its pre-failure value, and decoded scratch is returned after success/failure. Cumulative managed allocation for 256 small repeated reads is bounded within 32KiB using a prepared pool. This excludes native codec state, pool retention, and system/Editor memory.

`ContractAndCliTests.PayloadCodecDiscoveryKeepsJournalIntegrationUnqualified` verifies no codec exposure in the ordinary CLI and `standalone-not-journal-integrated`, actual size constants, and current output schema in the development CLI. The schema also rejects an integrated-stage claim. No new codec command is advertised.

In the first full test run, omission of a new development capabilities property from the response schema failed 5 existing discovery tests. Rather than relaxing the strict schema, the exact standalone contract for that property was added. Raw evidence also retains the initial sandbox restore log ending without a specific error and the initial build/tests under ordinary user privileges. The final required build is judged against the candidate after this correction.

<a id="같은-실제-bee-입력의-codec-대조"></a>
## Codec comparison using the same actual Bee input

Input is the same four files/full SHA-256/lengths/mtimes from [candidate selection](library-block-compression-feasibility.md), totaling **51,859,898B**. `artifacts/storage-foundation/library-payload-20261003/codec-probe/` contains this comparison's materials, not a new product command/general harness. Runtime DLL SHA-256 in `codec-measurement.json` is `cb00008f42eed073ecadb3ce1e5926ec9007b5fe86783f034b760f3af3b770f7`. Streaming the actual `BrotliWorkspacePayloadCodec` with 4KiB/64KiB/1MiB profiles verified full restored hashes, per-request contents, and small partial reads for **12 file/profile combinations**. Source files were held read-only, initial/final hashes/lengths/mtimes matched, and encoded payload files were not saved.

The following is the **logical byte representation including codec v1's actual 48B overhead**, not a metadata assumption. The raw comparison representation also has the same 48B per block. This standalone representation and the current product journal frame are not treated as the same format.

| Block | Raw codec representation B | Selected codec representation B | Reduction from raw codec | encode ms | full decode ms |
| --- | ---: | ---: | ---: | ---: | ---: |
| 4KiB | 52,467,722 | 8,448,264 | 83.90% | 555.28 | 399.96 |
| 64KiB | 51,898,010 | 5,025,381 | 90.32% | 336.47 | 195.33 |
| 1MiB | 51,862,346 | 2,229,642 | 95.70% | 236.53 | 248.81 |

The 4KiB representation is 83.71% smaller than bare input bytes. Actual callback boundaries, corresponding private data, frame/external journal metadata, file allocation, VSS, and total storage were not measured, so this ratio is not presented as workspace savings. These CPU intervals were executed once per profile; they are not before/after comparisons of reads/hashing, durable flush, or Editor latency.

Small partial reads across the profiles numbered 55/10/8 and were all correct. Maximum scratch request/actual pool array sizes were 4,096/65,536/1,048,576B respectively. Rent/return counts matched at 25,381/1,598/110 respectively. The caller's fixed byte arrays separately totaled 3,145,783B. This supports that small requests rent scratch matching the current profile's block length, without retaining the whole file/cumulative input in memory. A 7B read still restores the whole compressed block, so read amplification remains.

Thread cumulative managed allocations totaled 425,344/91,288/1,575,024B per profile, samples including pool preparation. Native codec state, pool retention/residency, and total RAM were not measured. This is evidence of comparing actual files one range at a time without initial full duplication, read hydration, or snapshot comparison reads; no Editor/provider memory savings are claimed.

<a id="최종-판정"></a>
## Final judgment

Final `pwsh ./scripts/build.ps1` locked restore and Release had 0 warnings/errors, **865 total / 864 passed / 0 failed / 1 test excluded at the time skipped**. Only this shell's `DOTNET_PROCESSOR_COUNT=4` was set for the final execution. `build-final.log`, `tests.trx`, and initial-failure `build-normal-user.log`/`initial-tests.trx` are preserved; all 18 codec tests and 2 discovery tests passed. The actual-input comparison Runtime DLL hash matches the final build. Document UTF-8, local links, and diff checks passed.

**This independent codec unit is closed as scoped-complete-with-limitations.** Actual product journal, replay/compaction/candidate/independent result, seal/budget, native interruption/reclaim, and NTFS savings were NotRun in this unit. Workspace lifecycle, service/registrations, original/peer/pending snapshots were unchanged at the time. Subsequent bounded product qualification is distinguished as [JP](journal-payload-compression.md), retaining full SF-02/03 Partial and total peak/VSS attribution unknown.
