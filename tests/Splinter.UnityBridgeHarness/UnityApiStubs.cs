// Test-only substitutes for Unity's external API. WorkspaceBridge.cs itself is linked unchanged.
namespace UnityEditor
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class InitializeOnLoadAttribute : Attribute { }
    public enum ImportAssetOptions { ForceUpdate }
    public static class AssetDatabase
    {
        public static void Refresh() { }
        public static void Refresh(ImportAssetOptions options) { }
    }
    public static class EditorApplication
    {
        public static double timeSinceStartup;
        public static bool isCompiling, isUpdating, isPlayingOrWillChangePlaymode;
        public static event Action update;
        public static event Action quitting;
        public static void Advance() { timeSinceStartup++; update?.Invoke(); }
        public static void Exit(int code) { quitting?.Invoke(); }
    }
}

namespace UnityEditor.Compilation
{
    public enum CompilerMessageType { Error, Warning }
    public enum RequestScriptCompilationOptions { CleanBuildCache }
    public struct CompilerMessage
    {
        public CompilerMessageType type;
        public string file, message;
        public int line;
    }
    public static class CompilationPipeline
    {
        public static event Action<object> compilationStarted;
        public static event Action<string, CompilerMessage[]> assemblyCompilationFinished;
        public static event Action<object> compilationFinished;
        public static void RequestScriptCompilation() { }
        public static void RequestScriptCompilation(RequestScriptCompilationOptions options) { }
        public static void Complete(object context, CompilerMessage[] messages)
        {
            compilationStarted?.Invoke(context);
            assemblyCompilationFinished?.Invoke("Assembly-CSharp", messages);
            compilationFinished?.Invoke(context);
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
        public static int sceneCount => 0;
        public static Scene GetSceneAt(int index) => default;
    }
}
