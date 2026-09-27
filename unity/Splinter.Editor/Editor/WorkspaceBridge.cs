// Included only in a Splinter-owned workspace. No global EditorPrefs writes.
using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Splinter.Editor
{
    [InitializeOnLoad]
    public static class WorkspaceBridge
    {
        private static string control = "";
        private static string token = "";
        private static Pending pending;
        private static object compilationContext;
        private static double nextTick;
        private static bool coplayShutdownGuard;
        private static bool incrementalCompilation;
        private static readonly int Pid = System.Diagnostics.Process.GetCurrentProcess().Id;
        private static readonly long StartTicks = System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime().Ticks;

        static WorkspaceBridge()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
            {
                if (args[i] == "-splinterControlPath") control = args[++i];
                else if (args[i] == "-splinterToken") token = args[++i];
                else if (args[i] == "-splinterIncrementalCompilation") incrementalCompilation = args[++i] == "1";
            }
            if (string.IsNullOrEmpty(control) || string.IsNullOrEmpty(token)) return;
            GuardCoplayShutdown();
            pending = Read<Pending>("pending.json");
            if (pending != null && pending.token != token) pending = null;
            CompilationPipeline.compilationStarted += CompilationStarted;
            CompilationPipeline.assemblyCompilationFinished += AssemblyFinished;
            CompilationPipeline.compilationFinished += CompilationFinished;
            EditorApplication.update += Tick;
            EditorApplication.quitting += () => Heartbeat("exiting", false);
            // A reload is evidence that successful compilation has crossed into a new domain.
            if (pending != null && pending.finished && pending.errors.Length == 0)
            {
                pending.reloaded = true;
                SavePending();
            }
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
                if (pending != null && pending.finished && !busy &&
                    (pending.errors.Length != 0 || pending.reloaded) && DateTime.UtcNow.Ticks - pending.finishedTicks > TimeSpan.TicksPerSecond)
                {
                    Write("validation.json", new Validation
                    {
                        requestId = pending.requestId, fingerprint = pending.fingerprint,
                        success = pending.errors.Length == 0, errors = pending.errors,
                        completedAt = DateTimeOffset.UtcNow.ToString("O")
                    });
                    pending = null;
                    File.Delete(Path.Combine(control, "pending.json"));
                }
                ValidationRequest request = Read<ValidationRequest>("validation-request.json");
                Validation completed = Read<Validation>("validation.json");
                if (pending == null && !busy && request != null && request.token == token &&
                    !string.IsNullOrEmpty(request.requestId) && (completed == null || completed.requestId != request.requestId))
                {
                    if (UnsavedReason() != null)
                    {
                        Write("validation.json", new Validation { requestId = request.requestId, fingerprint = request.fingerprint,
                            success = false, completedAt = DateTimeOffset.UtcNow.ToString("O"), errors = new[] { "Editor has unsaved scene or Play state." } });
                    }
                    else
                    {
                        pending = new Pending { requestId = request.requestId, fingerprint = request.fingerprint, token = token, errors = new string[0] };
                        SavePending();
                        // Explicit refresh is required even when the user's Auto Refresh is off.
                        if (incrementalCompilation)
                        {
                            AssetDatabase.Refresh();
                            CompilationPipeline.RequestScriptCompilation();
                        }
                        else
                        {
                            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                            CompilationPipeline.RequestScriptCompilation(RequestScriptCompilationOptions.CleanBuildCache);
                        }
                    }
                }
                string phase = EditorApplication.isCompiling ? "compiling" : EditorApplication.isUpdating ? "importing" : pending != null ? "validating" : "ready";
                Heartbeat(phase, phase == "ready");
            }
            catch (Exception exception)
            {
                // Preserve pending work and expose diagnostics; never turn an exception into success.
                Debug.LogError("Splinter bridge: " + exception);
            }
        }

        private static void CompilationStarted(object context)
        {
            if (pending == null) return;
            compilationContext = context;
            pending.started = true;
            pending.finished = false;
            pending.reloaded = false;
            pending.errors = new string[0];
            SavePending();
        }
        private static void AssemblyFinished(string assembly, CompilerMessage[] messages)
        {
            if (pending == null || !pending.started) return;
            var errors = new List<string>(pending.errors);
            foreach (CompilerMessage message in messages)
                if (message.type == CompilerMessageType.Error)
                    errors.Add(message.file + "(" + message.line + "): " + message.message);
            pending.errors = errors.ToArray();
            SavePending();
        }
        private static void CompilationFinished(object context)
        {
            if (pending == null || !pending.started || !ReferenceEquals(context, compilationContext)) return;
            pending.finished = true;
            pending.finishedTicks = DateTime.UtcNow.Ticks;
            SavePending();
            // Do not run more Unity operations here. Success waits for the following domain reload.
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
                token = token, phase = phase, ready = ready, coplayShutdownGuard = coplayShutdownGuard, observedAt = DateTimeOffset.UtcNow.ToString("O"),
                validation = Read<Validation>("validation.json")
            });
        }
        private static void SavePending() { Write("pending.json", pending); }
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
        [Serializable] private class ValidationRequest { public string requestId; public string fingerprint; public string token; }
        [Serializable] private class Pending
        {
            public string requestId; public string fingerprint; public string token;
            public bool started; public bool finished; public bool reloaded; public long finishedTicks; public string[] errors;
        }
        [Serializable] private class Validation
        {
            public string requestId; public string fingerprint; public bool success; public string completedAt; public string[] errors;
        }
        [Serializable] private class HeartbeatData
        {
            public int pid; public long startTimeUtcTicks; public string projectPath; public string token;
            public string phase; public bool ready; public string observedAt; public Validation validation;
            public bool coplayShutdownGuard;
        }
        [Serializable] private class StopRequest { public string requestId; public string token; }
        [Serializable] private class StopResult { public string requestId; public string error; }
    }
}
