// Included only in a Splinter-owned workspace. No global EditorPrefs writes.
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Splinter.Editor
{
    [InitializeOnLoad]
    public static class WorkspaceBridge
    {
        private const string StopSessionKey = "Splinter.Workspace.LastStopRequest";
        private static string control = "";
        private static string token = "";
        private static bool quitting;
        private static double nextTick;
        private static readonly int Pid = System.Diagnostics.Process.GetCurrentProcess().Id;
        private static readonly long StartTicks = System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime().Ticks;

        static WorkspaceBridge()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
            {
                if (args[i] == "-splinterControlPath") control = args[++i];
                else if (args[i] == "-splinterToken") token = args[++i];
            }
            if (string.IsNullOrEmpty(control) || string.IsNullOrEmpty(token)) return;
            EditorApplication.update += Tick;
            EditorApplication.quitting += () =>
            {
                quitting = true;
                try { Heartbeat("exiting", false); }
                catch (Exception exception) { Debug.LogError("Splinter exit heartbeat: " + exception); }
            };
        }

        private static void Tick()
        {
            if (quitting || EditorApplication.timeSinceStartup < nextTick) return;
            nextTick = EditorApplication.timeSinceStartup + 0.5;
            try
            {
                bool busy = EditorApplication.isCompiling || EditorApplication.isUpdating;
                StopRequest stop = Read<StopRequest>("stop-request.json");
                if (!busy && stop != null && stop.token == token && !string.IsNullOrEmpty(stop.requestId) &&
                    stop.requestId != SessionState.GetString(StopSessionKey, ""))
                {
                    SessionState.SetString(StopSessionKey, stop.requestId);
                    string reason = UnsavedReason();
                    if (reason == null)
                    {
                        try { reason = OwnedEditorWindow.PostClose(); }
                        catch (Exception exception) { reason = "Could not request window close: " + exception.Message; }
                    }
                    if (reason == null) return;
                    Write("stop-result.json", new StopResult { requestId = stop.requestId, error = reason });
                }
                string phase = EditorApplication.isCompiling ? "compiling" : EditorApplication.isUpdating ? "importing" : "ready";
                Heartbeat(phase, phase == "ready");
            }
            catch (Exception exception)
            {
                // Preserve the owned Editor and expose liveness/stop diagnostics.
                Debug.LogError("Splinter bridge: " + exception);
            }
        }

        private static string UnsavedReason()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return "Stop Play before closing or validating the Editor.";
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) return "Unsaved scene: " + SceneManager.GetSceneAt(i).path;
            return null;
        }
        private static void Heartbeat(string phase, bool ready)
        {
            // Product liveness must not execute project or qualification code.
            // Loaded-code evidence belongs to the fixture's explicit inspect request.
            Write("heartbeat.json", new HeartbeatData
            {
                pid = Pid, startTimeUtcTicks = StartTicks, projectPath = Path.GetDirectoryName(Application.dataPath),
                token = token, phase = phase, ready = ready, observedAt = DateTimeOffset.UtcNow.ToString("O")
            });
        }
        private static T Read<T>(string name) where T : class
        {
            string path = Path.Combine(control, name);
            if (!File.Exists(path)) return null;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new StreamReader(stream)) return JsonUtility.FromJson<T>(reader.ReadToEnd());
        }
        private static void Write(string name, object data)
        {
            string path = Path.Combine(control, name);
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(data));
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }
        [Serializable] private class HeartbeatData
        {
            public int pid; public long startTimeUtcTicks; public string projectPath; public string token;
            public string phase; public bool ready; public string observedAt;
        }
        [Serializable] private class StopRequest { public string requestId; public string token; }
        [Serializable] private class StopResult { public string requestId; public string error; }
    }

#if UNITY_EDITOR
    internal static class OwnedEditorWindow
    {
#if UNITY_EDITOR_WIN
        private const uint GetOwner = 4;
        private const uint CloseMessage = 0x0010;
        private delegate bool VisitWindow(IntPtr hwnd, IntPtr parameter);

        // A synchronous quit can unload Mono while this update callback is still on its stack.
        // Post to the main thread's own Unity window so the native message loop dispatches it later.
        internal static string PostClose()
        {
            IntPtr main = IntPtr.Zero;
            int matches = 0;
            var className = new StringBuilder(64);
            EnumThreadWindows(GetCurrentThreadId(), (window, parameter) =>
            {
                if (IsWindowVisible(window) && GetWindow(window, GetOwner) == IntPtr.Zero &&
                    GetClassNameW(window, className, className.Capacity) != 0 &&
                    className.ToString() == "UnityContainerWndClass")
                {
                    main = window;
                    matches++;
                }
                return true;
            }, IntPtr.Zero);
            if (matches != 1 || !IsWindowEnabled(main))
                return "Could not identify one enabled Unity main window on the Editor thread.";
            if (!PostMessageW(main, CloseMessage, IntPtr.Zero, IntPtr.Zero))
                return "Windows refused the Editor close message, error " + Marshal.GetLastWin32Error() + ".";
            return null;
        }

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();
        [DllImport("user32.dll")]
        private static extern bool EnumThreadWindows(uint thread, VisitWindow visitor, IntPtr parameter);
        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll")]
        private static extern bool IsWindowEnabled(IntPtr window);
        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr window, uint command);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern int GetClassNameW(IntPtr window, StringBuilder name, int count);
        [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern bool PostMessageW(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
#else
        internal static string PostClose() => "Owned Editor shutdown requires Windows.";
#endif
    }
#endif
}
