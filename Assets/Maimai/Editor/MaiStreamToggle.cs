// Switch the online-songs side screen on/off (off until the vrcmai-songs Pages site exists).
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class MaiStreamToggle
{
    [MenuItem("Maimai/Stream: Toggle Side Screen")]
    public static void Toggle() { Set(!Find().activeSelf); }
    static GameObject Find() { foreach (var t in Object.FindObjectsOfType<Transform>(true)) if (t.name == "StreamScreen") return t.gameObject; return null; }
    static void Set(bool on) { var g = Find(); Undo.RecordObject(g, "Stream screen"); g.SetActive(on); Debug.Log("MAISTREAM side screen " + (on ? "ON" : "OFF")); }
    public static void BatchOff()
    {
        EditorSceneManager.OpenScene(MaiBatchWorld.World);
        Set(false);
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        EditorApplication.Exit(0);
    }
}
