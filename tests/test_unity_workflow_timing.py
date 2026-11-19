"""Regression checks for the external Unity workflow timing diagnostic."""
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import time
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
import measure_unity_workflow as workflow


class RunningEditor:
    pid = 8123
    ticks = 9876

    def exited(self):
        return False


class ScriptEditTests(unittest.TestCase):
    def setUp(self):
        directory = tempfile.TemporaryDirectory()
        self.addCleanup(directory.cleanup)
        self.project = Path(directory.name)
        (self.project / "Assets").mkdir()
        (self.project / "Assets/Speed.cs").write_text("public class Speed {}", encoding="utf-8")
        self.events = []

    def run_edit(self):
        workflow.settle_new_scripts(self.project, RunningEditor(), ["Assets/Speed.cs"], "Speed",
                                    time.monotonic() + 5, lambda phase, **_: self.events.append(phase),
                                    integer_property=("Revision", 2))

    def test_domain_reload_can_drop_import_reply_without_a_second_import(self):
        calls = []
        replies = iter(["pending", None, "busy", "loaded"])

        def unity(args, **kwargs):
            calls.append(args)
            self.assertEqual(self.project, kwargs["cwd"])
            self.assertIn(str(RunningEditor.pid), args)
            value = next(replies)
            return subprocess.CompletedProcess(args, 6 if value is None else 0, json.dumps(value), "")

        with patch.object(workflow.subprocess, "run", side_effect=unity), patch.object(workflow.time, "sleep"):
            self.run_edit()
        import_requests = [args for args in calls if "AssetDatabase.ImportAsset" in args[4]]
        self.assertEqual(1, len(import_requests))
        self.assertIn("Assets/Speed.cs", import_requests[0][4])
        self.assertIn('GetProperty("Revision")', calls[-1][4])
        self.assertEqual(["edit-import-request", "edit-loaded"], self.events)

    def test_startup_that_loaded_the_new_revision_needs_no_import(self):
        with patch.object(workflow.subprocess, "run", return_value=subprocess.CompletedProcess([], 0, '"loaded"', "")) as unity:
            self.run_edit()
        self.assertEqual(1, unity.call_count)
        self.assertEqual(["edit-already-loaded"], self.events)

    def test_log_keeps_nested_refresh_and_compiler_scopes(self):
        phases = workflow.log_phases(
            "*** Tundra build success (12.25 seconds), 3 items updated\n"
            "AssetDatabase: script compilation time: 14.2s\nDomain Reload Profiling: 2500ms\n"
            "Asset Pipeline Refresh (id=1): Total: 19.75 seconds - Initiated by InitialRefreshV2(ForceSynchronousImport)\n")
        self.assertEqual([12.25], phases["beeSeconds"])
        self.assertEqual([14.2], phases["compileSeconds"])
        self.assertEqual([2.5], phases["reloadSeconds"])
        self.assertEqual([dict(seconds=19.75, reason="InitialRefreshV2(ForceSynchronousImport)")], phases["refresh"])

    def test_visible_progress_defers_ready_until_it_clears(self):
        state = {"data": {"instances": [{"projectPath": str(self.project), "pid": RunningEditor.pid, "state": "ready"}]}}
        progress = {"kind": "#32770", "title": "Hold on", "buttons": []}
        with patch.object(workflow.subprocess, "run", return_value=subprocess.CompletedProcess([], 0, json.dumps(state), "")), \
             patch.object(workflow, "windows_for", side_effect=[[progress], []]) as windows, \
             patch.object(workflow.time, "sleep"):
            workflow.ready(self.project, RunningEditor(), self.project/"Editor.log", time.monotonic()+5,
                           lambda phase, **_: self.events.append(phase))
        self.assertEqual(2, windows.call_count)
        self.assertEqual("editor-ready", self.events[-1])

    def test_actionable_dialogs_still_stop_observation(self):
        self.assertEqual("blocking", workflow.startup_dialog({"kind":"#32770", "title":"Hold on", "buttons":[1, 2]}))
        self.assertEqual("blocking", workflow.startup_dialog({"kind":"#32770", "title":"Unity Software Terms", "buttons":[]}))
        self.assertEqual("progress", workflow.startup_dialog({"kind":"#32770", "title":"Hold on...", "buttons":[2]}))

    def test_provider_sample_waits_for_replacement_and_keeps_its_identity(self):
        metric=dict(epoch="epoch",stopwatchFrequency=1000,baseReadBytes=4,journalReadBytes=3,
                    liveJournalWrites={"operations":[]},flushCount=0)
        state=dict(id="workspace",processId=123,processStartTimeUtcTicks=456,observedAt="now",
                   storage=dict(readBytes=7,metrics=metric))
        with patch.object(workflow,"read_observation",side_effect=[None,state]) as read, patch.object(workflow.time,"sleep"):
            sample=workflow.counters(self.project)
        self.assertEqual([self.project/"overlay/state.json"]*2,[c.args[0] for c in read.call_args_list])
        self.assertEqual(("workspace",123,456,"epoch",7),tuple(sample[k] for k in ("id","pid","start","epoch","readBytes")))

    def test_shutdown_preserves_nonzero_root_exit_even_with_an_empty_job(self):
        class ExitedEditor(RunningEditor):
            def exited(self):
                return True

            def exit_code(self):
                return -1073741819

        editor = ExitedEditor()
        for name, fields in (("stop-request", {}), ("heartbeat", {"pid":editor.pid, "startTimeUtcTicks":editor.ticks, "phase":"exiting"}),
                             ("process-exit", {"activeJobProcesses":0, "rootExitCode":editor.exit_code()})):
            (self.project/(name+".json")).write_text(json.dumps(dict(token="same", **fields)), encoding="utf-8")
        with self.assertRaisesRegex(RuntimeError, "0xC0000005"):
            workflow.observe_stop(self.project, editor, "same", time.monotonic()+1,
                                  lambda phase, **_: self.events.append(phase))
        self.assertEqual(["stop-request", "bridge-exiting", "root-exit", "job-empty"], self.events)

    @unittest.skipUnless(sys.platform == "win32", "Win32 file sharing")
    def test_live_receipt_can_be_temporarily_reserved_by_its_writer(self):
        path = self.project/"heartbeat.json"
        path.write_text('{"phase":"ready"}', encoding="utf-8")
        kernel = workflow.ctypes.WinDLL("kernel32", use_last_error=True)
        kernel.CreateFileW.argtypes = [workflow.wintypes.LPCWSTR, workflow.wintypes.DWORD, workflow.wintypes.DWORD,
                                      workflow.wintypes.LPVOID, workflow.wintypes.DWORD, workflow.wintypes.DWORD,
                                      workflow.wintypes.HANDLE]
        kernel.CreateFileW.restype = workflow.wintypes.HANDLE
        kernel.CloseHandle.argtypes = [workflow.wintypes.HANDLE]
        handle = kernel.CreateFileW(str(path), 0x80000000, 0, None, 3, 0x80, None)
        self.assertNotEqual(handle, workflow.ctypes.c_void_p(-1).value)
        try:
            self.assertIsNone(workflow.read_observation(path))
        finally:
            kernel.CloseHandle(handle)
        self.assertEqual({"phase":"ready"}, workflow.read_observation(path))


if __name__ == "__main__":
    unittest.main()
