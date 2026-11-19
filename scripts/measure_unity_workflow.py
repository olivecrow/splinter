"""Measure an owned Windows Unity Editor's startup, script edits and normal shutdown."""
import argparse
import ctypes
from ctypes import wintypes
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import time


def read_json(path):
    try:
        return json.loads(path.read_bytes().decode("utf-8-sig"))
    except FileNotFoundError:
        return None


def read_observation(path):
    """Read an atomically replaced live receipt without holding up its writer."""
    if os.name != "nt":
        return read_json(path)
    import msvcrt
    kernel = ctypes.WinDLL("kernel32", use_last_error=True)
    kernel.CreateFileW.argtypes = [wintypes.LPCWSTR, wintypes.DWORD, wintypes.DWORD, wintypes.LPVOID,
                                   wintypes.DWORD, wintypes.DWORD, wintypes.HANDLE]
    kernel.CreateFileW.restype = wintypes.HANDLE
    kernel.CloseHandle.argtypes = [wintypes.HANDLE]
    # GENERIC_READ, FILE_SHARE_READ | WRITE | DELETE, OPEN_EXISTING
    for attempt in range(3):
        handle = kernel.CreateFileW(str(path), 0x80000000, 7, None, 3, 0x80, None)
        if handle != ctypes.c_void_p(-1).value:
            break
        error = ctypes.get_last_error()
        if error in (2, 3, 32, 33, 303):  # absent, sharing/lock violation, delete pending
            return None
        if error == 5 and attempt < 2:  # Windows can deny an open during replacement.
            time.sleep(.02)
            continue
        raise ctypes.WinError(error)
    try:
        fd = msvcrt.open_osfhandle(handle, os.O_RDONLY | os.O_BINARY)
    except BaseException:
        kernel.CloseHandle(handle)
        raise
    with os.fdopen(fd, "rb") as stream:
        return json.loads(stream.read().decode("utf-8-sig"))


class EditorProcess:
    """Retain a handle so PID reuse cannot change the subject of a measurement."""

    def __init__(self, pid, executable, start_ticks=None):
        self.pid = pid
        self.kernel = ctypes.WinDLL("kernel32", use_last_error=True)
        self.kernel.OpenProcess.argtypes = [wintypes.DWORD, wintypes.BOOL, wintypes.DWORD]
        self.kernel.OpenProcess.restype = wintypes.HANDLE
        self.kernel.CloseHandle.argtypes = [wintypes.HANDLE]
        self.kernel.WaitForSingleObject.argtypes = [wintypes.HANDLE, wintypes.DWORD]
        self.kernel.WaitForSingleObject.restype = wintypes.DWORD
        self.handle = self.kernel.OpenProcess(0x100000 | 0x1000, False, pid)
        if not self.handle:
            raise ctypes.WinError(ctypes.get_last_error())
        try:
            stamp = wintypes.FILETIME
            self.kernel.GetProcessTimes.argtypes = [wintypes.HANDLE] + [ctypes.POINTER(stamp)] * 4
            creation, end, kernel, user = (stamp() for _ in range(4))
            if not self.kernel.GetProcessTimes(self.handle, *map(ctypes.byref, (creation, end, kernel, user))):
                raise ctypes.WinError(ctypes.get_last_error())
            self.ticks = (creation.dwHighDateTime << 32 | creation.dwLowDateTime) + 504911232000000000
            size = wintypes.DWORD(32768)
            name = ctypes.create_unicode_buffer(size.value)
            self.kernel.QueryFullProcessImageNameW.argtypes = [wintypes.HANDLE, wintypes.DWORD, wintypes.LPWSTR, ctypes.POINTER(wintypes.DWORD)]
            if not self.kernel.QueryFullProcessImageNameW(self.handle, 0, name, ctypes.byref(size)):
                raise ctypes.WinError(ctypes.get_last_error())
            if Path(name.value) != Path(executable) or start_ticks is not None and start_ticks != self.ticks:
                raise ValueError("Editor executable or native start time differs from the launch receipt")
        except BaseException:
            self.close()
            raise

    def exited(self):
        wait = self.kernel.WaitForSingleObject(self.handle, 0)
        if wait not in (0, 258):
            raise ctypes.WinError(ctypes.get_last_error())
        return wait == 0

    def exit_code(self):
        self.kernel.GetExitCodeProcess.argtypes = [wintypes.HANDLE, ctypes.POINTER(wintypes.DWORD)]
        code = wintypes.DWORD()
        if not self.kernel.GetExitCodeProcess(self.handle, ctypes.byref(code)):
            raise ctypes.WinError(ctypes.get_last_error())
        return ctypes.c_int32(code.value).value

    def close(self):
        if self.handle:
            self.kernel.CloseHandle(self.handle)
            self.handle = None


def windows_for(pid):
    user = ctypes.WinDLL("user32", use_last_error=True)
    windows = []
    callback = ctypes.WINFUNCTYPE(wintypes.BOOL, wintypes.HWND, wintypes.LPARAM)
    user.GetWindowThreadProcessId.argtypes = [wintypes.HWND, ctypes.POINTER(wintypes.DWORD)]
    for fn in (user.IsWindowVisible, user.IsWindowEnabled):
        fn.argtypes = [wintypes.HWND]
    user.GetClassNameW.argtypes = [wintypes.HWND, wintypes.LPWSTR, ctypes.c_int]
    user.GetWindowTextW.argtypes = [wintypes.HWND, wintypes.LPWSTR, ctypes.c_int]
    user.GetDlgCtrlID.argtypes = [wintypes.HWND]

    def dialog_buttons(dialog):
        buttons = []

        @callback
        def child(hwnd, _):
            cls = ctypes.create_unicode_buffer(256)
            user.GetClassNameW(hwnd, cls, len(cls))
            if cls.value.casefold() == "button" and user.IsWindowVisible(hwnd) and user.IsWindowEnabled(hwnd):
                buttons.append(user.GetDlgCtrlID(hwnd))
            return True

        user.EnumChildWindows(dialog, child, 0)
        return buttons

    user.EnumChildWindows.argtypes = [wintypes.HWND, callback, wintypes.LPARAM]

    @callback
    def inspect(hwnd, _):
        owner = wintypes.DWORD()
        user.GetWindowThreadProcessId(hwnd, ctypes.byref(owner))
        if owner.value == pid and user.IsWindowVisible(hwnd):
            cls, title = ctypes.create_unicode_buffer(256), ctypes.create_unicode_buffer(512)
            user.GetClassNameW(hwnd, cls, len(cls))
            user.GetWindowTextW(hwnd, title, len(title))
            window = dict(kind=cls.value, title=title.value, enabled=bool(user.IsWindowEnabled(hwnd)))
            if cls.value == "#32770":
                window["buttons"] = dialog_buttons(hwnd)
            windows.append(window)
        return True

    if not user.EnumWindows(inspect, 0):
        raise ctypes.WinError(ctypes.get_last_error())
    return windows


def startup_dialog(window):
    title = window["title"].strip()
    if window["kind"] == "#32770":
        # Unity shows this status window while loading, even when Pipeline is visible.
        # IDCANCEL is the only action a progress window can expose.
        if re.fullmatch(r"Hold on(?:\.{3}|…)?", title, re.I) and all(i == 2 for i in window["buttons"]):
            return "progress"
        return "blocking"
    if re.search(r"software terms|connection lost|license activation|safe mode", title, re.I):
        return "blocking"
    return None


def job_has_licensing_client(group, root_pid):
    kernel = ctypes.WinDLL("kernel32", use_last_error=True)
    kernel.OpenJobObjectW.argtypes = [wintypes.DWORD, wintypes.BOOL, wintypes.LPCWSTR]
    kernel.OpenJobObjectW.restype = wintypes.HANDLE
    kernel.OpenProcess.argtypes = [wintypes.DWORD, wintypes.BOOL, wintypes.DWORD]
    kernel.OpenProcess.restype = wintypes.HANDLE
    kernel.CloseHandle.argtypes = [wintypes.HANDLE]
    kernel.QueryInformationJobObject.argtypes = [wintypes.HANDLE, ctypes.c_int, wintypes.LPVOID, wintypes.DWORD, wintypes.LPVOID]
    kernel.QueryFullProcessImageNameW.argtypes = [wintypes.HANDLE, wintypes.DWORD, wintypes.LPWSTR, ctypes.POINTER(wintypes.DWORD)]
    job = kernel.OpenJobObjectW(4, False, group)
    if not job:
        raise ctypes.WinError(ctypes.get_last_error())
    try:
        size = 64
        while size <= 65536:
            data = ctypes.create_string_buffer(8 + ctypes.sizeof(ctypes.c_size_t) * size)
            if kernel.QueryInformationJobObject(job, 3, data, len(data), None):
                break
            if ctypes.get_last_error() != 234:
                raise ctypes.WinError(ctypes.get_last_error())
            size *= 2
        else:
            raise RuntimeError("Process inventory exceeded its bound")
        count = wintypes.DWORD.from_buffer(data, 4).value
        pids = list((ctypes.c_size_t * count).from_buffer(data, 8))
        if root_pid not in pids:
            raise RuntimeError("The native Editor is absent from its registered process group")
        for pid in pids:
            proc = kernel.OpenProcess(0x1000, False, pid)
            if not proc:
                if ctypes.get_last_error() == 87:  # member exited after enumeration
                    continue
                raise ctypes.WinError(ctypes.get_last_error())
            try:
                length = wintypes.DWORD(32768)
                name = ctypes.create_unicode_buffer(length.value)
                if not kernel.QueryFullProcessImageNameW(proc, 0, name, ctypes.byref(length)):
                    raise ctypes.WinError(ctypes.get_last_error())
                if Path(name.value).name.casefold() in ("unity.licensing.client.exe", "unitylicensingclient.exe"):
                    return True
            finally:
                kernel.CloseHandle(proc)
        return False
    finally:
        kernel.CloseHandle(job)


def log_phases(text):
    """Keep Unity's overlapping scopes and every refresh reason separately."""
    def values(pattern, divisor=1):
        return [float(s) / divisor for s in re.findall(pattern, text)]

    return dict(beeSeconds=values(r"Tundra build success \(([\d.]+) seconds"),
                compileSeconds=values(r"AssetDatabase: script compilation time: ([\d.]+)s"),
                reloadSeconds=values(r"Domain Reload Profiling: ([\d.]+)ms", 1000),
                scriptingSummaries=[dict(reloads=int(n), reloadSeconds=int(r)/1000, compileSeconds=int(c)/1000)
                                    for n, r, c in re.findall(r"Scripting: domain reloads=(\d+), domain reload time=(\d+) ms, compile time=(\d+) ms", text)],
                refresh=[dict(seconds=float(s), reason=reason.strip()) for s, reason in re.findall(
                    r"Asset Pipeline Refresh[^\n]*?Total: ([\d.]+) seconds - Initiated by ([^\r\n]+)", text)],
                packageIoErrors=[line.strip()[:500] for line in text.splitlines()
                                 if "PackageCache" in line and re.search(
                                     r"DirectoryNotFoundException|FileNotFoundException|IOException|could not (?:open|read)|unable to (?:open|read)|failed to (?:open|read)", line, re.I)])


def log_since(path, offset=0):
    with path.open("rb") as stream:
        stream.seek(offset)
        return stream.read().decode("utf-8", errors="replace")


def counters(control):
    path = control / "overlay/state.json"
    deadline = time.monotonic() + 1
    while True:
        state = read_observation(path)
        if state and state.get("storage"):
            break
        if time.monotonic() >= deadline:
            raise ValueError("Live provider counters were unavailable throughout the sampling interval")
        time.sleep(.05)
    storage = state["storage"]
    metric = storage["metrics"]
    writes = next((w for w in metric["liveJournalWrites"]["operations"] if w["operation"] == "write"), {})
    return dict(id=state["id"], pid=state["processId"], start=state["processStartTimeUtcTicks"],
                epoch=metric["epoch"], observedAt=state["observedAt"], frequency=metric["stopwatchFrequency"],
                readBytes=storage["readBytes"], baseReadBytes=metric["baseReadBytes"],
                journalReadBytes=metric["journalReadBytes"], writeBytes=writes.get("payloadBytes", 0),
                writes=writes.get("durableCommits", 0), flushes=writes.get("flushCalls", 0),
                flushTicks=writes.get("flushTicks", 0), allFlushes=metric["flushCount"])


def counter_delta(before, after):
    for key in ("id", "pid", "start", "epoch", "frequency"):
        if before[key] != after[key]:
            raise ValueError("Different provider or counter epoch: " + key)
    delta = {k: after[k] - before[k] for k in ("readBytes", "baseReadBytes", "journalReadBytes", "writeBytes",
                                                "writes", "flushes", "flushTicks", "allFlushes")}
    if min(delta.values()) < 0:
        raise ValueError("Provider counters regressed")
    return dict(**delta, flushSeconds=delta["flushTicks"] / before["frequency"],
                fromObservation=before["observedAt"], throughObservation=after["observedAt"])


def settle_new_scripts(project, process, paths, type_name, deadline, observe, integer_property=None):
    """Import an explicit fixture edit once, then observe its loaded type or version after reload."""
    if not re.fullmatch(r"[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*", type_name):
        raise ValueError("Expected the qualified name of the new Assembly-CSharp type")
    files = {}
    for relative in paths:
        path = project / relative
        if (not relative.startswith("Assets/") or "\\" in relative or ".." in Path(relative).parts or
                path.suffix != ".cs" or not path.is_file() or path.is_symlink()):
            raise ValueError("Select an existing relative C# fixture asset")
        files[relative] = hashlib.sha256(path.read_bytes()).hexdigest()
    if not files:
        raise ValueError("Select one or more fixture scripts")
    signature = json.dumps([str(project), process.pid, process.ticks, files, type_name, integer_property], sort_keys=True)
    key = "Splinter.Workflow." + hashlib.sha256(signature.encode()).hexdigest()
    read_value = "t != null"
    if integer_property:
        name, expected = integer_property
        if not re.fullmatch(r"[A-Za-z_]\w*", name) or not isinstance(expected, int):
            raise ValueError("Expected a public static integer property and its compiled value")
        read_value = f't != null && t.GetProperty("{name}") != null && (int)t.GetProperty("{name}").GetValue(null) == {expected}'
    check = (f'var t=System.Type.GetType("{type_name}, Assembly-CSharp"); '
             'if(UnityEditor.EditorApplication.isCompiling || UnityEditor.EditorApplication.isUpdating) return "busy"; '
             f'if({read_value} && !UnityEditor.EditorUtility.scriptCompilationFailed) return "loaded"; return "pending";')

    def evaluate(code):
        if process.exited():
            raise RuntimeError("The selected Editor exited during the edit")
        seconds = max(1, min(8, int(deadline - time.monotonic())))
        result = subprocess.run(["unity","command","script","eval",code,"--project-path",str(project),
                                 "--pid",str(process.pid),"--timeout",str(seconds),"--json"], cwd=project,
                                capture_output=True, encoding="utf-8-sig", errors="replace")
        try:
            return json.loads(result.stdout) if result.returncode == 0 else None
        except json.JSONDecodeError:
            return None

    def verify_files():
        if any(hashlib.sha256((project/p).read_bytes()).hexdigest()!=h for p,h in files.items()):
            raise RuntimeError("Fixture source changed while verifying its compiled value")

    while time.monotonic() < deadline:
        state = evaluate(check)
        if state == "loaded":
            verify_files()
            observe("edit-already-loaded", type=type_name)
            return
        if state == "pending":
            break
        time.sleep(min(.75, max(0, deadline-time.monotonic())))
    else:
        raise TimeoutError("Could not observe the exact Editor before requesting its script import")
    imports = "".join("UnityEditor.AssetDatabase.ImportAsset(" + json.dumps(path) + ");" for path in sorted(files))
    submit = (f'const string key="{key}"; if(UnityEditor.SessionState.GetString(key,"")=="") {{ '
              'UnityEditor.SessionState.SetString(key,"queued"); double at=UnityEditor.EditorApplication.timeSinceStartup+0.25; '
              'UnityEditor.EditorApplication.CallbackFunction apply=null; apply=()=>{ '
              'if(UnityEditor.EditorApplication.timeSinceStartup<at)return; UnityEditor.EditorApplication.update-=apply; '
              'UnityEditor.SessionState.SetString(key,"issued"); ' + imports +
              ' }; UnityEditor.EditorApplication.update+=apply; } return key;')
    # A domain reload can disconnect the command response. The request is never
    # resubmitted. Even a positive acknowledgement is insufficient to pass.
    reply = evaluate(submit)
    observe("edit-import-request", acknowledged=reply==key, paths=sorted(files))
    while time.monotonic() < deadline:
        if evaluate(check) == "loaded":
            verify_files()
            observe("edit-loaded", type=type_name)
            return
        time.sleep(min(.75, max(0,deadline-time.monotonic())))
    raise TimeoutError("The specific compiled edit was not observed by the phase deadline")


def ready(project, process, log, deadline, observe, control=None, token=None, startup_gate=None):
    next_sample = 0
    first_pipeline = False
    gate_seen = False
    progress_window = False
    while time.monotonic() < deadline:
        if process.exited():
            raise RuntimeError("The exact Editor exited before readiness")
        try:
            call = subprocess.run(["unity", "status", "--project-path", str(project), "--json"], cwd=project,
                                  capture_output=True, encoding="utf-8-sig", errors="replace",
                                  timeout=max(.1, min(8, deadline - time.monotonic())))
            result = json.loads(call.stdout) if call.stdout.strip() else {}
            rows = [r for r in (result.get("data") or {}).get("instances", [])
                    if Path(r["projectPath"]) == project and r["pid"] == process.pid]
        except (subprocess.TimeoutExpired, json.JSONDecodeError):
            rows = []
        if rows and not first_pipeline:
            observe("pipeline-visible", pid=process.pid)
            first_pipeline = True
        heartbeat = read_observation(control / "heartbeat.json") if control else None
        bridge = control is None or (heartbeat and heartbeat["token"] == token and
                 heartbeat["pid"] == process.pid and heartbeat["startTimeUtcTicks"] == process.ticks and
                 Path(heartbeat["projectPath"]) == project and heartbeat["ready"] and
                 (datetime.now(timezone.utc) - datetime.fromisoformat(heartbeat["observedAt"])).total_seconds() < 15)
        if time.monotonic() >= next_sample or rows and rows[0].get("state") == "ready" and bridge:
            body = log_since(log) if log.exists() else ""
            phases = log_phases(body)
            windows = windows_for(process.pid)
            observe("startup-observation", pid=process.pid, logBytes=log.stat().st_size if log.exists() else 0,
                    lastPhase=(heartbeat or {}).get("phase"), windows=windows, phases=phases)
            if phases["packageIoErrors"]:
                raise RuntimeError("Unity reported package I/O errors: " + str(phases["packageIoErrors"][:2]))
            modal = [w for w in windows if startup_dialog(w) == "blocking"]
            if modal:
                raise RuntimeError("Owned Editor has a blocking dialog: " + str(modal))
            progress_window = any(startup_dialog(w) == "progress" for w in windows)
            # Licensing and the first completed initial refresh cover the shared startup transition.
            if startup_gate and not gate_seen and "Successfully resolved entitlement" in body and any(
                    r["reason"].startswith("InitialRefresh") for r in phases["refresh"]):
                startup_gate()
                gate_seen = True
            next_sample = time.monotonic() + 5
        if len(rows) == 1 and rows[0]["state"] == "ready" and bridge and not progress_window:
            if startup_gate and not gate_seen:
                startup_gate()
            observe("editor-ready", pid=process.pid, startTicks=process.ticks, project=str(project))
            return rows[0]
        time.sleep(min(1.5, max(0, deadline - time.monotonic())))
    raise TimeoutError("Readiness phase expired. Inspect this launch and normally stop the owned Editor.")


def observe_stop(control, process, token, deadline, observe):
    seen = set()
    exit_proof = None
    while time.monotonic() < deadline:
        request = read_observation(control / "stop-request.json")
        heartbeat = read_observation(control / "heartbeat.json")
        receipt = read_observation(control / "process-exit.json")
        if receipt and receipt.get("token") == token and receipt["activeJobProcesses"] == 0:
            exit_proof = receipt
        for key, condition in (("stop-request", request and request.get("token") == token),
                               ("bridge-exiting", heartbeat and heartbeat.get("token") == token and heartbeat["pid"] == process.pid and
                                heartbeat["startTimeUtcTicks"] == process.ticks and heartbeat["phase"] == "exiting"),
                               ("root-exit", process.exited()),
                               ("job-empty", exit_proof)):
            if condition and key not in seen:
                seen.add(key)
                observe(key, pid=process.pid, **({"rootExitCode": process.exit_code()} if key == "root-exit" else {}))
        if {"stop-request", "bridge-exiting", "root-exit", "job-empty"} <= seen:
            code = process.exit_code()
            if exit_proof["rootExitCode"] != code:
                raise RuntimeError(f"Process and supervisor exit codes differ: {code} and {exit_proof['rootExitCode']}")
            if code != 0:
                raise AbnormalEditorExit(code)
            return
        time.sleep(.2)
    raise TimeoutError("Incomplete normal-stop observations for this launch: " + ", ".join(sorted(seen)))


class AbnormalEditorExit(RuntimeError):
    def __init__(self, code):
        self.code = code
        super().__init__(f"Owned Editor exited with 0x{code & 0xffffffff:08X} after its stop request")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("stage", choices=("ready", "stop", "log", "settle-edit"))
    parser.add_argument("--control", required=True, type=Path, help="Returned workspace control directory")
    parser.add_argument("--output", required=True, type=Path, help="New JSONL evidence file")
    parser.add_argument("--estimate-seconds", type=float, help="Comparable phase estimate. Observation stops at twice this value.")
    parser.add_argument("--offset", type=int, default=0, help="First Editor log byte for log analysis")
    parser.add_argument("--asset", action="append", help="Edited Assets-relative C# file, repeatable")
    parser.add_argument("--compiled-type", help="New type name in Assembly-CSharp, or a versioned type with --int-property")
    parser.add_argument("--int-property", help="Known public static integer property on the edited type")
    parser.add_argument("--expected-value", type=int, help="Expected compiled property value")
    args = parser.parse_args()
    workspace = read_json(args.control / "workspace.json")
    if not workspace or any(args.output.resolve().is_relative_to(root.resolve()) for root in
                            (args.control, Path(workspace["path"]), Path(workspace["projectPath"]))):
        parser.error("Choose a separate evidence output outside the source, workspace and control directories")
    if args.stage != "log" and (not args.estimate_seconds or args.estimate_seconds <= 0):
        parser.error("A positive phase estimate is required")
    if args.stage == "settle-edit" and (not args.asset or not args.compiled_type or
                                         bool(args.int_property) != (args.expected_value is not None)):
        parser.error("settle-edit needs the edited assets and compiled type. Supply both expected-value and int-property for an existing type.")
    with args.output.open("x", encoding="utf-8") as output:
        def record(phase, **fields):
            print(json.dumps(dict(time=time.monotonic(), phase=phase, **fields)), file=output, flush=True)
        log = args.control / "editor.log"
        if args.stage == "log":
            record("log-phases", **log_phases(log_since(log, args.offset)))
            return
        deadline = time.monotonic() + 2 * args.estimate_seconds
        lease = read_json(args.control / "launch-receipt.json")
        process = EditorProcess(lease["processId"], lease["executablePath"], lease["startTimeUtcTicks"])
        try:
            if args.stage == "ready":
                ready(Path(workspace["path"]), process, log, deadline, record, args.control, lease["token"])
            elif args.stage == "settle-edit":
                ready(Path(workspace["path"]), process, log, deadline, record, args.control, lease["token"])
                settle_new_scripts(Path(workspace["path"]), process, args.asset, args.compiled_type, deadline, record,
                                   (args.int_property, args.expected_value) if args.int_property else None)
            else:
                observe_stop(args.control, process, lease["token"], deadline, record)
        finally:
            process.close()


if __name__ == "__main__":
    main()
