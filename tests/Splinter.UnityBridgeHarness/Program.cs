using System.Runtime.CompilerServices;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.Json;
using UnityEditor;

namespace Splinter.UnityBridgeHarness
{
    public static class HarnessMarker { }

    internal static class Program
    {
        public static void Main(string[] args)
        {
            string control = args[Array.IndexOf(args, "-splinterControlPath") + 1];
            string token = args[Array.IndexOf(args, "-splinterToken") + 1];
            string externalTool = args[Array.IndexOf(args, "--external-tool") + 1];
            int externalLookups = 0, externalQuitCalls = 0;
            if (externalTool == "changed-hook")
            {
                // Simulate a user package whose private shutdown API has changed.
                // Resolving this assembly is possible, but Splinter must never inspect it.
                var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("MCPForUnity.Editor"), AssemblyBuilderAccess.Run);
                assembly.DefineDynamicModule("external-tool").DefineType("MCPForUnity.Editor.Services.McpEditorShutdownCleanup",
                    TypeAttributes.Public).CreateType();
                AppDomain.CurrentDomain.AssemblyResolve += (_, request) =>
                {
                    if (new AssemblyName(request.Name).Name != "MCPForUnity.Editor") return null;
                    externalLookups++;
                    return assembly;
                };
            }
            EditorApplication.quitting += () => externalQuitCalls++;
            UnityEngine.Application.dataPath = Path.Combine(control, "Assets");
            RuntimeHelpers.RunClassConstructor(typeof(Splinter.Editor.WorkspaceBridge).TypeHandle);
            var observations = new List<JsonElement>();
            EditorApplication.Advance(); Capture();
            EditorApplication.isCompiling = true;
            EditorApplication.Advance(); Capture();
            EditorApplication.isCompiling = false;
            EditorApplication.Advance(); Capture();
            Stop("wrong-token");
            bool wrongTokenBlocked = !EditorApplication.Exited;
            Stop(token);
            EditorApplication.isCompiling = true;
            EditorApplication.Advance();
            bool compilingBlocked = !EditorApplication.Exited;
            EditorApplication.isCompiling = false;
            EditorApplication.isUpdating = true;
            EditorApplication.Advance();
            bool importingBlocked = !EditorApplication.Exited;
            EditorApplication.isUpdating = false;
            EditorApplication.isPlayingOrWillChangePlaymode = true;
            EditorApplication.Advance();
            string playRefusal = Refusal();
            EditorApplication.isPlayingOrWillChangePlaymode = false;
            UnityEngine.SceneManagement.SceneManager.Scenes = [new() { isDirty = true, path = "Assets/Unsaved.unity" }];
            EditorApplication.Advance();
            string sceneRefusal = Refusal();
            UnityEngine.SceneManagement.SceneManager.Scenes = [];
            int quitCallsBeforeStop = externalQuitCalls;
            EditorApplication.Advance(); Capture();
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                fixtureCalls = SplinterSnapshotProbe.Calls,
                errors = UnityEngine.Debug.Errors,
                heartbeats = observations,
                externalLookups, externalQuitCalls, quitCallsBeforeStop,
                exited = EditorApplication.Exited, wrongTokenBlocked, compilingBlocked, importingBlocked,
                playRefusal, sceneRefusal
            }));

            void Stop(string requestToken)
            {
                File.WriteAllText(Path.Combine(control, "stop-request.json"),
                    JsonSerializer.Serialize(new { requestId = "test-stop", token = requestToken }));
                if (requestToken != token) EditorApplication.Advance();
            }
            string Refusal()
            {
                using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(control, "stop-result.json")));
                if (document.RootElement.GetProperty("requestId").GetString() != "test-stop")
                    throw new InvalidOperationException("Stop refusal lost request identity.");
                return document.RootElement.GetProperty("error").GetString();
            }

            void Capture()
            {
                string path = Path.Combine(control, "heartbeat.json");
                using var document = JsonDocument.Parse(File.Exists(path) ? File.ReadAllText(path) : "null");
                observations.Add(document.RootElement.Clone());
            }
        }
    }
}

// The assembly and type match the old product bridge's reflection target.
// If product liveness calls user code, the heartbeat stops and the test fails.
public static class SplinterSnapshotProbe
{
    public static int Calls;
    public static int Calculate(int value)
    {
        Calls++;
        throw new InvalidOperationException("Project code must not run in the product heartbeat.");
    }
}
