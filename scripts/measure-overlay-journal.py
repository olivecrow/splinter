"""Read-only accounting of a stopped, product-verified journal, including compaction.

Checksums and the journal SHA256 bind the accounting to the product's seal. This
does not replace product replay/validation. Live payload is logical range usage,
not path-level physical allocation. No snapshot payload is read or copied.
"""
import argparse
import collections
import contextlib
import hashlib
import json
import os
import pathlib
import struct


@contextlib.contextmanager
def stopped_reader(path):
    if os.name != "nt":
        with path.open("rb") as stream:
            yield stream
        return
    # Deny writers and deletion, including an already running provider. Never
    # open a mutable handle or truncate a partial tail as recovery might do.
    import ctypes
    import msvcrt
    from ctypes import wintypes
    kernel = ctypes.WinDLL("kernel32", use_last_error=True)
    create = kernel.CreateFileW
    create.argtypes = [wintypes.LPCWSTR, wintypes.DWORD, wintypes.DWORD,
                       ctypes.c_void_p, wintypes.DWORD, wintypes.DWORD, ctypes.c_void_p]
    create.restype = wintypes.HANDLE
    handle = create(str(path.resolve()), 0x80000000, 1, None, 3, 0x08000000, None)
    if handle == ctypes.c_void_p(-1).value:
        raise ctypes.WinError(ctypes.get_last_error())
    try:
        descriptor = msvcrt.open_osfhandle(handle, os.O_RDONLY | os.O_BINARY)
    except BaseException:
        close = kernel.CloseHandle
        close.argtypes = [wintypes.HANDLE]
        close.restype = wintypes.BOOL
        close(handle)
        raise
    with os.fdopen(descriptor, "rb") as stream:
        yield stream


def key(path):
    # This bounded diagnostic qualifies ASCII fixture paths only. Python case
    # folding must not silently stand in for .NET OrdinalIgnoreCase on Unicode.
    if not path.isascii():
        raise ValueError("Unsupported non-ASCII path in diagnostic accounting")
    return path.lower()


def group_name(path):
    first = path.split("/")[0]
    return next((name for name in ("Assets", "Packages", "ProjectSettings", "Library")
                 if name.lower() == first.lower()), "other")


def replace_range(extents, replacement):
    start, length, physical, origin = replacement
    end = start + length
    result = []
    for old_start, old_length, old_physical, old_origin in extents:
        old_end = old_start + old_length
        if old_end <= start or old_start >= end:
            result.append((old_start, old_length, old_physical, old_origin))
        else:
            if old_start < start:
                result.append((old_start, start - old_start, old_physical, old_origin))
            if old_end > end:
                result.append((end, old_end - end, old_physical + end - old_start, old_origin))
    return sorted(result + [replacement])


def analyze(path):
    groups = collections.defaultdict(lambda: dict(transactions=0, logicalMutations=0,
        payloadBytes=0, metadataAndFrameBytes=0, retainedPayloadBytes=0))
    operations, written_paths = collections.Counter(), collections.Counter()
    nodes, paths = {}, {}
    sequence = frames = 0
    journal_hash = hashlib.sha256()
    with stopped_reader(path) as stream:
        initial = os.fstat(stream.fileno())
        while header := stream.read(8):
            if len(header) != 8:
                raise ValueError("Incomplete frame")
            metadata_length, payload_length = struct.unpack("<ii", header)
            if not 0 < metadata_length <= 65536 or not 0 <= payload_length <= 1048576:
                raise ValueError("Invalid frame lengths")
            metadata = stream.read(metadata_length)
            payload_offset = stream.tell()
            payload = stream.read(payload_length)
            checksum = stream.read(32)
            if len(metadata) != metadata_length or len(payload) != payload_length or len(checksum) != 32:
                raise ValueError("Incomplete frame")
            frame_hash = hashlib.sha256(header + metadata)
            frame_hash.update(payload)
            if frame_hash.digest() != checksum:
                raise ValueError("Journal checksum mismatch")
            for part in (header, metadata, payload, checksum):
                journal_hash.update(part)
            mutation = json.loads(metadata)
            entry = mutation["Entry"]
            operation = mutation["Operation"]
            first = mutation.get("FromSequence") if operation == "skip-range" else mutation["Sequence"]
            if first != sequence + 1 or mutation["Sequence"] < first:
                raise ValueError("Invalid sequence")
            logical_count = mutation["Sequence"] - sequence
            sequence = mutation["Sequence"]
            current_path = entry["Path"]
            path_key = key(current_path)
            origin = group_name(current_path)
            group = groups[origin]
            group["transactions"] += 1
            group["logicalMutations"] += logical_count
            group["payloadBytes"] += payload_length
            group["metadataAndFrameBytes"] += metadata_length + 40
            written_paths[current_path] += payload_length
            operations[operation] += 1
            frames += 1
            if operation in ("skip-write", "skip-range"):
                if payload_length:
                    raise ValueError("Payload on skipped mutation")
                continue
            node_id = entry["Id"]
            node = nodes.setdefault(node_id, {"entry": entry, "extents": []})
            node["entry"] = entry.copy()
            if operation in ("write", "write-compacted"):
                fragments = (mutation.get("Fragments") if operation == "write-compacted" else
                             [{"Offset": mutation.get("Offset", 0), "Length": payload_length}])
                if not fragments or sum(f["Length"] for f in fragments) != payload_length:
                    raise ValueError("Invalid compacted payload length")
                previous_end = -1
                for fragment in fragments:
                    offset, length = fragment["Offset"], fragment["Length"]
                    if length <= 0 or offset < 0 or offset < previous_end or offset + length > entry["Length"]:
                        raise ValueError("Invalid fragment range")
                    node["extents"] = replace_range(node["extents"], (offset, length, payload_offset, origin))
                    payload_offset += length
                    previous_end = offset + length
            elif operation == "resize":
                node["extents"] = [(s, min(n, entry["Length"] - s), p, g)
                    for s, n, p, g in node["extents"] if s < entry["Length"]]
            elif operation == "delete":
                paths.pop(path_key, None)
            elif operation == "rename":
                destination = mutation["Destination"]
                destination_key = key(destination)
                if destination_key in paths and paths[destination_key] != node_id:
                    nodes[paths.pop(destination_key)]["entry"]["Deleted"] = True
                for old in list(paths):
                    if old == path_key or old.startswith(path_key + "/"):
                        moved_id = paths.pop(old)
                        moved = nodes[moved_id]
                        moved["entry"]["Path"] = destination + moved["entry"]["Path"][len(current_path):]
                        paths[key(moved["entry"]["Path"])] = moved_id
                node["entry"]["Path"] = destination
                paths[destination_key] = node_id
            elif operation not in ("create", "metadata"):
                raise ValueError("Unknown journal operation: " + operation)
            if operation not in ("delete", "rename") and not entry.get("Deleted", False):
                paths[path_key] = node_id
        after = os.fstat(stream.fileno())
        if (after.st_size, after.st_mtime_ns) != (initial.st_size, initial.st_mtime_ns):
            raise ValueError("Journal changed during accounting")
    live_groups, live_paths = collections.Counter(), collections.Counter()
    physical_ranges = []
    for node_id in set(paths.values()):
        node = nodes[node_id]
        if node["entry"].get("Deleted", False):
            continue
        current_path = node["entry"]["Path"]
        for _, length, physical, origin in node["extents"]:
            live_groups[group_name(current_path)] += length
            live_paths[current_path] += length
            groups[origin]["retainedPayloadBytes"] += length
            physical_ranges.append((physical, length))
    physical_ranges.sort()
    if any(a[0] + a[1] > b[0] for a, b in zip(physical_ranges, physical_ranges[1:])):
        raise ValueError("Overlapping live physical ranges")
    for group in groups.values():
        group["obsoletePayloadBytes"] = group["payloadBytes"] - group["retainedPayloadBytes"]
        if group["obsoletePayloadBytes"] < 0:
            raise ValueError("Invalid payload accounting")
    total_payload = sum(g["payloadBytes"] for g in groups.values())
    overhead = sum(g["metadataAndFrameBytes"] for g in groups.values())
    if total_payload + overhead != initial.st_size:
        raise ValueError("Unaccounted journal bytes")
    return dict(journalBytes=initial.st_size, journalSha256=journal_hash.hexdigest(),
        durableSequence=sequence, frameCount=frames, groups=dict(groups), operations=dict(operations),
        livePayloadBytes=sum(live_groups.values()), obsoletePayloadBytes=total_payload-sum(live_groups.values()),
        metadataAndFrameBytes=overhead, unclassifiedBytes=0, livePayloadByCurrentGroup=dict(live_groups),
        largestWrittenPaths=written_paths.most_common(15), largestLivePaths=live_paths.most_common(15),
        diagnosticReadBytes=initial.st_size, checksumsVerified=True, payloadIncludesRepeatedWrites=True,
        fileDataAllocationMeasuredSeparately=True, obsoletePayloadIsNotGuaranteedCompactionSavings=True,
        qualifiedPathComparison="ASCII case-insensitive; non-ASCII rejected")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("journal", type=pathlib.Path)
    parser.add_argument("--output", required=True, type=pathlib.Path)
    args = parser.parse_args()
    if args.journal.resolve() == args.output.resolve() or args.output.exists():
        raise ValueError("Output must be a new file distinct from the journal")
    result = analyze(args.journal)
    with args.output.open("x", encoding="utf-8") as output:
        json.dump(result, output, indent=2)
    print(json.dumps({name: result[name] for name in
                      ("journalBytes", "durableSequence", "livePayloadBytes", "obsoletePayloadBytes")}))


if __name__ == "__main__":
    main()
