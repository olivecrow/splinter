"""Replay one preserved tiny Unity Bee input without starting an Editor.

Graph-only pilot: --rounds 1 --order overlay native-bee --budget-seconds 180.
Default ABBA budget: 300 seconds. Live children and failed outputs are preserved.
The native graft fails actual Bee atomic state publication (Win32 error 17).
--compile retains that diagnostic path; it is not a supported native build workflow.
"""
import argparse
from contextlib import ExitStack
import ctypes
import hashlib
import json
import os
from pathlib import Path, PureWindowsPath
import subprocess
import time
import uuid


REPO = Path(__file__).resolve().parents[1]
SEED_RELATIVE = "artifacts/startup-read-followup-20261008/unity-probe"
INPUT_RELATIVE = "Library/Bee/1900b0aE-inputdata.json"
SEED_HASHES = {
    INPUT_RELATIVE: "80af13551306ef2aa6cf43ba11b2ec4cf98951ed19447e6c1e55cb33d6a413d3",
    "Assets/Editor/StartupProbe.cs": "45e1599deaa74e6bb6cef0c208321e6da3da702b0d05f24428e4be62f5d2fb21",
    "Assets/RuntimeSentinel.cs": "24e92a12b499761583a60947225c56f65af5a5e5cb1b01df9ee49cfff99a56e1",
    "Assets/__Splinter__/Editor/WorkspaceBridge.cs": "62697e223b1e62ca437ef7eddbc3f0bf3550697c2a2bd297aed2aae22c5cfb29",
    "Assets/__Splinter__/Editor/Splinter.Workspace.Editor.asmdef": "3f091dfbd0e72be751a2a7901d3e21a392b393c8fe2797c692cb95580919b9e7",
}
ASSEMBLIES = {"Assembly-CSharp", "Assembly-CSharp-Editor", "Splinter.Workspace.Editor"}
GRAPH_OUTPUT_KEYS = (
    "StructuredLogFileName", "StateFileName", "StateFileNameTmp", "StateFileNameMapped",
    "ScanCacheFileName", "ScanCacheFileNameTmp", "DigestCacheFileName", "DigestCacheFileNameTmp",
    "CachedNodeOutputDirectoryName", "PayloadsFile", "Identifier",
)
NATIVE_DURABILITY = (
    "Native Bee uses ordinary NTFS buffered durability, not SJL4 per-write durable ACKs. "
    "Source and other Library paths retain the range journal. These component timings "
    "do not establish equal-durability or complete Unity performance."
)
ALLOCATION_BOUNDARY = (
    "Final retained regular-file allocation after normal provider exit, not peak: only "
    "this run's delta and native-bee directories. Shared fixture, tools, source export, "
    "receipts, measurement copies and filesystem directory metadata are excluded."
)
MAPPING_CLEANUP_BOUNDARY = (
    "Explicit cleanup of this fixed Bee fixture's Library/Bee/1900b0aE.dag_derived only, "
    "after every known client exits: OPEN_EXISTING FILE_WRITE_DATA|READ_ATTRIBUTES, then "
    "close, without data/metadata writes, flush, rename or deletion. Both variants use "
    "the same procedure. This is not a product filename rule, power-loss qualification "
    "or proof that foreign data mappings are absent."
)


def read(path):
    return json.loads(Path(path).read_text(encoding="utf-8-sig"))


def save(path, value):
    with Path(path).open("x", encoding="utf-8") as stream:
        json.dump(value, stream, indent=2, ensure_ascii=False)


def sha(path):
    with Path(path).open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def digest(data):
    return hashlib.sha256(data).hexdigest()


def no_reparse_ancestors(path):
    for candidate in [path, *path.parents]:
        try:
            info = candidate.lstat()
        except FileNotFoundError:
            continue
        if candidate.is_symlink() or getattr(info, "st_file_attributes", 0) & 0x400:
            raise ValueError("Reparse input/output ancestor is not permitted: " + str(candidate))


def inventory(root):
    result = {}
    for path in sorted(root.rglob("*")):
        if path.is_symlink() or getattr(path.lstat(), "st_file_attributes", 0) & 0x400:
            raise ValueError("Unexpected reparse entry: " + str(path))
        if path.is_file():
            result[path.relative_to(root).as_posix()] = sha(path)
    return result


def replace_paths(value, old, new):
    if isinstance(value, str):
        return value.replace(str(old).replace("\\", "/"), str(new).replace("\\", "/")).replace(
            str(old).replace("/", "\\"), str(new).replace("/", "\\"))
    if isinstance(value, list):
        return [replace_paths(item, old, new) for item in value]
    if isinstance(value, dict):
        return {key: replace_paths(item, old, new) for key, item in value.items()}
    return value


def view_output(value, view):
    """Check the logical namespace without resolving the intentional Bee graft."""
    if not isinstance(value, str) or not value:
        raise ValueError("Missing write-bearing path.")
    parts = PureWindowsPath(value)
    components = parts.parts[1:] if parts.anchor else parts.parts
    if ".." in parts.parts or any(":" in part for part in components):
        raise ValueError("Parent traversal/stream in output: " + value)
    if parts.drive and not parts.is_absolute():
        raise ValueError("Drive-relative output: " + value)
    if parts.root and not parts.drive:
        raise ValueError("Root-relative output: " + value)
    path = Path(os.path.abspath(value if parts.is_absolute() else view / value))
    if not path.is_relative_to(view / "Library"):
        raise ValueError("Output outside this view's Library: " + value)
    return path


def validate_recipe(recipe, view):
    config = recipe["BeeBuildProgramCommon.Data.ConfigurationData"]
    compilation = recipe["ScriptCompilationBuildProgram.Data.ScriptCompilationData"]
    if config["UnityVersion"] != "6000.3.22f1" or config["Packages"] or config["Batchmode"] is not True:
        raise ValueError("Only the fixed zero-package Unity 6000.3.22f1 input is allowed.")
    if len(compilation["Assemblies"]) != 3 or {a["Name"] for a in compilation["Assemblies"]} != ASSEMBLIES:
        raise ValueError("Unexpected assembly selection.")
    selected = set()
    for assembly in compilation["Assemblies"]:
        if Path(assembly["Path"]) != view:
            raise ValueError("Assembly.Path does not name this view.")
        selected.update(assembly["SourceFiles"])
        if assembly.get("Asmdef"):
            selected.add(assembly["Asmdef"])
    if selected != set(SEED_HASHES) - {INPUT_RELATIVE}:
        raise ValueError("Unexpected source/asmdef input.")
    for key, expected in (("OutputDirectory", "Library/ScriptAssemblies"),
                          ("BuildPlayerDataOutput", "Library/BuildPlayerData/Editor")):
        if compilation[key] != expected:
            raise ValueError("Unexpected write-bearing recipe field: " + key)
        view_output(compilation[key], view)


def bounded_bytes(path, limit=16 * 1024 * 1024):
    with Path(path).open("rb") as stream:
        data = stream.read(limit + 1)
    if len(data) > limit:
        raise ValueError("Fixture output exceeds its bound: " + str(path))
    return data


def capture(path, destination):
    data = bounded_bytes(path)
    with destination.open("xb") as stream:
        stream.write(data)
    return data


def capture_if_present(path, destination):
    if path.is_file():
        capture(path, destination)


def semantic_graph(view, evidence, profile):
    # Preserve bytes before parsing, including unexpected or malformed output.
    raw = capture(view / "Library/Bee/1900b0aE.dag.json", evidence / "dag.json")
    payload = capture(view / "Library/Bee/1900b0aE.dag.payloads", evidence / "dag.payloads")
    capture(view / INPUT_RELATIVE, evidence / "inputdata.json")
    capture_if_present(view / profile, evidence / "graph.traceevents")
    graph = json.loads(raw.decode("utf-8-sig"))
    if len(graph["Nodes"]) != 167:
        raise ValueError("Fixed three-assembly fixture produced an unexpected graph size.")
    for key in GRAPH_OUTPUT_KEYS:
        view_output(graph[key], view)
    if graph["RelativePathToRoot"] != "../.." or "ScriptAssemblies" not in graph["NamedNodes"]:
        raise ValueError("Unexpected Bee graph root or target.")
    ranges = []
    for node in graph["Nodes"]:
        for output in node.get("Outputs", []):
            view_output(output, view)
        if "ILPostProcess" in node.get("Annotation", "") or "ILPP" in node.get("Annotation", ""):
            raise ValueError("Standalone fixture must not require an ILPP server.")
        if "PayloadOffset" in node or "PayloadLength" in node:
            start, length = node["PayloadOffset"], node["PayloadLength"]
            if type(start) is not int or type(length) is not int or start < 0 or length < 0 or start + length > len(payload):
                raise ValueError("Invalid Bee payload slice.")
            if node.get("ActionType") != "WriteFile":
                raise ValueError("Unknown payload action in the fixed fixture.")
            ranges.append((start, start + length))
            normalized = replace_paths(payload[start:start + length].decode("utf-8"), view, "<workspace>").encode("utf-8")
            node["NormalizedPayloadSha256"] = digest(normalized)
            node["NormalizedPayloadBytes"] = len(normalized)
            for key in ("PayloadOffset", "PayloadLength", "PayloadDebugContentSnippet"):
                node.pop(key, None)
    nonempty = sorted((a, b) for a, b in ranges if a != b)
    if any(left[1] > right[0] for left, right in zip(nonempty, nonempty[1:])):
        raise ValueError("Overlapping Bee payload slices.")
    normalized = replace_paths(graph, view, "<workspace>")
    save(evidence / "semantic-graph.json", normalized)
    encoded = json.dumps(normalized, sort_keys=True, separators=(",", ":")).encode()
    return digest(encoded), normalized


class Budget:
    def __init__(self, seconds):
        self.started = time.monotonic()
        self.deadline = self.started + seconds

    def remaining(self, cap, reserve=0):
        seconds = min(cap, self.deadline - time.monotonic() - reserve)
        if seconds <= 0:
            raise TimeoutError("Whole-run budget exhausted; preserve owned outputs and live children.")
        return seconds


def wait_json(path, child, budget, seconds=45, reserve=0):
    deadline = time.monotonic() + budget.remaining(seconds, reserve)
    while time.monotonic() < deadline:
        if path.exists():
            try:
                return read(path)
            except (PermissionError, json.JSONDecodeError):
                pass
        if child.poll() is not None:
            raise RuntimeError(f"Provider exited {child.returncode} before {path}")
        time.sleep(.05)
    raise TimeoutError(f"Preserve provider PID {child.pid} and {path.parent}")


def start(args, **kwargs):
    return subprocess.Popen(args, creationflags=subprocess.CREATE_NO_WINDOW, **kwargs)


def run_client(command, view, label, evidence, clients, budget, limit):
    allowance = budget.remaining(limit, reserve=20)
    save(evidence / (label + "-request.json"), dict(command=[str(x) for x in command], cwd=str(view), waitLimitSeconds=allowance))
    with (evidence / (label + ".stdout")).open("xb") as stdout, (evidence / (label + ".stderr")).open("xb") as stderr:
        began = time.perf_counter()
        child = start(command, cwd=view, stdout=stdout, stderr=stderr)
        clients.append((label, child))
        try:
            code = child.wait(timeout=allowance)
        except subprocess.TimeoutExpired as error:
            raise TimeoutError(f"Preserve live {label} PID {child.pid}; no stop/unmount requested.") from error
        seconds = time.perf_counter() - began
    save(evidence / (label + "-process.json"), dict(command=[str(x) for x in command], cwd=str(view),
         processId=child.pid, exitCode=code, seconds=seconds))
    if code:
        raise RuntimeError(f"{label} exited {code}; stdout/stderr and provider preserved.")
    return seconds


def source_file_records(records, expected, basis):
    actual = {}
    for record in records:
        path = record["path"]
        if path in actual or path not in expected or record.get("length") != (basis / path).stat().st_size:
            raise ValueError("Unexpected/duplicate provider basis file.")
        actual[path] = record["sha256"]
    if actual != expected:
        raise ValueError("Provider basis file hashes do not match the frozen fixture.")


def validate_ready(ready, nonce, mode, root, provider, before, basis):
    if (ready.get("nonce") != nonce or ready.get("mode") != mode or
            ready.get("processId") != provider.pid or Path(ready["root"]) != root or
            Path(ready["view"]) != root / "view"):
        raise ValueError("Provider ready identity/path mismatch.")
    native = ready.get("nativeBee")
    if (mode == "native-bee" and (not native or Path(native) != root / "native-bee" or not ready.get("nativeTargetIdentity"))) or (mode == "overlay" and native is not None):
        raise ValueError("Provider native target mismatch.")
    source_file_records(ready["sourceFiles"], before, basis)


def cleanup_fixed_bee_mapping(root, nonce, mode, ready, provider, clients, basis, before, request, evidence, budget):
    """Bounded fixture procedure based on preserved recovery-pilot02 evidence."""
    began = time.perf_counter()
    evidence.mkdir()
    kernel = ctypes.WinDLL("kernel32", use_last_error=True)
    handle_type, uint = ctypes.c_void_p, ctypes.c_uint32

    class FileTime(ctypes.Structure):
        _fields_ = [("low", uint), ("high", uint)]

    class FileInfo(ctypes.Structure):
        _fields_ = [("attrs", uint), ("creation", FileTime), ("access", FileTime),
                    ("write", FileTime), ("volume", uint), ("sizeHigh", uint),
                    ("sizeLow", uint), ("links", uint), ("indexHigh", uint), ("indexLow", uint)]

    signatures = {
        "CreateFileW": ([ctypes.c_wchar_p, uint, uint, handle_type, uint, uint, handle_type], handle_type),
        "CloseHandle": ([handle_type], ctypes.c_int),
        "GetFileInformationByHandle": ([handle_type, ctypes.POINTER(FileInfo)], ctypes.c_int),
        "GetFinalPathNameByHandleW": ([handle_type, ctypes.c_wchar_p, uint, uint], uint),
        "GetVolumeInformationByHandleW": ([handle_type, ctypes.c_wchar_p, uint, ctypes.POINTER(uint),
            ctypes.POINTER(uint), ctypes.POINTER(uint), ctypes.c_wchar_p, uint], ctypes.c_int),
        "OpenProcess": ([uint, ctypes.c_int, uint], handle_type),
        "GetProcessTimes": ([handle_type, *([ctypes.POINTER(FileTime)] * 4)], ctypes.c_int),
        "GetExitCodeProcess": ([handle_type, ctypes.POINTER(uint)], ctypes.c_int),
    }
    for name, (arguments, result) in signatures.items():
        getattr(kernel, name).argtypes = arguments
        getattr(kernel, name).restype = result

    def close(handle):
        if not kernel.CloseHandle(handle):
            raise ctypes.WinError(ctypes.get_last_error())

    def opened(path, access, flags=0, sharing=7):
        handle = kernel.CreateFileW(str(path), access, sharing, None, 3, flags, None)
        if handle == ctypes.c_void_p(-1).value:
            raise ctypes.WinError(ctypes.get_last_error())
        return handle

    def info(handle):
        value = FileInfo()
        if not kernel.GetFileInformationByHandle(handle, ctypes.byref(value)):
            raise ctypes.WinError(ctypes.get_last_error())
        return dict(attrs=value.attrs, creation=(value.creation.high << 32) | value.creation.low,
                    access=(value.access.high << 32) | value.access.low,
                    write=(value.write.high << 32) | value.write.low, volume=value.volume,
                    size=(value.sizeHigh << 32) | value.sizeLow, links=value.links,
                    index=(value.indexHigh << 32) | value.indexLow)

    def native_path(handle):
        buffer = ctypes.create_unicode_buffer(32768)
        length = kernel.GetFinalPathNameByHandleW(handle, buffer, len(buffer), 2)
        if length == 0 or length >= len(buffer):
            raise ctypes.WinError(ctypes.get_last_error())
        return buffer.value.rstrip("\\")

    def inspect_file(path):
        handle = opened(path, 0x80)
        try:
            return info(handle), native_path(handle)
        finally:
            close(handle)

    def volume(handle):
        fs = ctypes.create_unicode_buffer(64)
        serial, maximum, flags = uint(), uint(), uint()
        if not kernel.GetVolumeInformationByHandleW(handle, None, 0, ctypes.byref(serial),
                ctypes.byref(maximum), ctypes.byref(flags), fs, len(fs)):
            raise ctypes.WinError(ctypes.get_last_error())
        return fs.value, serial.value

    def verify_lifetime(process_handle):
        if provider.poll() is not None or any(child.poll() is None for _, child in clients):
            raise ValueError("Mapping cleanup requires the original live provider and all known clients exited.")
        times = [FileTime() for _ in range(4)]
        status = uint()
        if not kernel.GetProcessTimes(process_handle, *[ctypes.byref(value) for value in times]):
            raise ctypes.WinError(ctypes.get_last_error())
        if not kernel.GetExitCodeProcess(process_handle, ctypes.byref(status)):
            raise ctypes.WinError(ctypes.get_last_error())
        ticks = ((times[0].high << 32) | times[0].low) + 504911232000000000
        if ticks != ready["processStartTimeUtcTicks"] or status.value != 259:
            raise ValueError("Provider process generation changed or exited.")
        if read(root / "ready.json") != ready or read(request) != dict(root=str(root), basis=str(basis), mode=mode, nonce=nonce):
            raise ValueError("Mapping cleanup request/ready identity changed.")

    def physical_inventory(directory):
        if not directory.exists():
            return dict(exists=False, directories=[], files=[])
        no_reparse_ancestors(directory)
        directories, files = [], []
        for path in sorted(directory.rglob("*")):
            budget.remaining(1, reserve=20)
            no_reparse_ancestors(path)
            relative = path.relative_to(directory).as_posix()
            if path.is_dir():
                directories.append(relative)
                continue
            handle = opened(path, 0x80)
            try:
                metadata = info(handle)
                if metadata["links"] != 1 or metadata["attrs"] & 0x410:
                    raise ValueError("Unsupported mapping-cleanup inventory entry: " + str(path))
                file_hash = sha(path)
                if info(handle) != metadata:
                    raise ValueError("Inventory metadata changed while reading: " + str(path))
            finally:
                close(handle)
            files.append(dict(path=relative, metadata=metadata, sha256=file_hash))
        return dict(exists=True, directories=directories, files=files)

    def protected_inventory():
        return {name: physical_inventory(path) for name, path in (
            ("basis", basis), ("delta", root / "delta"), ("existingSourceResult", root / "source-result"),
            ("nativeBee", root / "native-bee"))}

    operation = dict(boundary=MAPPING_CLEANUP_BOUNDARY, processId=provider.pid,
                     processStartTimeUtcTicks=ready["processStartTimeUtcTicks"], nonce=nonce, mode=mode,
                     knownClients=[dict(stage=name, processId=child.pid, exitCode=child.poll()) for name, child in clients])
    try:
        budget.remaining(1, reserve=20)
        validate_ready(ready, nonce, mode, root, provider, before, basis)
        if (root / "stop.json").exists():
            raise ValueError("Mapping cleanup must precede this run's normal stop request.")
        with ExitStack() as lifetime:
            process_handle = kernel.OpenProcess(0x1000, False, provider.pid)
            if not process_handle:
                raise ctypes.WinError(ctypes.get_last_error())
            lifetime.callback(close, process_handle)
            verify_lifetime(process_handle)
            # Retain directory handles without delete sharing until all checks finish.
            view = root / "view"
            root_handle = opened(view, 0x81, 0x02000000, sharing=3)
            lifetime.callback(close, root_handle)
            root_info, root_native = info(root_handle), native_path(root_handle)
            expected_serial = int.from_bytes(hashlib.sha256(("bee-probe-" + nonce).encode()).digest()[:4], "little")
            expected_root_index = int.from_bytes(hashlib.sha256(("bee-probe-" + nonce + "/").encode()).digest()[:8], "little")
            if (volume(root_handle) != ("SplinterSO", expected_serial) or root_info["index"] != expected_root_index or
                    root_info["attrs"] & 0x410 != 0x10 or not root_native.startswith("\\Device\\Volume{")):
                raise ValueError("Mounted fixture root identity differs.")
            relative = "Library/Bee/1900b0aE.dag_derived"
            path = view / relative
            expected_native = root_native + "\\" + relative.replace("/", "\\")
            expected_volume = expected_serial
            native_root_handle, native_root_info = None, None
            if mode == "native-bee":
                native_root = root / "native-bee"
                no_reparse_ancestors(native_root)
                native_root_handle = opened(native_root, 0x81, 0x02000000, sharing=3)
                lifetime.callback(close, native_root_handle)
                native_root_info = info(native_root_handle)
                target = ready["nativeTargetIdentity"]
                if (volume(native_root_handle) != ("NTFS", target["volumeSerial"]) or
                        native_root_info["index"] != target["fileIndex"] or native_root_info["attrs"] & 0x410 != 0x10):
                    raise ValueError("Owned native Bee target identity differs.")
                expected_native = native_path(native_root_handle) + "\\1900b0aE.dag_derived"
                expected_volume = target["volumeSerial"]
            # Match the validated recovery: no extra leaf handle stays open during
            # the write-access open/close. Verify the exact identity on every open;
            # retained process and directory handles protect the owned lifetime.
            before_info, before_native = inspect_file(path)
            if (before_info["attrs"] & 0x410 or before_info["links"] != 1 or
                    not 0 < before_info["size"] <= 16 * 1024 * 1024 or before_info["volume"] != expected_volume or
                    before_native.casefold() != expected_native.casefold()):
                raise ValueError("Fixed Bee mapping file identity/path differs.")
            before_hash = digest(bounded_bytes(path))
            before_inventory = protected_inventory()
            if inventory(basis) != before or inspect_file(path) != (before_info, before_native):
                raise ValueError("Protected input or mapping metadata changed before cleanup.")
            save(evidence / "before.json", dict(**operation, rootIdentity=root_info, nativeRoot=root_native,
                 nativeBeeRootIdentity=native_root_info, path=str(path), fileNativePath=before_native,
                 metadata=before_info, sha256=before_hash, inventory=before_inventory))
            verify_lifetime(process_handle)
            tick = time.perf_counter()
            handle = opened(path, 0x82)
            try:
                if info(handle) != before_info or native_path(handle) != before_native:
                    raise ValueError("Existing write-access open resolved a different file.")
            finally:
                close(handle)
            operation["existingFileOpenCloseSeconds"] = time.perf_counter() - tick
            operation["openSucceeded"] = True
            after_hash = digest(bounded_bytes(path))
            after_inventory = protected_inventory()
            if (inspect_file(path) != (before_info, before_native) or after_hash != before_hash or
                    info(root_handle) != root_info or after_inventory != before_inventory or
                    (native_root_handle is not None and info(native_root_handle) != native_root_info)):
                raise ValueError("Mapping cleanup changed protected identity, metadata, content or inventory.")
            verify_lifetime(process_handle)
            operation.update(contentAndMetadataUnchanged=True, protectedInventoriesUnchanged=True,
                             providerGenerationUnchanged=True, inventory=after_inventory)
        # All owned proof handles are closed before the caller requests normal stop.
        operation["mappingCleanupSeconds"] = time.perf_counter() - began
        operation["proofHandlesClosed"] = True
        save(evidence / "result.json", operation)
        return {**{key: value for key, value in operation.items() if key != "inventory"}, "evidence": str(evidence)}
    except BaseException as error:
        save(evidence / "failure.json", dict(**operation, errorType=type(error).__name__, error=str(error),
             elapsedSeconds=time.perf_counter() - began, outputsPreserved=True))
        raise


def publish_stop(root, nonce):
    path = root / "stop.json"
    if path.exists():
        if read(path) != {"nonce": nonce}:
            raise ValueError("Existing stop request differs; preserve provider.")
        return
    temporary = root / ("stop-" + uuid.uuid4().hex + ".tmp")
    save(temporary, dict(nonce=nonce))
    temporary.rename(path)


def validate_result(result, nonce, mode, root, before, basis, expected_sources):
    if (result.get("nonce") != nonce or result.get("mode") != mode or
            result.get("sourceUnchanged") is not True or result.get("normalUnmount") is not True or result.get("passed") is not True or
            result.get("openHandles") != 0 or result.get("retainedFileContexts") != 0 or
            result.get("errors") != [] or result.get("nativeOutputsPreserved") != (mode == "native-bee")):
        raise ValueError("Provider result did not satisfy identity/source/normal-closure/error gates.")
    source_file_records(result["sourceFiles"], before, basis)
    records = {}
    for item in result["independentSourceResults"]:
        path = item["path"]
        if path in records or path not in expected_sources or item.get("deleted") or item.get("directory"):
            raise ValueError("Unexpected independent source result.")
        data = expected_sources[path]
        if item.get("length") != len(data) or item.get("sha256") != digest(data):
            raise ValueError("Independent source receipt differs from requested bytes.")
        file = root / "source-result" / path
        no_reparse_ancestors(file)
        if bounded_bytes(file) != data:
            raise ValueError("Independent source file differs from requested bytes.")
        records[path] = item["sha256"]
    if set(records) != set(expected_sources) or inventory(root / "source-result") != records:
        raise ValueError("Independent source result inventory differs.")
    if inventory(basis) != before:
        raise ValueError("Immutable basis changed.")


def final_allocation(root):
    kernel = ctypes.WinDLL("kernel32", use_last_error=True)
    class StandardInfo(ctypes.Structure):
        _fields_ = [("allocation", ctypes.c_int64), ("end", ctypes.c_int64),
                    ("links", ctypes.c_uint32), ("delete_pending", ctypes.c_ubyte),
                    ("directory", ctypes.c_ubyte)]
    create = kernel.CreateFileW
    create.argtypes = [ctypes.c_wchar_p, ctypes.c_uint32, ctypes.c_uint32,
                       ctypes.c_void_p, ctypes.c_uint32, ctypes.c_uint32, ctypes.c_void_p]
    create.restype = ctypes.c_void_p
    measure = kernel.GetFileInformationByHandleEx
    measure.argtypes = [ctypes.c_void_p, ctypes.c_int, ctypes.c_void_p, ctypes.c_uint32]
    measure.restype = ctypes.c_int
    close = kernel.CloseHandle
    close.argtypes = [ctypes.c_void_p]
    close.restype = ctypes.c_int
    rows = []
    for name in ("delta", "native-bee"):
        directory = root / name
        if not directory.exists():
            if name == "delta":
                raise ValueError("Expected stopped delta directory is missing.")
            continue
        no_reparse_ancestors(directory)
        logical = allocated = count = 0
        for path in directory.rglob("*"):
            no_reparse_ancestors(path)
            if not path.is_file():
                continue
            info = path.stat()
            if info.st_nlink != 1:
                raise ValueError("Hardlinked final allocation input: " + str(path))
            handle = create(str(path), 0x80, 7, None, 3, 0x00200000, None)
            if handle == ctypes.c_void_p(-1).value:
                raise ctypes.WinError(ctypes.get_last_error())
            try:
                standard = StandardInfo()
                if not measure(handle, 1, ctypes.byref(standard), ctypes.sizeof(standard)):
                    raise ctypes.WinError(ctypes.get_last_error())
                if (standard.links != 1 or standard.directory or standard.delete_pending or
                        standard.end != info.st_size or standard.allocation < 0):
                    raise ValueError("Unsupported or changed allocation input: " + str(path))
                logical += standard.end
                allocated += standard.allocation
            finally:
                close(handle)
            count += 1
        rows.append(dict(directory=name, files=count, logicalBytes=logical, allocatedBytes=allocated))
    return dict(directories=rows, logicalBytes=sum(r["logicalBytes"] for r in rows),
                allocatedBytes=sum(r["allocatedBytes"] for r in rows), boundary=ALLOCATION_BOUNDARY,
                method="GetFileInformationByHandleEx(FileStandardInfo).AllocationSize",
                peakTemporaryAllocation="NotMeasured")


def fail_preserving(error, root, nonce, mode, provider, ready_valid, clients, budget, before, basis, expected_sources):
    failure = dict(errorType=type(error).__name__, error=str(error), root=str(root), nonce=nonce,
                   providerPid=provider.pid if provider else None,
                   clients=[dict(stage=name, processId=p.pid, exitCode=p.poll()) for name, p in clients],
                   completedSourceWrites={p: digest(b) for p, b in expected_sources.items()},
                   forcedTermination=False, filesDeleted=False)
    alive = [p.pid for _, p in clients if p.poll() is None]
    if alive:
        failure["normalStop"] = "NotRequested: known clients still alive"
    elif provider is not None and provider.poll() is None and ready_valid:
        try:
            publish_stop(root, nonce)
            failure["normalStop"] = "Requested with matching nonce after all known clients exited"
            result = wait_json(root / "result.json", provider, budget, seconds=20)
            code = provider.wait(timeout=budget.remaining(5))
            validate_result(result, nonce, mode, root, before, basis, expected_sources)
            if code != 0:
                raise RuntimeError("Provider exit code after failure: " + str(code))
            failure["normalStop"] = "Completed and verified; experiment remains failed"
        except Exception as stop_error:
            failure["stopError"] = repr(stop_error)
    else:
        failure["normalStop"] = "NotRequested: provider absent/exited or ready identity unverified"
    if provider is not None:
        failure["providerExitCode"] = provider.poll()
    save(root.parent / (root.name + "-failure.json"), failure)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--probe", type=Path, required=True)
    parser.add_argument("--seed", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--rounds", type=int, default=3)
    parser.add_argument("--budget-seconds", type=float, default=300)
    parser.add_argument("--compile", action="store_true", help="Diagnostic only: native-bee currently fails atomic state rename (Win32 17). Overlay can build empty/unchanged/one-change inputs; no ILPP.")
    parser.add_argument("--order", nargs="+", choices=["overlay", "native-bee"], default=["overlay", "native-bee", "native-bee", "overlay"])
    args = parser.parse_args()
    if not 1 <= args.rounds <= 6 or not 1 <= len(args.order) <= 4 or not 30 <= args.budget_seconds <= 300:
        raise ValueError("Allow 1-6 graph rounds, 1-4 mounts and a 30-300 second whole-run budget.")
    budget = Budget(args.budget_seconds)
    for path in (args.seed.absolute(), args.output.absolute(), args.probe.absolute()):
        no_reparse_ancestors(path)
    output, seed, probe = args.output.resolve(), args.seed.resolve(), args.probe.resolve()
    if seed != (REPO / SEED_RELATIVE).resolve():
        raise ValueError("Only the explicitly preserved tiny fixture is authorized.")
    if not any(output.is_relative_to(REPO / name) and output != REPO / name for name in ("artifacts", ".local")):
        raise ValueError("Output must be a new directory below this repository's artifacts or .local.")
    if output.is_relative_to(seed) or seed.is_relative_to(output) or output.exists():
        raise ValueError("Output must be new and disjoint from the historical fixture.")
    for relative, expected in SEED_HASHES.items():
        path = seed / relative
        no_reparse_ancestors(path)
        if sha(path) != expected:
            raise ValueError("Frozen historical fixture differs: " + relative)
    original_input = read(seed / INPUT_RELATIVE)
    validate_recipe(original_input, seed)
    config = original_input["BeeBuildProgramCommon.Data.ConfigurationData"]
    tool = Path(config["EditorBuildPipelinePath"]) / "ScriptCompilationBuildProgram.exe"
    launcher = Path(config["NetCoreRunPath"])
    backend = tool.parent / "bee_backend.exe"
    identities = {str(path): sha(path) for path in {probe, launcher, tool, backend, Path(__file__).resolve()}}
    for path in probe.parent.iterdir():
        if path.is_file() and path.suffix.lower() in {".dll", ".json", ".exe"}:
            identities[str(path)] = sha(path)
    budget.remaining(1, reserve=20)
    output.mkdir(parents=True, exist_ok=False)
    basis = output / "basis"
    (basis / "Library/Bee").mkdir(parents=True)
    for relative in sorted(set(SEED_HASHES) - {INPUT_RELATIVE}):
        destination = basis / relative
        destination.parent.mkdir(parents=True, exist_ok=True)
        destination.write_bytes((seed / relative).read_bytes())
    (basis / "Assets/prototype-sentinel.txt").write_bytes(b"immutable-source\n")
    before = inventory(basis)
    save(output / "input.json", dict(scope="Fixed real three-assembly Bee graph/optional Csc; no Editor, ILPP or Play",
         seed=str(seed), seedFilesSha256=SEED_HASHES, basisFilesSha256=before,
         copiedFixtureBytes=sum((basis / p).stat().st_size for p in before), runtimeFilesSha256=identities,
         order=args.order, rounds=args.rounds, budgetSeconds=args.budget_seconds, osCacheReset=False,
         compilerServerReset=False,
         backendInvocation="Standalone explicit DAG; no parent IPC or deferred stdin gate",
         coldDefinition="Empty per-mount generated outputs; OS and compiler server caches are not reset",
         nativeDurability=NATIVE_DURABILITY, allocationBoundary=ALLOCATION_BOUNDARY,
         mappingCleanupBoundary=MAPPING_CLEANUP_BOUNDARY))
    rows, canonical = [], None
    for index, mode in enumerate(args.order):
        root = output / f"run-{index}-{mode}"
        nonce = uuid.uuid4().hex
        request = output / f"request-{index}.json"
        save(request, dict(root=str(root), basis=str(basis), mode=mode, nonce=nonce))
        provider, ready_valid, clients, expected_sources = None, False, [], {}
        with (output / f"provider-{index}.stdout").open("xb") as stdout, (output / f"provider-{index}.stderr").open("xb") as stderr:
            try:
                budget.remaining(1, reserve=20)
                began = time.perf_counter()
                provider = start(["dotnet", str(probe), "serve", "--input", str(request)], cwd=REPO, stdout=stdout, stderr=stderr)
                ready = wait_json(root / "ready.json", provider, budget, reserve=20)
                prepare_seconds = time.perf_counter() - began
                validate_ready(ready, nonce, mode, root, provider, before, basis)
                ready_valid = True
                view = root / "view"
                remapped = replace_paths(original_input, seed, view)
                validate_recipe(remapped, view)
                tick = time.perf_counter()
                (view / INPUT_RELATIVE).write_text(json.dumps(remapped, separators=(",", ":")), encoding="utf-8")
                input_seconds = time.perf_counter() - tick
                costs, builds = [], []
                for iteration in range(args.rounds):
                    evidence = output / f"graph-{index}-{iteration}"
                    evidence.mkdir()
                    profile = f"Library/Bee/buildprogram-{iteration}.traceevents"
                    seconds = run_client([str(launcher), str(tool), "Library/Bee/1900b0aE.dag.json", INPUT_RELATIVE, profile],
                                         view, "graph", evidence, clients, budget, 30)
                    graph_digest, graph_data = semantic_graph(view, evidence, profile)
                    if canonical is None:
                        canonical = graph_digest
                        save(output / "canonical-graph.json", graph_data)
                    elif graph_digest != canonical:
                        raise ValueError(f"Semantic graph/payload differs; preserve evidence {evidence}")
                    costs.append(dict(iteration=iteration, phase="empty-Bee-output" if iteration == 0 else "graph-rewrite",
                                      seconds=seconds, normalizedGraphSha256=graph_digest, evidence=str(evidence)))
                    print(json.dumps(dict(mode=mode, run=index, **costs[-1])), flush=True)
                if args.compile:
                    for phase, expected in [("empty-compile-output", 42), ("unchanged", 42), ("incremental", 43)]:
                        incremental_source_edit_seconds = 0
                        if phase == "incremental":
                            edit_began = time.perf_counter()
                            sentinel = view / "Assets/RuntimeSentinel.cs"
                            prior = bounded_bytes(sentinel)
                            if prior.count(b"Value = 42") != 1:
                                raise ValueError("Unexpected sentinel input.")
                            changed = prior.replace(b"Value = 42", b"Value = 43")
                            sentinel.write_bytes(changed)
                            if bounded_bytes(sentinel) != changed:
                                raise ValueError("Incremental source write differs.")
                            expected_sources["Assets/RuntimeSentinel.cs"] = changed
                            incremental_source_edit_seconds = time.perf_counter() - edit_began
                        evidence = output / f"compile-{index}-{phase}"
                        evidence.mkdir()
                        profile = f"Library/Bee/backend-{phase}.traceevents"
                        seconds = run_client([str(backend),
                            "--dagfile=Library/Bee/1900b0aE.dag", "--continue-on-failure",
                            "--dagfilejson=Library/Bee/1900b0aE.dag.json", "--profile=" + profile, "ScriptAssemblies"],
                            view, "compile", evidence, clients, budget, 45)
                        capture_if_present(view / profile, evidence / "backend.traceevents")
                        capture_if_present(view / "Library/Bee/tundra.log.json", evidence / "tundra.log.json")
                        assembly_hashes = {}
                        for name in sorted(ASSEMBLIES):
                            file = view / "Library/ScriptAssemblies" / (name + ".dll")
                            assembly_hashes[name] = digest(capture(file, evidence / (name + ".dll")))
                        inspection_seconds = run_client(["dotnet", str(probe), "inspect-assembly", str(view / "Library/ScriptAssemblies/Assembly-CSharp.dll")],
                                                        view, "inspect", evidence, clients, budget, 15)
                        proof = read(evidence / "inspect.stdout")
                        if proof != dict(type="RuntimeSentinel", field="Value", value=expected, loadedExecutableImage=False):
                            raise ValueError("Compiled constant proof differs from the requested source.")
                        build_row = dict(phase=phase, seconds=seconds, inspectionSeconds=inspection_seconds,
                                         incrementalSourceEditSeconds=incremental_source_edit_seconds,
                                         assemblyProof=proof, assemblySha256=assembly_hashes, evidence=str(evidence))
                        builds.append(build_row)
                        print(json.dumps(dict(mode=mode, run=index, **build_row)), flush=True)
                tick = time.perf_counter()
                changed = b"independent-result\n"
                (view / "Assets/prototype-sentinel.txt").write_bytes(changed)
                if (view / "Assets/prototype-sentinel.txt").read_bytes() != changed or (basis / "Assets/prototype-sentinel.txt").read_bytes() != b"immutable-source\n":
                    raise ValueError("Source write/isolation proof differs.")
                expected_sources["Assets/prototype-sentinel.txt"] = changed
                source_edit_seconds = time.perf_counter() - tick
                mapping_cleanup = dict(mappingCleanupSeconds=0, status="NotRun: compilation not requested",
                                       boundary=MAPPING_CLEANUP_BOUNDARY)
                if args.compile:
                    mapping_cleanup = cleanup_fixed_bee_mapping(root, nonce, mode, ready, provider, clients, basis,
                        before, request, output / f"mapping-cleanup-{index}", budget)
                tick = time.perf_counter()
                publish_stop(root, nonce)
                result = wait_json(root / "result.json", provider, budget, seconds=60)
                if provider.wait(timeout=budget.remaining(10)) != 0:
                    raise RuntimeError("Provider failed normal closure.")
                closure_seconds = time.perf_counter() - tick
                lifecycle_wall_seconds = time.perf_counter() - began
                validate_result(result, nonce, mode, root, before, basis, expected_sources)
                accounted_phases = dict(prepareSeconds=prepare_seconds, inputSeconds=input_seconds,
                    graphSeconds=sum(item["seconds"] for item in costs), compileSeconds=sum(item["seconds"] for item in builds),
                    inspectionSeconds=sum(item["inspectionSeconds"] for item in builds),
                    incrementalSourceEditSeconds=sum(item["incrementalSourceEditSeconds"] for item in builds),
                    sourceEditSeconds=source_edit_seconds, mappingCleanupSeconds=mapping_cleanup["mappingCleanupSeconds"],
                    closureSeconds=closure_seconds)
                row = dict(mode=mode, run=index, prepareSeconds=prepare_seconds, inputSeconds=input_seconds,
                           graphs=costs, builds=builds, sourceEditSeconds=source_edit_seconds,
                           mappingCleanupSeconds=mapping_cleanup["mappingCleanupSeconds"], mappingCleanup=mapping_cleanup,
                           closureSeconds=closure_seconds, provider=result, finalAllocation=final_allocation(root),
                           accountedPhases=accounted_phases, accountedLifecycleSeconds=sum(accounted_phases.values()),
                           lifecycleWallSeconds=lifecycle_wall_seconds,
                           accountedLifecycleBoundary="Sum of named phases including mapping cleanup and its guards; excludes graph/build raw capture and graph comparison, dispatch gaps and final allocation. Wall interval spans provider start through normal exit.",
                           nativeDurability=NATIVE_DURABILITY)
                rows.append(row)
                save(output / f"run-{index}-summary.json", row)
            except BaseException as error:
                fail_preserving(error, root, nonce, mode, provider, ready_valid, clients, budget, before, basis, expected_sources)
                raise
    try:
        if inventory(basis) != before or any(sha(seed / p) != h for p, h in SEED_HASHES.items()):
            raise ValueError("Immutable input changed.")
        if any(sha(path) != expected for path, expected in identities.items()):
            raise ValueError("Frozen script/runtime identity changed during measurement.")
        budget.remaining(1)
    except Exception as error:
        save(output / "final-validation-failure.json", dict(errorType=type(error).__name__, error=str(error),
             completedRuns=len(rows), elapsedSeconds=time.monotonic() - budget.started, outputsPreserved=True))
        raise
    save(output / "result.json", dict(scope="Bounded real Bee graph/optional Csc replay, not complete Unity performance",
         basisAndHistoricalInputUnchanged=True, frozenRuntimeUnchanged=True, equalNormalizedGraphAndPayload=True,
         matchedVariants=set(args.order) == {"overlay", "native-bee"}, rows=rows,
         nativeDurability=NATIVE_DURABILITY, allocationBoundary=ALLOCATION_BOUNDARY,
         mappingCleanupBoundary=MAPPING_CLEANUP_BOUNDARY,
         peakTemporaryAllocation="NotMeasured", elapsedSeconds=time.monotonic() - budget.started))


if __name__ == "__main__":
    main()

