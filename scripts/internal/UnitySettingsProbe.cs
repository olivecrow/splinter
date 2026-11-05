// Copy only through test-unity-shared-settings.ps1 into its fresh scratch fixture.
using System;
using System.IO;
using System.Diagnostics;
using System.Reflection;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;

[Serializable] public class SettingsProbeRequest { public string id, action; }
[Serializable] public class SettingsProbeReply
{
    public string id, action, project, preferences, domain, error, lastMode, exitEventFor;
    public int pid, frames, acquisitionDeferrals;
    public bool playing, changingPlay, compiling, updating, oldExitPredicateWouldBlock, searchReady;
}
[InitializeOnLoad] public static class SplinterSettingsProbe
{
    static readonly string Project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    static readonly string Domain = Guid.NewGuid().ToString("N");
    const string Pending = "SplinterPrefsProbe.Pending", Origin = "SplinterPrefsProbe.Origin";
    const string LastMode = "SplinterPrefsProbe.LastMode", ExitFor = "SplinterPrefsProbe.ExitFor";
    // This is an external diagnostic for a freshly generated fixture, not a product bridge/API.
    static SplinterSettingsProbe()
    {
        string token=Argument("-splinterSettingsProbe");
        string marker=Path.Combine(Project,".splinter-settings-probe");
        if(string.IsNullOrEmpty(token) || !File.Exists(marker) || File.ReadAllText(marker)!=token) return;
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += Mode;
        AssemblyReloadEvents.beforeAssemblyReload += () => Trace("beforeReload");
        AssemblyReloadEvents.afterAssemblyReload += () => Trace("afterReload");
        Trace("domainLoaded");
    }
    static string Argument(string name)
    {
        var args=Environment.GetCommandLineArgs();
        for(int i=0;i+1<args.Length;i++)if(args[i]==name)return args[i+1];
        return "";
    }
    static bool CleanScenes()
    {
        for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)return false;
        return true;
    }
    static void Trace(string action)
    {
        var state = State(new SettingsProbeRequest {id="trace",action=action});
        File.AppendAllText(Path.Combine(Project,"mode-trace.jsonl"),JsonUtility.ToJson(state)+"\n");
    }
    static void Mode(PlayModeStateChange state)
    {
        SessionState.SetString(LastMode,state.ToString());
        var pending = SessionState.GetString(Pending,"");
        if (state == PlayModeStateChange.EnteredEditMode && pending.Length != 0)
        {
            var request = JsonUtility.FromJson<SettingsProbeRequest>(pending);
            if (request.action == "exit") SessionState.SetString(ExitFor,request.id);
        }
        Trace(state.ToString());
    }
    static void PrepareSearch()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Prepare the fixture index in Edit mode");
        foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var search=assembly.GetType("UnityEditor.Search.SearchDatabase");
            if(search==null)continue;
            search.GetMethod("GetDefaultSearchDatabase",BindingFlags.Static|BindingFlags.Public).Invoke(null,null);
            return;
        }
        throw new InvalidOperationException("This Editor has no expected Search database API");
    }
    static bool SearchReady()
    {
        if (!File.Exists(Path.Combine(Project,"UserSettings","Search.index"))) return false;
        foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var type=assembly.GetType("UnityEditor.Search.SearchDatabase");
            if(type==null)continue;
            foreach(var db in Resources.FindObjectsOfTypeAll(type))
            {
                var path=(string)type.GetProperty("path").GetValue(db,null);
                if(path=="UserSettings/Search.index")return (bool)type.GetProperty("ready").GetValue(db,null) && !(bool)type.GetProperty("updating").GetValue(db,null);
            }
        }
        return false;
    }
    static SettingsProbeReply State(SettingsProbeRequest request)
    {
        return new SettingsProbeReply { id=request.id,action=request.action,project=Project,
            preferences=InternalEditorUtility.unityPreferencesFolder,domain=Domain,pid=Process.GetCurrentProcess().Id,
            frames=SettingsProbeFrames.Frames,acquisitionDeferrals=SessionState.GetInt("SplinterPrefsProbe.ReadDeferrals",0),playing=EditorApplication.isPlaying,
            changingPlay=EditorApplication.isPlayingOrWillChangePlaymode,compiling=EditorApplication.isCompiling,
            updating=EditorApplication.isUpdating,searchReady=SearchReady(),error="",lastMode=SessionState.GetString(LastMode,""),
            exitEventFor=SessionState.GetString(ExitFor,""),
            oldExitPredicateWouldBlock=request.action=="exit" && SessionState.GetString(Origin,"")==Domain };
    }
    static void Reply(SettingsProbeRequest request,string error="")
    {
        var reply=State(request);reply.error=error;
        string target=Path.Combine(Project,"response-"+request.id+".json"),pending=target+".pending";
        File.WriteAllText(pending,JsonUtility.ToJson(reply,true));File.Move(pending,target);
    }
    static void Tick()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (File.Exists(Path.Combine(Project,"cancel-request")))
        {
            if (EditorApplication.isPlaying) {EditorApplication.isPlaying=false;return;}
            if (EditorApplication.isPlayingOrWillChangePlaymode || !CleanScenes()) return;
            EditorApplication.Exit(0);return;
        }
        var saved=SessionState.GetString(Pending,"");
        if (saved.Length!=0)
        {
            var request=JsonUtility.FromJson<SettingsProbeRequest>(saved);
            if ((request.action=="setup" || request.action=="ready") && !SearchReady()) return;
            if (request.action=="reload" && SessionState.GetString(Origin,"")==Domain) return;
            if (request.action=="enter" && (!EditorApplication.isPlaying || SettingsProbeFrames.Frames<1 || SessionState.GetString(LastMode,"")!="EnteredPlayMode")) return;
            if (request.action=="exit" && (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetString(ExitFor,"")!=request.id)) return;
            SessionState.EraseString(Pending);Reply(request);return;
        }
        string path=Path.Combine(Project,"request.json");if(!File.Exists(path))return;
        SettingsProbeRequest current;
        try
        {
            // Publication may still hold DELETE access after its new name is visible.
            // Read the immutable request with delete sharing; no action has run yet.
            using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read|FileShare.Delete))
            using(var reader=new StreamReader(stream))current=JsonUtility.FromJson<SettingsProbeRequest>(reader.ReadToEnd());
            File.Delete(path);
        }
        catch(IOException ex) when((ex.HResult & 0xffff)==32 || (ex.HResult & 0xffff)==33)
        {
            SessionState.SetInt("SplinterPrefsProbe.ReadDeferrals",SessionState.GetInt("SplinterPrefsProbe.ReadDeferrals",0)+1);
            return; // Retry only acquisition of this untouched request on a later tick.
        }
        if(string.IsNullOrEmpty(current.id) || Array.Exists(current.id.ToCharArray(),c=>!char.IsLetterOrDigit(c) && c!='-'))throw new InvalidDataException("Invalid diagnostic request id");
        try
        {
            if (current.action=="setup")
            {
                EditorSettings.enterPlayModeOptionsEnabled=false;Application.runInBackground=true;
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                new GameObject("Native preferences frame probe").AddComponent<SettingsProbeFrames>();
                EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),"Assets/PrefsProbe.unity");
                PrepareSearch();
                SessionState.SetString(Pending,JsonUtility.ToJson(current));return;
            }
            if(current.action=="status"){Reply(current);return;}
            if(current.action=="quit")
            {
                if(EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode || !CleanScenes())throw new InvalidOperationException("Stop Play and save fixture scenes before normal quit");
                Reply(current);EditorApplication.Exit(0);return;
            }
            SessionState.SetString(Pending,JsonUtility.ToJson(current));SessionState.SetString(Origin,Domain);
            if(current.action=="ready"){PrepareSearch();}
            else if(current.action=="reload")EditorUtility.RequestScriptReload();
            else if(current.action=="enter")EditorApplication.isPlaying=true;
            else if(current.action=="exit"){SessionState.EraseString(ExitFor);EditorApplication.isPlaying=false;}
            else{SessionState.EraseString(Pending);throw new InvalidOperationException("Unknown action");}
        }
        catch(Exception ex){SessionState.EraseString(Pending);Reply(current,ex.ToString());}
    }
}
