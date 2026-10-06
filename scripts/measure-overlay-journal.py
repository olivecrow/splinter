"""Read-only accounting of one current SJR2 stopped, product-verified journal.

Checksums and the journal SHA256 bind the accounting to the product's seal. This
does not replace product replay/validation. Live payload is logical range usage,
not path-level physical allocation. Encoded blocks are counted whole even when
only a fragment remains live. Codec structure/checksums are checked, but this
tool does not decode Brotli or replace product replay. No snapshot is read.
"""
import base64
import re
import argparse
import collections
import contextlib
import hashlib
import json
import os
import pathlib
import struct
import uuid


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
    start, length, physical, block_offset, origin = replacement
    end = start + length
    result = []
    for old_start, old_length, old_physical, old_offset, old_origin in extents:
        old_end = old_start + old_length
        if old_end <= start or old_start >= end:
            result.append((old_start, old_length, old_physical, old_offset, old_origin))
        else:
            if old_start < start:
                result.append((old_start, start - old_start, old_physical, old_offset, old_origin))
            if old_end > end:
                result.append((end, old_end - end, old_physical, old_offset + end - old_start, old_origin))
    return sorted(result + [replacement])


def framed_json(data, magic):
    if len(data) < 44 or data[:4] != magic or struct.unpack('<q', data[4:12])[0] != len(data) - 44 or hashlib.sha256(data[:-32]).digest() != data[-32:]:
        raise ValueError('Corrupt complete current-state record')
    return json.loads(data[12:-32])


def validate_payload(payload, decoded):
    if not 0 < len(payload) <= decoded <= 1048576:
        raise ValueError('Invalid current block lengths')
    if len(payload) == decoded:
        return False
    if len(payload) < 48 or payload[:4] != b'SPB1' or payload[12:16] != b'\x01\0\0\0':
        raise ValueError('Invalid encoded current block')
    declared, stored = struct.unpack('<ii', payload[4:12])
    if declared != decoded or stored + 48 != len(payload) or hashlib.sha256(payload[:-32]).digest() != payload[-32:]:
        raise ValueError('Encoded current block checksum mismatch')
    return True


def capsule(data, identity):
    if len(data) < 128 or data[:4] != b'SJL4' or struct.unpack('<i', data[4:8])[0] != 4:
        raise ValueError('Unsupported capsule')
    generation, count, metadata = struct.unpack('<qqi', data[8:28])
    end = 128 + count
    if generation < 1 or count < 0 or metadata < 1 or end + metadata + 32 > 268435456 or data[32:64].hex() != identity:
        raise ValueError('Invalid capsule bounds or owner')
    if len(data) not in (end, end + metadata + 32) or hashlib.sha256(data[128:end]).digest() != data[96:128]:
        raise ValueError('Capsule data checksum differs')
    values, at = {}, 128
    while at < end:
        header = data[at:at+80]
        block_id, size = struct.unpack('<qi', header[4:16])
        if header[:4] != b'SJB1' or block_id < 1 or not 0 < size <= 1048576 or at + 80 + size > end or header[16:48].hex() != identity:
            raise ValueError('Invalid capsule block')
        block = data[at:at+80+size]
        if hashlib.sha256(block[80:]).digest() != header[48:80]:
            raise ValueError('Current block checksum differs')
        values[f'block-{block_id:016x}.bin'] = block
        at += 80 + size
    record = None
    if len(data) != end:
        if hashlib.sha256(data[:-32]).digest() != data[-32:]:
            raise ValueError('Complete capsule checksum differs')
        record = json.loads(data[end:-32])
        if record['Version'] != 1 or record['Generation'] != generation:
            raise ValueError('Capsule generation differs')
        for item in record['Items']:
            name = item['Name']
            if item['Metadata'] is not None:
                values[name] = base64.b64decode(item['Metadata'], validate=True)
            if len(values[name]) != item['Length'] or hashlib.sha256(values[name]).hexdigest() != item['Hash']:
                raise ValueError('Capsule item checksum differs')
    elif count == 0:
        raise ValueError('Empty capsule prefix')
    return generation, values, record


def analyze_latest(path):
    with stopped_reader(path) as root_file:
        root_bytes = root_file.read(268435457)
        is_capsule = root_bytes[4:8] == b'\x04\0\0\0'
        if is_capsule:
            footer_at = 128 + struct.unpack('<q', root_bytes[16:24])[0]
            root = json.loads(root_bytes[footer_at:-32])['After']
        else:
            root = framed_json(root_bytes, b'SJL4')
        if root['Version'] != 4 or not re.fullmatch(r'latest-[0-9a-f]{32}', root['Objects']):
            raise ValueError('Unsupported current root')
        objects = path.parent / root['Objects']
        if objects.is_symlink() or not objects.is_dir():
            raise ValueError('Missing or redirected current objects')
        names = sorted(objects.iterdir(), key=lambda item: item.name)
        digest = hashlib.sha256(root_bytes)
        groups = collections.defaultdict(lambda: dict(transactions=0, logicalMutations=0, payloadBytes=0,
            decodedPayloadBytes=0, metadataAndFrameBytes=0, retainedPayloadBytes=0))
        nodes, block_refs, hashes, lengths, virtual, owners, frames = [], {}, {}, {}, {}, {}, []
        physical_bytes = len(root_bytes)
        for file in names:
            if file.is_symlink() or not file.is_file() or not (re.fullmatch(r'(node-[0-9a-f]{32}\.json|bucket-[0-9a-f]{2}\.json|block-[0-9a-f]{16}\.bin|commit-[0-9a-f]{16}-[0-9a-f]{32}\.bin|owner\.lock)', file.name)):
                raise ValueError('Pending or unowned current journal file')
            with stopped_reader(file) as stream:
                data = stream.read(268435457)
                name = file.name.encode('utf-8')
                digest.update(struct.pack('<q', len(name))); digest.update(name); digest.update(struct.pack('<q', len(data))); digest.update(data)
                physical_bytes += len(data)
                if file.name.startswith('commit-'):
                    generation, values, record = capsule(data, root['IdentityHash'])
                    frames.append((generation, file.name, values, record))
                else:
                    virtual[file.name] = data; owners[file.name] = file.name
        if is_capsule:
            generation, values, record = capsule(root_bytes, root['IdentityHash'])
            if any(f[0] >= generation for f in frames) or len({f[0] for f in frames}) != len(frames):
                raise ValueError('Unexpected backup generation')
            frames.append((generation, path.name, values, record))
        for _, name, values, record in sorted(frames):
            virtual.update(values); owners.update({v: name for v in values})
            for removed in record['Removes'] if record else []:
                virtual.pop(removed, None); owners.pop(removed, None)
        for _, name, values, record in frames:
            if any(owners.get(item) != name for item in values):
                raise ValueError('Superseded metadata or payload remains')
        for file in names:
            if not file.name.startswith('commit-') and owners.get(file.name) != file.name:
                raise ValueError('Superseded seed file remains')
        file_bytes = sum(map(len, virtual.values()))
        for name, data in virtual.items():
            lengths[name], hashes[name] = len(data), hashlib.sha256(data).hexdigest()
            if name.startswith('node-'):
                node = framed_json(data, b'SJN1'); key(node['Entry']['Path']); nodes.append(node)
                groups[group_name(node['Entry']['Path'])]['metadataAndFrameBytes'] += len(data)
                for block in node['Blocks']:
                    if block['Id'] in block_refs:
                        raise ValueError('Duplicate current block owner')
                    block_refs[block['Id']] = (block['Hash'], node)
            elif name.startswith('bucket-'):
                framed_json(data, b'SJK1'); groups['other']['metadataAndFrameBytes'] += len(data)
            elif name == 'owner.lock' and data:
                raise ValueError('Owner guard is not empty')
        if file_bytes != root['FileBytes'] or len(virtual) != root['FileCount'] or sum(name.startswith('block-') for name in virtual) != len(block_refs):
            raise ValueError('Current inventory differs from root')
        live_paths, live_groups = collections.Counter(), collections.Counter()
        stored = decoded = live_stored = compressed = fragments = unlinked = 0
        for block_id, (expected_hash, node) in block_refs.items():
            name = f'block-{block_id:016x}.bin'
            if hashes.get(name) != expected_hash:
                raise ValueError('Current payload differs from node reference')
            pieces = sorted((extent for extent in node['Extents'] if extent['JournalOffset'] == block_id * 2097152), key=lambda extent: extent['BlockOffset'])
            if not pieces:
                raise ValueError('Unreferenced current block')
            descriptor = pieces[0]['Payload']
            end = 0
            for piece in pieces:
                if piece['Payload'] != descriptor or piece['BlockOffset'] != end or piece['Length'] <= 0 or piece['Start'] < 0 or piece['Start'] + piece['Length'] > node['Entry']['Length']:
                    raise ValueError('Invalid or superseded current payload slices')
                end += piece['Length']; fragments += 1
            if end != descriptor['DecodedBytes'] or lengths[name] != descriptor['StoredBytes'] + 80:
                raise ValueError('Current payload includes unused bytes')
            block = virtual[name]; header, payload = block[:80], block[80:]
            if len(header) != 80 or header[:4] != b'SJB1' or struct.unpack('<q', header[4:12])[0] != block_id or struct.unpack('<i', header[12:16])[0] != descriptor['StoredBytes'] or header[16:48].hex() != root['IdentityHash']:
                raise ValueError('Current block has another owner, address or length')
            if hashlib.sha256(payload).digest() != header[48:80]:
                raise ValueError('Current block payload checksum differs')
            compressed += validate_payload(payload, end)
            origin = group_name(node['Entry']['Path']); group = groups[origin]
            size = descriptor['StoredBytes']
            group['payloadBytes'] += size; group['decodedPayloadBytes'] += end; group['metadataAndFrameBytes'] += 80
            stored += size; decoded += end
            if not node['Entry'].get('Deleted', False):
                group['retainedPayloadBytes'] += size; live_stored += size
                live_paths[node['Entry']['Path']] += end; live_groups[origin] += end
            else:
                unlinked += size
        if stored != root['PayloadBytes'] or decoded != root['LogicalBytes']:
            raise ValueError('Current payload totals differ from root')
        for group in groups.values():
            group['obsoletePayloadBytes'] = group['payloadBytes'] - group['retainedPayloadBytes']
        root_file.seek(0)
        if root_file.read() != root_bytes:
            raise ValueError('Current root changed during accounting')
        total = physical_bytes
        groups['other']['metadataAndFrameBytes'] += total - stored - sum(g['metadataAndFrameBytes'] for g in groups.values())
        return dict(version=2, journalFormat='SJL4', journalBytes=total, journalSha256=digest.hexdigest(), durableSequence=root['Sequence'],
            frameCount=0, currentNodeCount=len(nodes), currentPayloadBlockCount=len(block_refs), currentFragmentCount=fragments,
            groups=dict(groups), operations={}, storedPayloadBytes=stored, decodedPayloadBytes=decoded,
            livePrivateBytes=sum(live_groups.values()), liveStoredPayloadBytes=live_stored, obsoletePayloadBytes=stored-live_stored,
            unlinkedCurrentPayloadBytes=unlinked, compressedFrameCount=compressed, metadataAndFrameBytes=total-stored,
            unclassifiedBytes=0, livePayloadByCurrentGroup=dict(live_groups), largestWrittenPaths=[], largestLivePaths=live_paths.most_common(15),
            diagnosticReadBytes=total+stored, checksumsVerified=True, payloadContentsDecoded=False, payloadIncludesRepeatedWrites=False,
            fileDataAllocationMeasuredSeparately=True, obsoletePayloadIsNotGuaranteedCompactionSavings=True,
            qualifiedPathComparison='ASCII case-insensitive; non-ASCII rejected')


def analyze(path):
    with stopped_reader(path) as stream:
        if stream.read(4) == b'SJL4':
            return analyze_latest(path)
    groups = collections.defaultdict(lambda: dict(transactions=0, logicalMutations=0,
        payloadBytes=0, decodedPayloadBytes=0, metadataAndFrameBytes=0, retainedPayloadBytes=0))
    operations, written_paths = collections.Counter(), collections.Counter()
    nodes, paths, blocks = {}, {}, {}
    sequence = frames = compressed_frames = 0
    state_open = state_complete = False
    journal_hash = hashlib.sha256()
    with stopped_reader(path) as stream:
        initial = os.fstat(stream.fileno())
        while header := stream.read(16):
            if len(header) != 16:
                raise ValueError("Incomplete frame")
            if header[:4] != b'SJR2':
                raise ValueError("Unsupported journal format; preserve without migration")
            metadata_length, payload_length, decoded_length = struct.unpack("<iii", header[4:])
            if not 0 < metadata_length <= 65536 or not 0 <= payload_length <= decoded_length <= 1048576 or (payload_length == 0) != (decoded_length == 0):
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
            if payload_length < decoded_length:
                if len(payload) < 48 or payload[:4] != b'SPB1' or payload[12] != 1 or payload[13:16] != b'\0\0\0':
                    raise ValueError("Unsupported or corrupt payload block")
                declared, stored = struct.unpack('<ii', payload[4:12])
                if declared != decoded_length or not 0 < stored <= decoded_length - 64 or len(payload) != stored + 48:
                    raise ValueError("Invalid payload block lengths")
                if hashlib.sha256(payload[:-32]).digest() != payload[-32:]:
                    raise ValueError("Payload block checksum mismatch")
                compressed_frames += 1
            for part in (header, metadata, payload, checksum):
                journal_hash.update(part)
            mutation = json.loads(metadata)
            entry = mutation["Entry"]
            operation = mutation["Operation"]
            state = operation.startswith('state-')
            if state:
                if frames == 0 and operation == 'state-node':
                    state_open = True
                if not state_open or state_complete:
                    raise ValueError('Misplaced current-state prefix')
                logical_count = 0
                if operation == 'state-complete':
                    sequence = mutation['Sequence']; state_complete = True
            else:
                first = mutation.get("FromSequence") if operation == "skip-range" else mutation["Sequence"]
                if state_open and not state_complete or first != sequence + 1 or mutation["Sequence"] < first:
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
            group["decodedPayloadBytes"] += decoded_length
            group["metadataAndFrameBytes"] += metadata_length + 48
            written_paths[current_path] += payload_length
            operations[operation] += 1
            frames += 1
            if operation in ('state-hidden', 'state-change', 'state-complete', 'state-projection-unsafe'):
                if payload_length:
                    raise ValueError('Payload on current-state metadata')
                continue
            operation = {'state-node': 'metadata', 'state-write': 'write'}.get(operation, operation)
            if operation in ("skip-write", "skip-range"):
                if payload_length:
                    raise ValueError("Payload on skipped mutation")
                continue
            node_id = entry["Id"]
            node = nodes.setdefault(node_id, {"entry": entry, "extents": []})
            if len(nodes) > 250000:
                raise ValueError("Bounded node index exceeded")
            node["entry"] = entry.copy()
            if operation in ("write", "write-compacted"):
                fragments = (mutation.get("Fragments") if operation == "write-compacted" else
                             [{"Offset": mutation.get("Offset", 0), "Length": decoded_length}])
                if not fragments or sum(f["Length"] for f in fragments) != decoded_length:
                    raise ValueError("Invalid compacted payload length")
                blocks[payload_offset] = (payload_length, decoded_length, origin)
                block_offset = 0
                previous_end = -1
                for fragment in fragments:
                    offset, length = fragment["Offset"], fragment["Length"]
                    if length <= 0 or offset < 0 or offset < previous_end or offset + length > entry["Length"]:
                        raise ValueError("Invalid fragment range")
                    node["extents"] = replace_range(node["extents"], (offset, length, payload_offset, block_offset, origin))
                    block_offset += length
                    previous_end = offset + length
            elif operation == "resize":
                node["extents"] = [(s, min(n, entry["Length"] - s), p, o, g)
                    for s, n, p, o, g in node["extents"] if s < entry["Length"]]
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
            elif operation == "create":
                node["extents"].clear()
            elif operation != "metadata":
                raise ValueError("Unknown journal operation: " + operation)
            if operation not in ("write", "write-compacted") and decoded_length:
                raise ValueError("Payload on metadata mutation")
            if operation not in ("delete", "rename") and not entry.get("Deleted", False):
                paths[path_key] = node_id
        after = os.fstat(stream.fileno())
        if state_open and not state_complete:
            raise ValueError('Incomplete current-state prefix')
        if (after.st_size, after.st_mtime_ns) != (initial.st_size, initial.st_mtime_ns):
            raise ValueError("Journal changed during accounting")
    live_groups, live_paths = collections.Counter(), collections.Counter()
    block_ranges, referenced = [], set()
    for node_id in set(paths.values()):
        node = nodes[node_id]
        if node["entry"].get("Deleted", False):
            continue
        current_path = node["entry"]["Path"]
        for _, length, physical, offset, origin in node["extents"]:
            live_groups[group_name(current_path)] += length
            live_paths[current_path] += length
            stored, decoded, block_origin = blocks[physical]
            if offset < 0 or offset + length > decoded:
                raise ValueError("Invalid live block fragment")
            if physical not in referenced:
                groups[block_origin]["retainedPayloadBytes"] += stored
                referenced.add(physical)
            block_ranges.append((physical, offset, length))
    block_ranges.sort()
    if any(a[0] == b[0] and a[1] + a[2] > b[1] for a, b in zip(block_ranges, block_ranges[1:])):
        raise ValueError("Overlapping live logical block fragments")
    for group in groups.values():
        group["obsoletePayloadBytes"] = group["payloadBytes"] - group["retainedPayloadBytes"]
        if group["obsoletePayloadBytes"] < 0:
            raise ValueError("Invalid payload accounting")
    total_payload = sum(g["payloadBytes"] for g in groups.values())
    overhead = sum(g["metadataAndFrameBytes"] for g in groups.values())
    if total_payload + overhead != initial.st_size:
        raise ValueError("Unaccounted journal bytes")
    live_stored = sum(blocks[physical][0] for physical in referenced)
    return dict(version=2, journalFormat='SJR2', journalBytes=initial.st_size, journalSha256=journal_hash.hexdigest(),
        durableSequence=sequence, frameCount=frames, groups=dict(groups), operations=dict(operations),
        storedPayloadBytes=total_payload, decodedPayloadBytes=sum(g['decodedPayloadBytes'] for g in groups.values()),
        livePrivateBytes=sum(live_groups.values()), liveStoredPayloadBytes=live_stored,
        obsoletePayloadBytes=total_payload-live_stored, compressedFrameCount=compressed_frames,
        metadataAndFrameBytes=overhead, unclassifiedBytes=0, livePayloadByCurrentGroup=dict(live_groups),
        largestWrittenPaths=written_paths.most_common(15), largestLivePaths=live_paths.most_common(15),
        diagnosticReadBytes=initial.st_size, checksumsVerified=True, payloadContentsDecoded=False, payloadIncludesRepeatedWrites=True,
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
                      ("journalBytes", "durableSequence", "livePrivateBytes", "liveStoredPayloadBytes", "obsoletePayloadBytes")}))


if __name__ == "__main__":
    main()
