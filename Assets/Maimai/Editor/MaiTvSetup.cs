// Room TV scoreboard: a dark card + TMP texts laid over the "TV" mesh's screen face, wired to both cabinets.
using TMPro;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MaiTvSetup
{
    const string Fonts = "Assets/Maimai/Fonts", Prog = "Assets/Maimai/Runtime/MaiScoreboard.asset";

    [MenuItem("Maimai/Setup TV Scoreboard")]
    public static void Setup()
    {
        Renderer tv = null;
        foreach (var r in Object.FindObjectsOfType<MeshRenderer>(true)) if (r.name == "TV") { tv = r; break; }
        if (tv == null) { Debug.LogError("No 'TV' mesh in the scene"); return; }
        var p1 = GameObject.Find("MaiCabinet/StartButton").GetComponent<MaiGame>();
        var p2go = GameObject.Find("MaiCabinetP2/StartButton");

        // screen face: the bounds' thinnest horizontal axis is the normal; it points toward the cabinets
        Bounds b = tv.bounds;
        bool thinX = b.size.x < b.size.z;
        Vector3 n = thinX ? Vector3.right : Vector3.forward;
        if (Vector3.Dot(p1.screen.position - b.center, n) < 0) n = -n;
        float W = (thinX ? b.size.z : b.size.x) * 0.9f, H = b.size.y * 0.86f;
        Vector3 face = b.center + n * ((thinX ? b.size.x : b.size.z) * 0.5f + 0.006f);

        var old = GameObject.Find("TVScoreboard");
        if (old != null) Undo.DestroyObjectImmediate(old);
        var root = new GameObject("TVScoreboard");
        Undo.RegisterCreatedObjectUndo(root, "TV scoreboard");
        root.transform.SetPositionAndRotation(face, Quaternion.LookRotation(-n, Vector3.up)); // text reads from the room side

        var card = GameObject.CreatePrimitive(PrimitiveType.Quad);
        card.name = "Card";
        Object.DestroyImmediate(card.GetComponent<Collider>());
        card.transform.SetParent(root.transform, false);
        card.transform.localPosition = new Vector3(0, 0, 0.003f);
        card.transform.localScale = new Vector3(W, H, 1);
        card.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Maimai/Generated/Panel.mat");

        var mono = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Fonts + "/SpaceMono SDF.asset");
        var dots = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Fonts + "/Doto SDF.asset");
        // TMP 3D: glyph height ~ fontSize / 9 units
        TextMeshPro T(string name, float x, float y, float w, float h, TMP_FontAsset f, float glyph, TextAlignmentOptions a)
        {
            var t = MaiSetup.Text(root.transform, name, new Vector3(x, y, 0), new Vector2(w, h));
            t.font = f;
            t.enableAutoSizing = false;
            t.fontSize = glyph * 9f;
            t.alignment = a;
            t.enableWordWrapping = true;
            t.overflowMode = TextOverflowModes.Overflow;
            t.richText = true;
            t.color = Color.white;
            t.text = "";
            return t;
        }

        if (AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(Prog) == null)
        {
            var pa = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
            pa.sourceCsScript = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/Maimai/Runtime/MaiScoreboard.cs");
            AssetDatabase.CreateAsset(pa, Prog);
            AssetDatabase.SaveAssets();
            UdonSharpProgramAsset.CompileAllCsPrograms(true);
        }
        var board = UdonSharpUndo.AddComponent<MaiScoreboard>(root);
        board.header = T("Header", 0, H * 0.4f, W * 0.92f, H * 0.1f, mono, H * 0.045f, TextAlignmentOptions.Left);
        board.singleBig = T("SingleBig", -W * 0.23f, 0, W * 0.46f, H * 0.4f, dots, H * 0.16f, TextAlignmentOptions.Center);
        board.single = T("Single", W * 0.22f, -H * 0.04f, W * 0.5f, H * 0.7f, mono, H * 0.05f, TextAlignmentOptions.Left);
        board.leftBig = T("LeftBig", -W * 0.24f, H * 0.2f, W * 0.46f, H * 0.2f, dots, H * 0.1f, TextAlignmentOptions.Center);
        board.left = T("Left", -W * 0.24f, -H * 0.15f, W * 0.44f, H * 0.5f, mono, H * 0.04f, TextAlignmentOptions.Top);
        board.rightBig = T("RightBig", W * 0.24f, H * 0.2f, W * 0.46f, H * 0.2f, dots, H * 0.1f, TextAlignmentOptions.Center);
        board.right = T("Right", W * 0.24f, -H * 0.15f, W * 0.44f, H * 0.5f, mono, H * 0.04f, TextAlignmentOptions.Top);
        UdonSharpEditorUtility.CopyProxyToUdon(board);

        foreach (var g in new[] { p1, p2go != null ? p2go.GetComponent<MaiGame>() : null })
        {
            if (g == null) continue;
            g.board = board;
            UdonSharpEditorUtility.CopyProxyToUdon(g);
            EditorUtility.SetDirty(g);
        }
        Debug.Log($"MAITV board on TV face {face}, normal {n}, {W:F2} x {H:F2} m, P2 {(p2go != null)}");
    }

    // batch: setup + save, then fill with fake results (not saved) and render the TV from the cabinets
    public static void Batch()
    {
        UdonSharp.Compiler.UdonSharpCompilerV1.CompileSync();
        EditorSceneManager.OpenScene(MaiBatchWorld.World);
        Setup();
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        var board = Object.FindObjectOfType<MaiScoreboard>();
        string outDir = System.Environment.GetEnvironmentVariable("MAI_PREVIEW_DIR") ?? "Temp";
        string body = "<b>Transcend My Love</b>\n<size=75%><color=#B865F0>●</color> MASTER 13+</size>\n<size=150%><color=#FFC94D><b>SSS</b></color></size>  <color=#6BE37A>FC+</color>\n" +
                      "<size=75%><color=#8A8A8A>DX </color>2790<color=#8A8A8A>/2856   MAX COMBO </color>952\n<color=#FFE36B>CRIT</color> 880  <color=#FFA640>PERF</color> 60  <color=#FF7AD0>GREAT</color> 12  <color=#6BE37A>GOOD</color> 0  <color=#8A8A8A>MISS</color> 0\n<color=#6FB8FF>FAST</color> 31   <color=#FF5A36>LATE</color> 41</size>";
        var tvR = board.transform;
        var eye = new GameObject("eye").AddComponent<Camera>();
        eye.transform.position = tvR.position - tvR.forward * 2.2f;
        eye.transform.LookAt(tvR.position);
        eye.fieldOfView = 55;
        var rt = new RenderTexture(1280, 720, 24);
        foreach (var mode in new[] { "single", "vs" })
        {
            board.Report(0, "SynthStitch", body, 100.2345f, false);
            if (mode == "vs") board.Report(1, "Friend", body.Replace("SSS<", "SS+<"), 99.6120f, true);
            eye.targetTexture = rt;
            eye.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            System.IO.File.WriteAllBytes(outDir + "/tv_" + mode + ".png", tex.EncodeToPNG());
        }
        Debug.Log("MAITV rendered");
        EditorApplication.Exit(0);
    }
}
