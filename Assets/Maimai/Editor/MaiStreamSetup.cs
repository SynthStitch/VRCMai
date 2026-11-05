// Streamed songs, step 2: side-screen browser between the cabinets + one baked URL per slot.
using TMPro;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;

public static class MaiStreamSetup
{
    // where Stream/site is published (GitHub Pages). Changing it = run this setup again + re-upload the world.
    public const string BaseUrl = "https://synthstitch.github.io/vrcmai-songs/";
    const string Prog = "Assets/Maimai/Runtime/MaiStream.asset";
    const float Px = 0.0008f; // metres per canvas pixel

    [MenuItem("Maimai/Stream: Setup Side Screen")]
    public static void Setup()
    {
        var p1 = GameObject.Find("MaiCabinet/StartButton").GetComponent<MaiGame>();
        var p2go = GameObject.Find("MaiCabinetP2/StartButton");
        var p2 = p2go != null ? p2go.GetComponent<MaiGame>() : null;
        if (AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(Prog) == null)
        {
            var pa = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
            pa.sourceCsScript = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/Maimai/Runtime/MaiStream.cs");
            AssetDatabase.CreateAsset(pa, Prog);
            AssetDatabase.SaveAssets();
            UdonSharpProgramAsset.CompileAllCsPrograms(true);
        }

        var old = GameObject.Find("StreamScreen");
        if (old != null) Undo.DestroyObjectImmediate(old);
        var root = new GameObject("StreamScreen");
        Undo.RegisterCreatedObjectUndo(root, "Stream screen");
        // between the two round screens, same tilt, a little proud of the bezel
        Vector3 mid = p2 != null ? (p1.screen.position + p2.screen.position) * 0.5f : p1.screen.position + p1.screen.right * 0.5f;
        root.transform.SetPositionAndRotation(mid - p1.screen.forward * 0.1f, p1.screen.rotation); // clear of the side box buttons

        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        root.AddComponent<GraphicRaycaster>();
        root.AddComponent<VRC.SDK3.Components.VRCUiShape>(); // laser-pointer interaction
        var rt = root.GetComponent<RectTransform>();
        const float W = 250, H = 450;
        rt.sizeDelta = new Vector2(W, H);
        rt.localScale = Vector3.one * Px;
        var bg = root.AddComponent<Image>();
        bg.color = new Color(0.016f, 0.016f, 0.018f, 1f);

        var mono = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Maimai/Fonts/SpaceMono SDF.asset");
        TextMeshProUGUI Text(string name, Transform parent, Vector2 pos, Vector2 size, float fs, TextAlignmentOptions a, string txt)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var r = go.GetComponent<RectTransform>();
            r.anchoredPosition = pos; r.sizeDelta = size;
            var t = go.AddComponent<TextMeshProUGUI>();
            t.font = mono; t.fontSize = fs; t.alignment = a; t.text = txt; t.color = Color.white;
            t.enableWordWrapping = false; t.overflowMode = TextOverflowModes.Overflow; t.raycastTarget = false; // Ellipsis would stop at the first long line
            return t;
        }
        Text("Label", root.transform, new Vector2(0, H / 2 - 18), new Vector2(W - 20, 24), 13, TextAlignmentOptions.Left, "<color=#8A8A8A>05  ONLINE SONGS</color>");
        var status = Text("Status", root.transform, new Vector2(0, H / 2 - 40), new Vector2(W - 20, 20), 11, TextAlignmentOptions.Left, "");
        var list = Text("List", root.transform, new Vector2(0, 20), new Vector2(W - 20, 270), 14, TextAlignmentOptions.TopLeft, "");
        list.lineSpacing = 12;

        var stream = UdonSharpUndo.AddComponent<MaiStream>(root);
        var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(stream);
        void Btn(string label, string evt, float x, float y, float w)
        {
            var go = new GameObject("Btn " + evt, typeof(RectTransform));
            go.transform.SetParent(root.transform, false);
            var r = go.GetComponent<RectTransform>();
            r.anchoredPosition = new Vector2(x, y); r.sizeDelta = new Vector2(w, 34);
            var img = go.AddComponent<Image>();
            img.color = new Color(1, 1, 1, 0.08f);
            var b = go.AddComponent<Button>();
            var cb = b.colors; cb.highlightedColor = new Color(1, 0.35f, 0.21f, 0.6f); cb.pressedColor = new Color(1, 0.35f, 0.21f, 1); b.colors = cb;
            UnityEventTools.AddStringPersistentListener(b.onClick, ub.SendCustomEvent, evt);
            Text("Label", go.transform, Vector2.zero, new Vector2(w, 34), 14, TextAlignmentOptions.Center, label);
        }
        float bw = (W - 50) / 4f, y1 = -H / 2 + 102, y2 = -H / 2 + 62, y3 = -H / 2 + 22;
        Btn("▲", "Up", -1.5f * (bw + 10), y1, bw); Btn("▼", "Down", -0.5f * (bw + 10), y1, bw);
        Btn("«", "PageUp", 0.5f * (bw + 10), y1, bw); Btn("»", "PageDown", 1.5f * (bw + 10), y1, bw);
        Btn("<color=#FF5A36>→ P1</color>", "ToP1", -(W - 30) / 4f - 2.5f, y2, (W - 30) / 2f);
        Btn("<color=#FF5A36>→ P2</color>", "ToP2", (W - 30) / 4f + 2.5f, y2, (W - 30) / 2f);
        Btn("refresh list", "Refresh", 0, y3, W - 20);

        stream.list = list;
        stream.status = status;
        stream.games = p2 != null ? new[] { p1, p2 } : new[] { p1 };
        stream.indexUrl = new VRCUrl(BaseUrl + "index.txt");
        stream.chartUrls = new VRCUrl[MaiStreamExport.Slots];
        stream.audioUrls = new VRCUrl[MaiStreamExport.Slots];
        for (int i = 0; i < MaiStreamExport.Slots; i++)
        {
            stream.chartUrls[i] = new VRCUrl(BaseUrl + "c/" + i.ToString("000") + ".txt");
            stream.audioUrls[i] = new VRCUrl(BaseUrl + "a/" + i.ToString("000") + ".mp3");
        }
        UdonSharpEditorUtility.CopyProxyToUdon(stream);
        foreach (var g in stream.games) { g.stream = stream; UdonSharpEditorUtility.CopyProxyToUdon(g); EditorUtility.SetDirty(g); }
        Debug.Log($"MAISTREAM side screen at {root.transform.position}, {MaiStreamExport.Slots} slots under {BaseUrl}, games {stream.games.Length}");
    }

    // batch: setup + save, then fake an index (not saved) and render the screen
    public static void Batch()
    {
        UdonSharp.Compiler.UdonSharpCompilerV1.CompileSync();
        if (UdonSharpProgramAsset.AnyUdonSharpScriptHasError()) { Debug.LogError("MAISTREAM U# errors"); EditorApplication.Exit(1); return; }
        EditorSceneManager.OpenScene(MaiBatchWorld.World);
        Setup();
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        var stream = Object.FindObjectOfType<MaiStream>();
        if (System.IO.File.Exists(MaiStreamExport.Site + "/index.txt"))
        {
            var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            typeof(MaiStream).GetMethod("Parse", bf).Invoke(stream, new object[] { System.IO.File.ReadAllText(MaiStreamExport.Site + "/index.txt") });
            stream.status.text = "<color=#8A8A8A>" + stream.count + " online songs</color>";
            stream.Down();
        }
        string outDir = System.Environment.GetEnvironmentVariable("MAI_PREVIEW_DIR") ?? "Temp";
        var eye = new GameObject("eye").AddComponent<Camera>();
        var t = stream.transform;
        eye.transform.position = t.position - t.forward * 0.55f;
        eye.transform.rotation = t.rotation;
        eye.fieldOfView = 50;
        var rtx = new RenderTexture(900, 900, 24);
        eye.targetTexture = rtx;
        eye.Render();
        RenderTexture.active = rtx;
        var tex = new Texture2D(900, 900, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 900, 900), 0, 0);
        System.IO.File.WriteAllBytes(outDir + "/stream_screen.png", tex.EncodeToPNG());
        Debug.Log("MAISTREAM rendered, count " + stream.count);
        EditorApplication.Exit(0);
    }
}
