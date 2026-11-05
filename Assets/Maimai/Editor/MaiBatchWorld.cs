// One-off: merge the cabinet scene into the baked room scene and save it as Assets/Scenes/MaiWorld.unity.
// Keeps the room's baked lighting. Leaves the old map, Floor and Directional Light behind.
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MaiBatchWorld
{
    public const string World = "Assets/Scenes/MaiWorld.unity";

    public static void Merge()
    {
        var room = EditorSceneManager.OpenScene("Assets/Room/Room.unity", OpenSceneMode.Single);
        var old = EditorSceneManager.OpenScene("Assets/Scenes/VRCDefaultWorldScene.unity", OpenSceneMode.Additive);
        string[] take = { "VRCWorld", "Main Camera", "EventSystem", "MaiCabinet", "CabinetModel" };
        foreach (var go in old.GetRootGameObjects())
            if (System.Array.IndexOf(take, go.name) >= 0) { SceneManager.MoveGameObjectToScene(go, room); Debug.Log("MAIWORLD moved " + go.name); }
            else Debug.Log("MAIWORLD left " + go.name);
        EditorSceneManager.CloseScene(old, true);
        SceneManager.SetActiveScene(room);
        EditorSceneManager.SaveScene(room, World, true); // save a copy; the room asset's own scene stays untouched
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(World, true) };

        var w = GameObject.Find("VRCWorld");
        var desc = Object.FindObjectOfType<VRC.SDKBase.VRC_SceneDescriptor>();
        Debug.Log($"MAIWORLD VRCWorld at {w.transform.position} rot {w.transform.eulerAngles.y:F0}; spawns {(desc.spawns == null ? 0 : desc.spawns.Length)}" +
                  (desc.spawns != null && desc.spawns.Length > 0 && desc.spawns[0] != null ? $" first {desc.spawns[0].position}" : "") +
                  $"; cabinet at {GameObject.Find("MaiCabinet").transform.position}");
        foreach (var r in room.GetRootGameObjects())
            foreach (var mr in r.GetComponentsInChildren<MeshRenderer>())
                if (mr.name.Contains("Floor") || mr.name.Contains("floor")) { Debug.Log($"MAIWORLD floor {mr.name} bounds {mr.bounds}"); break; }
        EditorApplication.Exit(0);
    }
}
