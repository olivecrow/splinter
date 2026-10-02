using System.Runtime.CompilerServices;
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
            UnityEngine.Application.dataPath = Path.Combine(control, "Assets");
            RuntimeHelpers.RunClassConstructor(typeof(Splinter.Editor.WorkspaceBridge).TypeHandle);
            var observations = new List<JsonElement>();
            EditorApplication.Advance(); Capture();
            EditorApplication.isCompiling = true;
            EditorApplication.Advance(); Capture();
            EditorApplication.isCompiling = false;
            EditorApplication.Advance(); Capture();
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                fixtureCalls = SplinterSnapshotProbe.Calls,
                errors = UnityEngine.Debug.Errors,
                heartbeats = observations
            }));

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
