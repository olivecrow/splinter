"""Measure the fixed real three-assembly Unity Bee build without an Editor.

Default: bounded-overlay/internal-native/internal-native/bounded-overlay, with cold,
unchanged and one-source-change builds. --no-compile is a graph-only diagnostic.
The old native-bee junction remains an explicit diagnostic; its atomic publication
failure is not a supported build workflow. No filename-specific mapping cleanup is
performed. Live providers, clients and failed evidence are preserved.
"""
import argparse
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
    "New internal-native Bee output uses ordinary NTFS buffered durability, not the "
    "bounded comparator's per-write durable ACKs. Source, inherited Bee files and "
    "other Library paths retain the bounded range journal. These component timings "
    "do not establish equal-durability or complete Unity performance."
)
ALLOCATION_BOUNDARY = (
    "Final retained regular-file allocation after normal provider exit, not peak: only "
    "this run's delta and native-bee directories. Shared fixture, tools, source export, "
    "receipts, measurement copies and filesystem directory metadata are excluded."
)
CLOSURE_BOUNDARY = (
    "Normal provider stop only; no filename-specific reopen, forced unmap, process "
    "kill or output deletion. Held file contexts fail qualification and preserve "
    "the provider and its outputs."
)
MEMORY_BOUNDARY = (
    "Sampled provider and directly launched client process working sets/private "
    "bytes only. Compiler descendants, system/NTFS cache and complete machine RAM "
    "are not measured. Sampled maxima are not a simultaneous process-tree peak."
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


def run_client(command, view, label, evidence, clients, budget, limit, trace):
    allowance = budget.remaining(limit, reserve=20)
    save(evidence / (label + "-request.json"), dict(command=[str(x) for x in command], cwd=str(view), waitLimitSeconds=allowance))
    with (evidence / (label + ".stdout")).open("xb") as stdout, (evidence / (label + ".stderr")).open("xb") as stderr:
        began = time.perf_counter()
        child = start(command, cwd=view, stdout=stdout, stderr=stderr)
        clients.append((label, child))
        deadline = time.monotonic() + allowance
        while True:
            trace.memory(label, child)
            remaining = deadline - time.monotonic()
            if remaining <= 0:
                raise TimeoutError(f"Preserve live {label} PID {child.pid}; no stop/unmount requested.")
            try:
                code = child.wait(timeout=min(.1, remaining))
                break
            except subprocess.TimeoutExpired:
                pass
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
    if (mode in {"native-bee", "internal-native"} and
            (not native or Path(native) != root / "native-bee" or not ready.get("nativeTargetIdentity"))) or (mode == "overlay" and native is not None):
        raise ValueError("Provider native target mismatch.")
    if ready.get("journalStorageMode") != "bounded":
        raise ValueError("The comparator and source journal must use the current bounded mode.")
    if mode in {"overlay", "internal-native"}:
        if ready.get("nativeClientReparsePoint") is not False:
            raise ValueError("The current candidates must share one virtual namespace without a Bee junction.")
        if (root / "view/Library/Bee").lstat().st_file_attributes & 0x400:
            raise ValueError("Unexpected client-visible Bee reparse point.")
    source_file_records(ready["sourceFiles"], before, basis)


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
            result.get("errors") != [] or result.get("nativeOutputsPreserved") != (mode in {"native-bee", "internal-native"})):
        raise ValueError("Provider result did not satisfy identity/source/normal-closure/error gates.")
    for name in ("flushSeconds", "preserveSeconds", "cleanupSeconds"):
        value = result.get("timings", {}).get(name)
        if not isinstance(value, (int, float)) or isinstance(value, bool) or value < 0:
            raise ValueError("Missing provider completion timing: " + name)
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


def final_allocation(root, stopped=True):
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
                allocatedBytes=sum(r["allocatedBytes"] for r in rows),
                boundary=ALLOCATION_BOUNDARY if stopped else
                    "Recursive point sample between client phases of delta/native-bee regular files; not a peak. Same exclusions as final allocation.",
                method="GetFileInformationByHandleEx(FileStandardInfo).AllocationSize",
                peakTemporaryAllocation="NotMeasured")


class ResourceTrace:
    """Small point samples; never confuse them with a process-tree/space peak."""
    def __init__(self, root, provider):
        self.root, self.provider = root, provider
        self.started = time.perf_counter()
        self.memory_samples, self.phases = [], []
        self.kernel = ctypes.WinDLL("kernel32", use_last_error=True)
        self.psapi = ctypes.WinDLL("psapi", use_last_error=True)
        self.kernel.OpenProcess.argtypes = [ctypes.c_uint32, ctypes.c_int, ctypes.c_uint32]
        self.kernel.OpenProcess.restype = ctypes.c_void_p
        self.kernel.CloseHandle.argtypes = [ctypes.c_void_p]
        self.kernel.CloseHandle.restype = ctypes.c_int
        self.psapi.GetProcessMemoryInfo.argtypes = [ctypes.c_void_p, ctypes.c_void_p, ctypes.c_uint32]
        self.psapi.GetProcessMemoryInfo.restype = ctypes.c_int

    def process_memory(self, child, role):
        if child.poll() is not None:
            return dict(role=role, processId=child.pid, status="exited", exitCode=child.returncode)
        class MemoryCounters(ctypes.Structure):
            _fields_ = [("cb", ctypes.c_uint32), ("pageFaultCount", ctypes.c_uint32),
                        *[(name, ctypes.c_size_t) for name in (
                            "peakWorkingSet", "workingSet", "peakPagedPool", "pagedPool",
                            "peakNonPagedPool", "nonPagedPool", "pagefile", "peakPagefile", "private")]]
        handle = self.kernel.OpenProcess(0x410, False, child.pid)
        if not handle:
            return dict(role=role, processId=child.pid, status="unavailable", win32Error=ctypes.get_last_error())
        try:
            counters = MemoryCounters()
            counters.cb = ctypes.sizeof(counters)
            if not self.psapi.GetProcessMemoryInfo(handle, ctypes.byref(counters), ctypes.sizeof(counters)):
                return dict(role=role, processId=child.pid, status="unavailable", win32Error=ctypes.get_last_error())
            return dict(role=role, processId=child.pid, status="sampled", workingSetBytes=counters.workingSet,
                        privateBytes=counters.private, processPeakWorkingSetBytes=counters.peakWorkingSet)
        finally:
            self.kernel.CloseHandle(handle)

    def memory(self, label, client=None):
        began = time.perf_counter()
        rows = [self.process_memory(self.provider, "provider")]
        if client is not None:
            rows.append(self.process_memory(client, "direct-client"))
        self.memory_samples.append(dict(phase=label, elapsedSeconds=began - self.started,
            processes=rows, samplingSeconds=time.perf_counter() - began))

    def phase(self, label):
        began = time.perf_counter()
        self.memory(label)
        item = dict(phase=label, elapsedSeconds=began - self.started,
                    allocation=final_allocation(self.root, stopped=False))
        item["samplingSeconds"] = time.perf_counter() - began
        self.phases.append(item)
        save(self.root / f"resource-phase-{len(self.phases):02}.json", item)

    def summary(self):
        observations = [item for row in self.memory_samples for item in row["processes"] if item["status"] == "sampled"]
        by_role = {}
        for role in ("provider", "direct-client"):
            rows = [item for item in observations if item["role"] == role]
            by_role[role] = None if not rows else dict(
                observedMaximumWorkingSetBytes=max(item["workingSetBytes"] for item in rows),
                observedMaximumPrivateBytes=max(item["privateBytes"] for item in rows),
                maximumReportedProcessPeakWorkingSetBytes=max(item["processPeakWorkingSetBytes"] for item in rows))
        return dict(memoryBoundary=MEMORY_BOUNDARY, memorySamplingIntervalMilliseconds=100,
                    memorySamples=self.memory_samples, observedMemory=by_role,
                    phaseAllocationSamples=self.phases,
                    sampledMaximumAllocatedBytes=max((row["allocation"]["allocatedBytes"] for row in self.phases), default=0),
                    sampledMaximumAllocationBoundary="Point samples only, not peak temporary allocation.",
                    memorySamplingSeconds=sum(row["samplingSeconds"] for row in self.memory_samples),
                    phaseSamplingSeconds=sum(row["samplingSeconds"] for row in self.phases))


def file_identity(path):
    """Read the Windows volume/file identity through the application's exact path."""
    kernel = ctypes.WinDLL("kernel32", use_last_error=True)
    class FileTime(ctypes.Structure):
        _fields_ = [("low", ctypes.c_uint32), ("high", ctypes.c_uint32)]
    class FileInfo(ctypes.Structure):
        _fields_ = [("attributes", ctypes.c_uint32), ("creation", FileTime), ("access", FileTime),
                    ("write", FileTime), ("volume", ctypes.c_uint32), ("sizeHigh", ctypes.c_uint32),
                    ("sizeLow", ctypes.c_uint32), ("links", ctypes.c_uint32),
                    ("indexHigh", ctypes.c_uint32), ("indexLow", ctypes.c_uint32)]
    kernel.CreateFileW.argtypes = [ctypes.c_wchar_p, ctypes.c_uint32, ctypes.c_uint32,
        ctypes.c_void_p, ctypes.c_uint32, ctypes.c_uint32, ctypes.c_void_p]
    kernel.CreateFileW.restype = ctypes.c_void_p
    kernel.GetFileInformationByHandle.argtypes = [ctypes.c_void_p, ctypes.POINTER(FileInfo)]
    kernel.GetFileInformationByHandle.restype = ctypes.c_int
    kernel.CloseHandle.argtypes = [ctypes.c_void_p]
    kernel.CloseHandle.restype = ctypes.c_int
    # Follow the project mount itself; OPEN_REPARSE_POINT would inspect its NTFS
    # mountpoint instead of the virtual volume presented to the Bee process.
    handle = kernel.CreateFileW(str(path), 0x80, 7, None, 3, 0x02000000, None)
    if handle == ctypes.c_void_p(-1).value:
        raise ctypes.WinError(ctypes.get_last_error())
    try:
        info = FileInfo()
        if not kernel.GetFileInformationByHandle(handle, ctypes.byref(info)):
            raise ctypes.WinError(ctypes.get_last_error())
        return dict(volumeSerial=info.volume, fileIndex=(info.indexHigh << 32) | info.indexLow,
                    length=(info.sizeHigh << 32) | info.sizeLow, links=info.links, attributes=info.attributes)
    finally:
        kernel.CloseHandle(handle)


def bee_publication_files(view, graph, evidence, mode):
    root_identity = file_identity(view)
    rows = []
    for key, temporary_key in (("StateFileName", "StateFileNameTmp"), ("DigestCacheFileName", "DigestCacheFileNameTmp")):
        path, temporary = view_output(graph[key], view), view_output(graph[temporary_key], view)
        contents = capture(path, evidence / (key + ".bin"))
        identity = file_identity(path)
        if not contents or identity["length"] != len(contents) or identity["attributes"] & 0x410 or identity["links"] != 1:
            raise ValueError("Unsupported or empty published Bee state/cache: " + str(path))
        if mode != "native-bee" and identity["volumeSerial"] != root_identity["volumeSerial"]:
            raise ValueError("Published Bee output does not belong to the single virtual volume.")
        if temporary.exists():
            raise ValueError("Successful Bee build left an unpublished state/digest temporary file: " + str(temporary))
        rows.append(dict(path=path.relative_to(view).as_posix(), sha256=digest(contents), identity=identity,
                         temporaryPath=temporary.relative_to(view).as_posix(), temporaryAbsent=True))
    return dict(files=rows, sameVirtualVolume=mode != "native-bee",
                boundary="Successful backend output and same-volume identity. Atomic logical publication additionally requires provider rename telemetry and the native rename correctness gate.")


def validate_bee_publications(result, mode, builds):
    if not builds or mode != "internal-native":
        return dict(status="NotApplicable", boundary="Only the internal-native complete build candidate has this telemetry gate.")
    publications = result.get("nativeStorage", {}).get("publications", [])
    matched = []
    for name in ("Library/Bee/TundraBuildState.state", "Library/Bee/tundra.digestcache"):
        records = [row for row in publications if row.get("destinationPath") == name]
        if not records or any(row.get("sourcePath") != name + ".tmp" or
            not row.get("sourceId") or row.get("sourceId") != row.get("publishedId") or
            row.get("atomicNamespacePublication") is not True for row in records):
            raise ValueError("Missing or invalid actual Bee atomic publication telemetry for " + name)
        matched.extend(records)
    return dict(status="Passed", publications=matched,
                boundary="Actual Bee state/digest temporary-file publication preserves logical file identity within the provider namespace; not a power-loss proof.")


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
    parser.add_argument("--rounds", type=int, default=1)
    parser.add_argument("--budget-seconds", type=float, default=300)
    parser.add_argument("--compile", action=argparse.BooleanOptionalAction, default=True,
                        help="Run real cold/unchanged/incremental Bee builds (default); --no-compile is graph-only. No Editor or ILPP.")
    parser.add_argument("--order", nargs="+", choices=["overlay", "internal-native", "native-bee"],
                        default=["overlay", "internal-native", "internal-native", "overlay"])
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
    save(output / "input.json", dict(scope="Fixed real three-assembly Bee graph and Csc; no Editor, ILPP or Play",
         seed=str(seed), seedFilesSha256=SEED_HASHES, basisFilesSha256=before,
         copiedFixtureBytes=sum((basis / p).stat().st_size for p in before), runtimeFilesSha256=identities,
         order=args.order, rounds=args.rounds, compile=args.compile, budgetSeconds=args.budget_seconds, osCacheReset=False,
         compilerServerReset=False,
         backendInvocation="Standalone explicit DAG; no parent IPC or deferred stdin gate",
         coldDefinition="Empty per-mount generated outputs; OS and compiler server caches are not reset",
         comparatorJournalStorageMode="bounded", clientBeeJunction="Only historical native-bee diagnostic",
         nativeDurability=NATIVE_DURABILITY, allocationBoundary=ALLOCATION_BOUNDARY,
         closureBoundary=CLOSURE_BOUNDARY, memoryBoundary=MEMORY_BOUNDARY))
    rows, canonical = [], None
    for index, mode in enumerate(args.order):
        root = output / f"run-{index}-{mode}"
        nonce = uuid.uuid4().hex
        request = output / f"request-{index}.json"
        save(request, dict(root=str(root), basis=str(basis), mode=mode, nonce=nonce))
        provider, ready_valid, clients, expected_sources, trace = None, False, [], {}, None
        with (output / f"provider-{index}.stdout").open("xb") as stdout, (output / f"provider-{index}.stderr").open("xb") as stderr:
            try:
                budget.remaining(1, reserve=20)
                began = time.perf_counter()
                provider = start(["dotnet", str(probe), "serve", "--input", str(request)], cwd=REPO, stdout=stdout, stderr=stderr)
                ready = wait_json(root / "ready.json", provider, budget, reserve=20)
                prepare_seconds = time.perf_counter() - began
                validate_ready(ready, nonce, mode, root, provider, before, basis)
                ready_valid = True
                trace = ResourceTrace(root, provider)
                trace.phase("prepared")
                view = root / "view"
                remapped = replace_paths(original_input, seed, view)
                validate_recipe(remapped, view)
                tick = time.perf_counter()
                (view / INPUT_RELATIVE).write_text(json.dumps(remapped, separators=(",", ":")), encoding="utf-8")
                input_seconds = time.perf_counter() - tick
                trace.phase("input-published")
                costs, builds = [], []
                for iteration in range(args.rounds):
                    evidence = output / f"graph-{index}-{iteration}"
                    evidence.mkdir()
                    profile = f"Library/Bee/buildprogram-{iteration}.traceevents"
                    seconds = run_client([str(launcher), str(tool), "Library/Bee/1900b0aE.dag.json", INPUT_RELATIVE, profile],
                                         view, "graph", evidence, clients, budget, 30, trace)
                    graph_digest, graph_data = semantic_graph(view, evidence, profile)
                    if canonical is None:
                        canonical = graph_digest
                        save(output / "canonical-graph.json", graph_data)
                    elif graph_digest != canonical:
                        raise ValueError(f"Semantic graph/payload differs; preserve evidence {evidence}")
                    costs.append(dict(iteration=iteration, phase="empty-Bee-output" if iteration == 0 else "graph-rewrite",
                                      seconds=seconds, normalizedGraphSha256=graph_digest, evidence=str(evidence)))
                    trace.phase("graph-" + str(iteration))
                    print(json.dumps(dict(mode=mode, run=index, **costs[-1])), flush=True)
                graph = read(output / f"graph-{index}-{args.rounds - 1}" / "dag.json")
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
                            view, "compile", evidence, clients, budget, 45, trace)
                        capture_if_present(view / profile, evidence / "backend.traceevents")
                        capture_if_present(view / "Library/Bee/tundra.log.json", evidence / "tundra.log.json")
                        assembly_hashes = {}
                        for name in sorted(ASSEMBLIES):
                            file = view / "Library/ScriptAssemblies" / (name + ".dll")
                            assembly_hashes[name] = digest(capture(file, evidence / (name + ".dll")))
                        inspection_seconds = run_client(["dotnet", str(probe), "inspect-assembly", str(view / "Library/ScriptAssemblies/Assembly-CSharp.dll")],
                                                        view, "inspect", evidence, clients, budget, 15, trace)
                        proof = read(evidence / "inspect.stdout")
                        if proof != dict(type="RuntimeSentinel", field="Value", value=expected, loadedExecutableImage=False):
                            raise ValueError("Compiled constant proof differs from the requested source.")
                        publication = bee_publication_files(view, graph, evidence, mode)
                        build_row = dict(phase=phase, seconds=seconds, inspectionSeconds=inspection_seconds,
                                         incrementalSourceEditSeconds=incremental_source_edit_seconds,
                                         assemblyProof=proof, assemblySha256=assembly_hashes,
                                         beeStatePublication=publication, evidence=str(evidence))
                        builds.append(build_row)
                        trace.phase("compile-" + phase)
                        print(json.dumps(dict(mode=mode, run=index, **build_row)), flush=True)
                tick = time.perf_counter()
                changed = b"independent-result\n"
                (view / "Assets/prototype-sentinel.txt").write_bytes(changed)
                if (view / "Assets/prototype-sentinel.txt").read_bytes() != changed or (basis / "Assets/prototype-sentinel.txt").read_bytes() != b"immutable-source\n":
                    raise ValueError("Source write/isolation proof differs.")
                expected_sources["Assets/prototype-sentinel.txt"] = changed
                source_edit_seconds = time.perf_counter() - tick
                trace.phase("before-normal-stop")
                tick = time.perf_counter()
                publish_stop(root, nonce)
                result = wait_json(root / "result.json", provider, budget, seconds=60)
                if provider.wait(timeout=budget.remaining(10)) != 0:
                    raise RuntimeError("Provider failed normal closure.")
                closure_seconds = time.perf_counter() - tick
                lifecycle_wall_seconds = time.perf_counter() - began
                validate_result(result, nonce, mode, root, before, basis, expected_sources)
                atomic_publication = validate_bee_publications(result, mode, builds)
                accounted_phases = dict(prepareSeconds=prepare_seconds, inputSeconds=input_seconds,
                    graphSeconds=sum(item["seconds"] for item in costs), compileSeconds=sum(item["seconds"] for item in builds),
                    inspectionSeconds=sum(item["inspectionSeconds"] for item in builds),
                    incrementalSourceEditSeconds=sum(item["incrementalSourceEditSeconds"] for item in builds),
                    sourceEditSeconds=source_edit_seconds, closureSeconds=closure_seconds)
                row = dict(mode=mode, run=index, prepareSeconds=prepare_seconds, inputSeconds=input_seconds,
                           graphs=costs, builds=builds, sourceEditSeconds=source_edit_seconds,
                           closureSeconds=closure_seconds, provider=result, finalAllocation=final_allocation(root),
                           providerCompletionPhases=result["timings"], actualBeeAtomicPublication=atomic_publication,
                           resourceTrace=trace.summary(), closureBoundary=CLOSURE_BOUNDARY,
                           accountedPhases=accounted_phases, accountedLifecycleSeconds=sum(accounted_phases.values()),
                           lifecycleWallSeconds=lifecycle_wall_seconds,
                           accountedLifecycleBoundary="Sum of named phases including normal closure once; provider flush/preserve/cleanup are subphases of closure and must not be added again. Excludes raw capture, comparison, phase allocation samples, dispatch gaps and final allocation; periodic process-memory sampling is included in client phases. Wall interval spans provider start through normal exit.",
                           nativeDurability=NATIVE_DURABILITY)
                rows.append(row)
                save(output / f"run-{index}-summary.json", row)
            except BaseException as error:
                if trace is not None:
                    save(output / f"run-{index}-resource-failure.json", trace.summary())
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
    save(output / "result.json", dict(scope="Bounded real Bee graph/Csc replay, not complete Unity performance",
         basisAndHistoricalInputUnchanged=True, frozenRuntimeUnchanged=True, equalNormalizedGraphAndPayload=True,
         matchedVariants=set(args.order) == {"overlay", "internal-native"},
         balancedAbba=args.order in (["overlay", "internal-native", "internal-native", "overlay"],
                                    ["internal-native", "overlay", "overlay", "internal-native"]),
         actualBuildsCompleted=args.compile, comparatorJournalStorageMode="bounded", rows=rows,
         nativeDurability=NATIVE_DURABILITY, allocationBoundary=ALLOCATION_BOUNDARY,
         closureBoundary=CLOSURE_BOUNDARY, memoryBoundary=MEMORY_BOUNDARY,
         peakTemporaryAllocation="NotMeasured", elapsedSeconds=time.monotonic() - budget.started))


if __name__ == "__main__":
    main()

