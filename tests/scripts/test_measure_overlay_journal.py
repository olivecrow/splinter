"""Independent byte expectations for the stopped-journal accounting tool."""
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import struct
import tempfile
import unittest

spec = importlib.util.spec_from_file_location("measure_journal",
    Path(__file__).resolve().parents[2] / "scripts/measure-overlay-journal.py")
measure = importlib.util.module_from_spec(spec)
spec.loader.exec_module(measure)


def frame(sequence, operation, path="Assets/a", length=0, data=b"", node="a", **extra):
    mutation = dict(Sequence=sequence, Operation=operation,
        Entry=dict(Id=node, Path=path, Length=length, Deleted=operation == "delete"), **extra)
    metadata = json.dumps(mutation, separators=(",", ":")).encode()
    content = struct.pack("<ii", len(metadata), len(data)) + metadata + data
    return content + hashlib.sha256(content).digest()


class AccountingTests(unittest.TestCase):
    def setUp(self):
        self.temp_root = Path(__file__).resolve().parents[2] / "artifacts/test-journal-accounting"
        self.temp_root.mkdir(parents=True, exist_ok=True)

    def inspect(self, data):
        with tempfile.TemporaryDirectory(dir=self.temp_root) as directory:
            path = Path(directory) / "delta.journal"
            path.write_bytes(data)
            result = measure.analyze(path)
            self.assertEqual(path.read_bytes(), data)
            self.assertEqual(result["journalSha256"], hashlib.sha256(data).hexdigest())
            return result

    def test_overwrite_truncate_rename_and_deleted_payload(self):
        data = b"".join([
            frame(1, "create"), frame(2, "write", length=8, data=b"abcdefgh", Offset=0),
            frame(3, "write", length=8, data=b"WXYZ", Offset=2), frame(4, "resize", length=5),
            frame(5, "rename", length=5, Destination="Library/reused"),
            frame(6, "create", path="Library/trash", node="b"),
            frame(7, "write", path="Library/trash", node="b", length=3, data=b"123", Offset=0),
            frame(8, "delete", path="Library/trash", node="b", length=3),
        ])
        result = self.inspect(data)
        # Original abcdefgh -> abWXYZgh -> abWXY; trash is deleted.
        self.assertEqual(result["livePayloadBytes"], len(b"abWXY"))
        self.assertEqual(result["obsoletePayloadBytes"], 10)
        self.assertEqual(result["livePayloadByCurrentGroup"], {"Library": 5})
        self.assertEqual(result["groups"]["Assets"]["retainedPayloadBytes"], 5)
        self.assertEqual(result["groups"]["Library"]["obsoletePayloadBytes"], 3)

    def test_compacted_fragments_and_skip_sequence_preserve_live_accounting(self):
        data = b"".join([
            frame(1, "create"),
            frame(2, "write-compacted", length=8, data=b"ab", Fragments=[dict(Offset=0, Length=2)]),
            frame(3, "write-compacted", length=8, data=b"WXY", Fragments=[dict(Offset=2, Length=3)]),
            frame(4, "resize", length=5), frame(5, "rename", length=5, Destination="Library/reused"),
            frame(100, "skip-range", path="Library/discarded", FromSequence=6),
            frame(101, "metadata", path="Library/reused", length=5),
        ])
        result = self.inspect(data)
        self.assertEqual(result["livePayloadBytes"], 5)
        self.assertEqual(result["obsoletePayloadBytes"], 0)
        self.assertEqual(result["durableSequence"], 101)
        self.assertEqual(result["frameCount"], 7)
        self.assertEqual(sum(g["logicalMutations"] for g in result["groups"].values()), 101)

    def test_directory_rename_and_replaced_destination(self):
        result = self.inspect(b"".join([
            frame(1, "create", path="Assets/folder", node="folder"),
            frame(2, "write", path="Assets/folder/a", length=4, data=b"keep", Offset=0),
            frame(3, "rename", path="Assets/folder", node="folder", Destination="Library/folder"),
            frame(4, "write", path="Library/target", node="b", length=3, data=b"old", Offset=0),
            frame(5, "rename", path="Library/folder/a", length=4, Destination="Library/target", Replace=True),
        ]))
        self.assertEqual(result["livePayloadByCurrentGroup"], {"Library": 4})
        self.assertEqual(result["obsoletePayloadBytes"], 3)
        self.assertEqual(result["largestLivePaths"], [("Library/target", 4)])

    def test_corruption_incomplete_tail_and_invalid_sequence_are_read_only_failures(self):
        good = frame(1, "write", length=4, data=b"data", Offset=0)
        for data in (good[:-1], good + b"tail", good[:-1] + bytes([good[-1] ^ 1]), frame(2, "create")):
            with self.subTest(size=len(data)), tempfile.TemporaryDirectory(dir=self.temp_root) as directory:
                path = Path(directory) / "delta.journal"
                path.write_bytes(data)
                with self.assertRaises(ValueError):
                    measure.analyze(path)
                self.assertEqual(path.read_bytes(), data)

    def test_unicode_comparison_is_not_silently_assumed(self):
        with self.assertRaisesRegex(ValueError, "non-ASCII"):
            self.inspect(frame(1, "create", path="Assets/이름"))

    @unittest.skipUnless(os.name == "nt", "Windows share-mode boundary")
    def test_active_writer_is_rejected_without_changing_journal(self):
        data = frame(1, "write", length=4, data=b"data", Offset=0)
        with tempfile.TemporaryDirectory(dir=self.temp_root) as directory:
            path = Path(directory) / "delta.journal"
            path.write_bytes(data)
            with path.open("r+b"):
                with self.assertRaises(OSError) as failure:
                    measure.analyze(path)
                self.assertEqual(failure.exception.winerror, 32)
            self.assertEqual(path.read_bytes(), data)


if __name__ == "__main__":
    unittest.main()
