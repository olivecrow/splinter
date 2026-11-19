# Compaction and base reversion

Use the exact existing workspace and home. Inspect `schema workspace.compact`, the stopped Editor, open handles and available backing space before initiating compaction. Retain the request's overlap limit and reversion choice on retry. A dependent reader or changing seal blocks publication.

Product overlay v3 compacts the segmented SJS1 journal. For a fresh compaction, omitting `--revert-base-ranges` leaves base comparison off. Explicit `true` compares private bytes of linked, base-backed files with the immutable basis in windows of up to 64 KiB. Matching portions of up to 4 KiB can return to reading from that basis. Changed ranges, bytes past the base length and file metadata remain represented. This runs at the explicit compaction boundary.

The segmented intent fixes the workspace, snapshot, before seal and reversion choice. Publication flushes the paged index, records the candidate roots, rolls the segment and verifies the resulting seal before completing the intent. Retry resumes the recorded operation. `baseComparedBytes` and `revertedPayloadBytes` report logical I/O and matching payload. Physical reclamation depends on remaining segment and result references. [Storage and maintenance](scalable-storage.md)

The [bounded](bounded-journal.md) and [latest-state](latest-only-journal.md) journal probes use separate single-journal representations. Product segmented compaction retains its segmented representation.
