"""Summarize acknowledged journal writes after clients stop; never read payloads.

This is an accounting reader, not a replacement for the provider's checksum replay.
"""
import argparse
import collections
import json
import pathlib
import struct

parser = argparse.ArgumentParser()
parser.add_argument("journal", type=pathlib.Path)
parser.add_argument("--output", required=True, type=pathlib.Path)
args = parser.parse_args()
groups = collections.defaultdict(lambda: {"transactions": 0, "payloadBytes": 0, "metadataAndFrameBytes": 0})
operations = collections.Counter()
paths = collections.Counter()
sequence = 0
with args.journal.open("rb") as stream:
    while header := stream.read(8):
        if len(header) != 8:
            raise ValueError("Incomplete frame")
        metadata_bytes, payload_bytes = struct.unpack("<ii", header)
        if not 0 < metadata_bytes <= 65536 or not 0 <= payload_bytes <= 1048576:
            raise ValueError("Invalid frame lengths")
        mutation = json.loads(stream.read(metadata_bytes))
        if mutation["Sequence"] != sequence + 1:
            raise ValueError("Invalid sequence")
        sequence += 1
        path = mutation["Entry"]["Path"]
        group = groups[path.split("/")[0]]
        group["transactions"] += 1
        group["payloadBytes"] += payload_bytes
        group["metadataAndFrameBytes"] += metadata_bytes + 40
        paths[path] += payload_bytes
        operations[mutation["Operation"]] += 1
        stream.seek(payload_bytes + 32, 1)
    if stream.tell() != args.journal.stat().st_size:
        raise ValueError("Incomplete trailing transaction")
result = {
    "journalBytes": args.journal.stat().st_size,
    "durableSequence": sequence,
    "groups": groups,
    "operations": operations,
    "largestWrittenPaths": paths.most_common(15),
    "payloadIncludesRepeatedWrites": True,
    "fileDataAllocationMeasuredSeparately": True,
}
args.output.write_text(json.dumps(result, indent=2), encoding="utf-8")
print(json.dumps({"journalBytes": result["journalBytes"], "durableSequence": sequence, "library": groups.get("Library"), "output": str(args.output)}))
