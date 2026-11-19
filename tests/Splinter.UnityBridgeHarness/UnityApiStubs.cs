// Test-only substitutes for Unity's external API. WorkspaceBridge.cs itself is linked unchanged.
namespace UnityEditor
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class InitializeOnLoadAttribute : Attribute { }
    public static class SessionState
    {
        private static readonly Dictionary<string, string> Values = new();
        public static string GetString(string key, string fallback) => Values.GetValueOrDefault(key, fallback);
        public static void SetString(string key, string value) => Values[key] = value;
    }
    public static class EditorApplication
    {
        public static double timeSinceStartup;
        public static bool isCompiling, isUpdating, isPlayingOrWillChangePlaymode;
        public static event Action update;
        public static event Action quitting;
        public static bool Exited;
        public static bool MainWindowAvailable = true, VetoQuit, ClosePosted;
        public static int CloseRequests;
        public static void Advance()
        {
            timeSinceStartup++;
            update?.Invoke();
            if (!ClosePosted) return;
            ClosePosted = false;
            if (!VetoQuit) { Exited = true; quitting?.Invoke(); }
        }
    }
}

namespace Splinter.Editor
{
    internal static class OwnedEditorWindow
    {
        internal static string PostClose()
        {
            UnityEditor.EditorApplication.CloseRequests++;
            if (!UnityEditor.EditorApplication.MainWindowAvailable) return "No eligible main window.";
            UnityEditor.EditorApplication.ClosePosted = true;
            return null;
        }
    }
}

namespace UnityEngine
{
    public static class Application { public static string dataPath; }
    public static class Debug
    {
        public static readonly List<string> Errors = [];
        public static void LogError(string message) => Errors.Add(message);
    }
    public static class JsonUtility
    {
        private static readonly System.Text.Json.JsonSerializerOptions Options = new() { IncludeFields = true };
        public static T FromJson<T>(string json) => System.Text.Json.JsonSerializer.Deserialize<T>(json, Options);
        public static string ToJson(object value) => System.Text.Json.JsonSerializer.Serialize(value, Options);
    }
}

namespace UnityEngine.SceneManagement
{
    public struct Scene { public bool isDirty; public string path; }
    public static class SceneManager
    {
        public static Scene[] Scenes = [];
        public static int sceneCount => Scenes.Length;
        public static Scene GetSceneAt(int index) => Scenes[index];
    }
}
