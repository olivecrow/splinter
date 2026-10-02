// Included only in a Splinter-owned workspace. No global EditorPrefs writes.
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Splinter.Editor
{
    [InitializeOnLoad]
    public static class WorkspaceBridge
    {
        private static string control = "";
        private static string token = "";
        private static double nextTick;
        private static bool coplayShutdownGuard;
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
            GuardCoplayShutdown();
            EditorApplication.update += Tick;
            EditorApplication.quitting += () => Heartbeat("exiting", false);
        }

        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup < nextTick) return;
            nextTick = EditorApplication.timeSinceStartup + 0.5;
            try
            {
                bool busy = EditorApplication.isCompiling || EditorApplication.isUpdating;
                StopRequest stop = Read<StopRequest>("stop-request.json");
                if (stop != null && stop.token == token && !busy)
                {
                    string reason = UnsavedReason();
                    if (reason != null) Write("stop-result.json", new StopResult { requestId = stop.requestId, error = reason });
                    else { GuardCoplayShutdown(); Heartbeat("exiting", false); EditorApplication.Exit(0); return; }
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
        private static void GuardCoplayShutdown()
        {
            // Coplay 10.2.0 stores its server handshake in global EditorPrefs.
            // A worker's quit callback can therefore stop the original Editor's server.
            // Unsubscribe only that callback in this owned process; never edit global
            // preferences or package files. Splinter workers do not own MCP servers.
            Type cleanup = Type.GetType("MCPForUnity.Editor.Services.McpEditorShutdownCleanup, MCPForUnity.Editor");
            if (cleanup == null) return;
            var method = cleanup.GetMethod("OnEditorQuitting", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (method == null) throw new InvalidOperationException("Unsupported Coplay shutdown hook; preserve the Editor for inspection.");
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(cleanup.TypeHandle);
            EditorApplication.quitting -= (Action)Delegate.CreateDelegate(typeof(Action), method);
            coplayShutdownGuard = true;
        }
        private static void Heartbeat(string phase, bool ready)
        {
            // Product liveness must not execute project or qualification code.
            // Loaded-code evidence belongs to the fixture's explicit inspect request.
            Write("heartbeat.json", new HeartbeatData
            {
                pid = Pid, startTimeUtcTicks = StartTicks, projectPath = Path.GetDirectoryName(Application.dataPath),
                token = token, phase = phase, ready = ready, coplayShutdownGuard = coplayShutdownGuard, observedAt = DateTimeOffset.UtcNow.ToString("O")
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
            public bool coplayShutdownGuard;
        }
        [Serializable] private class StopRequest { public string requestId; public string token; }
        [Serializable] private class StopResult { public string requestId; public string error; }
    }
}
