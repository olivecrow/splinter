using UnityEngine;
public class SettingsProbeFrames : MonoBehaviour
{
    public static int Frames;
    void Awake() { Application.runInBackground=true; Frames=0; }
    void Update() { Frames++; }
}
