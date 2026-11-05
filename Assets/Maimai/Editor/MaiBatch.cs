// Batch-mode entry points so the project can be compiled/set up without opening the editor UI:
//   Unity.exe -batchmode -projectPath <proj> -executeMethod MaiBatch.Compile -logFile <log>
//   Unity.exe -batchmode -projectPath <proj> -executeMethod MaiBatch.Stage1  -logFile <log>
using UdonSharp.Compiler;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MaiBatch
{
    const string Scene = MaiBatchWorld.World;

    public static void Compile()
    {
        UdonSharpCompilerV1.CompileSync();
        bool err = UdonSharp.UdonSharpProgramAsset.AnyUdonSharpScriptHasError();
        Debug.Log("MAIBATCH U# compile: " + (err ? "ERRORS" : "OK"));
        EditorApplication.Exit(err ? 1 : 0);
    }

    public static void Stage1()
    {
        UdonSharpCompilerV1.CompileSync();
        if (UdonSharp.UdonSharpProgramAsset.AnyUdonSharpScriptHasError()) { Debug.LogError("MAIBATCH U# errors, setup skipped"); EditorApplication.Exit(1); return; }
        EditorSceneManager.OpenScene(Scene);
        MaiScreenSetup.AddCjkFont();
        MaiSetup.RebuildSongList();
        MaiScreenSetup.Setup();
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("MAIBATCH stage1 done");
        EditorApplication.Exit(0);
    }
}

public static class MaiBatchDump
{
    public static void Dump()
    {
        EditorSceneManager.OpenScene(MaiBatchWorld.World);
        var game = GameObject.Find("MaiCabinet/StartButton").GetComponent<MaiGame>();
        var screen = game.screen;
        var disp = GameObject.Find("Display P1");
        var mesh = disp.GetComponent<MeshFilter>().sharedMesh;
        var v = mesh.vertices; var uv = mesh.uv;
        Debug.Log($"DUMP Display P1 verts {v.Length} uvChannels uv:{mesh.uv.Length} uv2:{mesh.uv2.Length} subMeshes {mesh.subMeshCount} screenScale {screen.lossyScale.x}");
        for (int i = 0; i < v.Length; i += Mathf.Max(1, v.Length / 40))
        {
            var l = screen.InverseTransformPoint(disp.transform.TransformPoint(v[i]));
            Debug.Log($"DUMP v{i} local ({l.x:F3},{l.y:F3},{l.z:F3}) uv ({uv[i].x:F3},{uv[i].y:F3})");
        }
        foreach (var t in Object.FindObjectsOfType<Transform>(true))
            if (t.name.StartsWith("Button Light") || t.name == "ButtonLight")
            {
                var l = screen.InverseTransformPoint(t.position);
                var mf = t.GetComponent<MeshFilter>();
                string c = mf != null && mf.sharedMesh != null ? screen.InverseTransformPoint(t.TransformPoint(mf.sharedMesh.bounds.center)).ToString("F3") : "-";
                Debug.Log($"DUMP {t.name} parent {t.parent?.name} active {t.gameObject.activeInHierarchy} renderer {t.GetComponent<Renderer>() != null} mf {mf != null} pivot {l:F3} meshCentre {c}");
            }
        EditorApplication.Exit(0);
    }
}

public static class MaiBatchPreview
{
    // Renders the screen RT and a player-eye view of the cabinet to PNGs (scene is NOT saved).
    public static void Preview()
    {
        string outDir = System.Environment.GetEnvironmentVariable("MAI_PREVIEW_DIR") ?? "Temp";
        EditorSceneManager.OpenScene(MaiBatchWorld.World);
        var game = GameObject.Find("MaiCabinet/StartButton").GetComponent<MaiGame>();
        foreach (var fx in game.sensorFx) fx.SetActive(fx.name == "E3" || fx.name == "B6");
        // real frame: load a chart and run MaiGame.Draw at time MAI_PREVIEW_T
        var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance;
        string want = System.Environment.GetEnvironmentVariable("MAI_TEST_TITLE") ?? "老撸";
        float pt = float.Parse(System.Environment.GetEnvironmentVariable("MAI_PREVIEW_T") ?? "30", System.Globalization.CultureInfo.InvariantCulture);
        int song = System.Array.FindIndex(game.songTitles, s2 => s2.Contains(want)), lvl = 6;
        while (lvl > 0 && game.charts[song * 7 + lvl] == null) lvl--;
        typeof(MaiGame).GetMethod("LoadChart", bf).Invoke(game, new object[] { game.charts[song * 7 + lvl] });
        var rmeth = typeof(MaiGame).GetMethod("Renderers", bf); // Start() doesn't run in batch
        typeof(MaiGame).GetField("poolR", bf).SetValue(game, rmeth.Invoke(game, new object[] { game.pool }));
        typeof(MaiGame).GetField("barR", bf).SetValue(game, rmeth.Invoke(game, new object[] { game.bars }));
        typeof(MaiGame).GetField("touchR", bf).SetValue(game, rmeth.Invoke(game, new object[] { game.touchPool }));
        typeof(MaiGame).GetField("touchMpb", bf).SetValue(game, new MaterialPropertyBlock());
        typeof(MaiGame).GetField("noteMpb", bf).SetValue(game, new MaterialPropertyBlock());
        typeof(MaiGame).GetMethod("Draw", bf).Invoke(game, new object[] { pt, true });
        typeof(MaiGame).GetMethod("InitSlots", bf).Invoke(game, null);
        typeof(MaiGame).GetMethod("ResetSlides", bf).Invoke(game, null);
        typeof(MaiGame).GetMethod("DrawSlides", bf).Invoke(game, new object[] { pt, true });
        Debug.Log($"MAIBATCH slides {typeof(MaiGame).GetField("sCount", bf).GetValue(game)}");
        Debug.Log($"MAIBATCH draw t={pt}: notes {typeof(MaiGame).GetField("lastNotes", bf).GetValue(game)} bars {typeof(MaiGame).GetField("lastBars", bf).GetValue(game)} touch {typeof(MaiGame).GetField("lastTouch", bf).GetValue(game)} count {typeof(MaiGame).GetField("count", bf).GetValue(game)} shaderOK {game.noteMats[0].shader.isSupported} {game.noteMats[0].shader.name} pool0 active {game.pool[0].gameObject.activeSelf} pos {game.pool[0].localPosition} scale {game.pool[0].localScale}");
        // real HUD text from fake counts (MAI_PREVIEW_MENU=1 renders the song-select state)
        string mv = System.Environment.GetEnvironmentVariable("MAI_PREVIEW_MENU");
        bool menu = mv == "1" || mv == "2";
        typeof(MaiGame).GetField("menuPage", bf).SetValue(game, mv == "2" ? 1 : mv == "3" ? 2 : 0);
        menu |= mv == "3"; // 3 = results screen
        typeof(MaiGame).GetMethod("ApplySpeed", bf).Invoke(game, null);
        typeof(MaiGame).GetField("song", bf).SetValue(game, song);
        typeof(MaiGame).GetField("level", bf).SetValue(game, lvl);
        typeof(MaiGame).GetField("playing", bf).SetValue(game, !menu);
        foreach (var kv in new[] { ("crit", 120), ("perf", 30), ("great", 4), ("good", 1), ("miss", 0), ("combo", 123) })
            typeof(MaiGame).GetField(kv.Item1, bf).SetValue(game, kv.Item2);
        var hud = game.hud; game.hud = null; // ShowJudge -> UpdateHud needs VRC networking; skip it
        typeof(MaiGame).GetMethod("ShowJudge", bf).Invoke(game, new object[] { 2, 0.072f, "", true });
        game.hud = hud;
        typeof(MaiGame).GetMethod("BuildHud", bf).Invoke(game, new object[] { true });
        if (game.buttonLights.Length > 2) game.buttonLights[2].SetActive(true);

        var cam = GameObject.Find("MaiCabinet/ScreenField/ScreenCamera").GetComponent<Camera>();
        Save(cam, cam.targetTexture, outDir + "/preview_screen.png");

        var eye = new GameObject("eye").AddComponent<Camera>();
        var scr = game.screen;
        eye.transform.position = scr.position - Vector3.ProjectOnPlane(scr.forward, Vector3.up).normalized * 0.75f + Vector3.up * 0.25f;
        eye.transform.LookAt(scr.position + Vector3.up * 0.12f);
        eye.fieldOfView = 70;
        cam.Render(); // refresh screen RT first
        var rt = new RenderTexture(1280, 960, 24);
        Save(eye, rt, outDir + "/preview_cabinet.png");

        // both sides: P2 in its JOINED menu state, camera pulled back between the screens
        var p2go = GameObject.Find("MaiCabinetP2/StartButton");
        if (p2go != null)
        {
            var p2 = p2go.GetComponent<MaiGame>();
            foreach (var f in new[] { ("song", (object)song), ("level", (object)3), ("playing", (object)false), ("joined", (object)true), ("menuPage", (object)0) })
                typeof(MaiGame).GetField(f.Item1, bf).SetValue(p2, f.Item2);
            typeof(MaiGame).GetMethod("BuildHud", bf).Invoke(p2, new object[] { true });
            var cam2 = GameObject.Find("MaiCabinetP2/ScreenField/ScreenCamera").GetComponent<Camera>();
            cam2.Render();
            Save(cam2, cam2.targetTexture, outDir + "/preview_screen_p2.png");
            Vector3 mid = (scr.position + p2.screen.position) * 0.5f;
            eye.transform.position = mid - Vector3.ProjectOnPlane(scr.forward, Vector3.up).normalized * 1.5f + Vector3.up * 0.2f;
            eye.transform.LookAt(mid + Vector3.up * 0.12f);
            cam.Render();
            Save(eye, rt, outDir + "/preview_both.png");
        }
        EditorApplication.Exit(0);
    }

    static void Save(Camera cam, RenderTexture rt, string path)
    {
        var prev = cam.targetTexture;
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        RenderTexture.active = null;
        cam.targetTexture = prev;
        Debug.Log("MAIBATCH wrote " + path);
    }
}

// Drives MaiGame's real judging code (U# proxies are plain C# in the editor) through a chart, frame by frame.
//   MAI_TEST_TITLE = substring of a song title (default: the touch-heavy chart)
public static class MaiBatchTest
{
    static readonly System.Reflection.BindingFlags BF = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance;
    static object Get(object o, string f) => o.GetType().GetField(f, BF).GetValue(o);
    static void Set(object o, string f, object v) => o.GetType().GetField(f, BF).SetValue(o, v);
    static object Call(object o, string m, params object[] a) => o.GetType().GetMethod(m, BF).Invoke(o, a);

    public static void AutoPlay()
    {
        EditorSceneManager.OpenScene(MaiBatchWorld.World);
        var game = GameObject.Find("MaiCabinet/StartButton").GetComponent<MaiGame>();
        string want = System.Environment.GetEnvironmentVariable("MAI_TEST_TITLE") ?? "老撸";
        int song = System.Array.FindIndex(game.songTitles, s => s.Contains(want));
        int lvl = -1;
        for (int l = 6; l >= 0 && lvl < 0; l--) if (game.charts[song * 7 + l] != null) lvl = l;
        game.hud = null; // UpdateHud returns early (no VRC networking in batch)
        bool ok = true;
        ok &= Run(game, song, lvl, 0f, true, "perfect", new[] { 1, 0, 0, 0, 0 });
        ok &= Run(game, song, lvl, 0.040f, true, "40ms late (lane P, touch CP)", null);
        ok &= Run(game, song, lvl, 0f, false, "holds released halfway", null);
        Debug.Log("MAITEST " + (ok ? "PASS" : "FAIL"));
        EditorApplication.Exit(ok ? 0 : 1);
    }

    // expect: share of grades that must be 100% (index 0 CP .. 4 MISS), or null = just report
    static bool Run(MaiGame game, int song, int lvl, float lateBy, bool holdFull, string name, int[] expect)
    {
        Call(game, "LoadChart", game.charts[song * 7 + lvl]);
        Call(game, "ResetSlides");
        Set(game, "playing", true);
        Set(game, "lastHoldT", -1f);
        foreach (var f in new[] { "head", "drawHead", "actCount", "crit", "perf", "great", "good", "miss", "combo", "maxCombo", "lost", "lostB", "dxLost" }) Set(game, f, 0);
        int count = (int)Get(game, "count");
        var ms = (int[])Get(game, "nMs"); var lane = (int[])Get(game, "nLane"); var type = (int[])Get(game, "nType");
        var dur = (int[])Get(game, "nDur"); var sens = (int[])Get(game, "nSens"); var mask = (long[])Get(game, "nMask");
        var state = (int[])Get(game, "nState"); var act = (int[])Get(game, "act");
        int next = 0, holds = 0;
        float end = (float)Get(game, "endSec");
        int sc = (int)Get(game, "sCount");
        var sStart = (int[])Get(game, "sStart"); var sDur = (int[])Get(game, "sDur"); var sPathN = (int[])Get(game, "sPathN");
        var pFirst = (int[])Get(game, "pFirst"); var pLen = (int[])Get(game, "pLen"); var ptX = (float[])Get(game, "ptX"); var ptY = (float[])Get(game, "ptY");
        for (float t = 0; t < end; t += 1f / 60f)
        {
            // clicks for notes whose time (+lateBy) falls in this frame
            while (next < count && ms[next] / 1000f + lateBy <= t + 0.5f / 60f)
            {
                int i = next++;
                if (state[i] != 0) continue;
                if (type[i] == 4) Call(game, "TouchClick", (lane[i] / 10 == 2 ? 16 : sens[i]), t);
                else Call(game, "LaneClick", lane[i], t);
                if (dur[i] > 0) holds++;
            }
            // hold the lanes/sensors of active holds (or let go halfway)
            int btn = 0; long sm = 0;
            int ac = (int)Get(game, "actCount");
            for (int a = 0; a < ac; a++)
            {
                int i = act[a];
                if (!holdFull && t > (ms[i] + dur[i] * 0.5f) / 1000f) continue;
                if (type[i] == 4) sm |= mask[i]; else btn |= 1 << (lane[i] - 1);
            }
            // slides: a ball follows each star (held 0.15 s at the end), read through the baked sensor grid
            for (int s = 0; s < sc; s++)
            {
                float st = sStart[s] / 1000f + lateBy, du = sDur[s] / 1000f;
                if (t < st || t > st + du + 0.15f) continue;
                float u = Mathf.Clamp01((t - st) / du);
                for (int k = 0; k < sPathN[s]; k++)
                {
                    int n = pLen[s * 3 + k], f0 = pFirst[s * 3 + k];
                    float x = u * (n - 1); int i0 = Mathf.Min(n - 2, (int)x); float fr = x - i0;
                    float px = Mathf.Lerp(ptX[f0 + i0], ptX[f0 + i0 + 1], fr) * game.radius, py = Mathf.Lerp(ptY[f0 + i0], ptY[f0 + i0 + 1], fr) * game.radius;
                    sm |= (long)Call(game, "SensorAt", px, py);
                }
            }
            Set(game, "buttonMask", btn); Set(game, "sensorMask", sm);
            Call(game, "MissLateNotes", t);
            Call(game, "UpdateHolds", t);
            int goodBefore = (int)Get(game, "good") + (int)Get(game, "miss");
            var sState = (int[])Get(game, "sState");
            var snap = (int[])sState.Clone();
            Call(game, "UpdateSlides", t);
            if ((int)Get(game, "good") + (int)Get(game, "miss") > goodBefore)
                for (int s = 0; s < sc; s++)
                    if (snap[s] != sState[s])
                        Debug.Log($"MAITEST slide GD/MISS: head {((int[])Get(game, "sHead"))[s]} start {sStart[s]} dur {sDur[s]} judge {((int[])Get(game, "sJudge"))[s]} stay {((int[])Get(game, "sStay"))[s]} at t={t * 1000:F0} left {Get(game, "lastJudge")}");
        }
        int[] g = { (int)Get(game, "crit"), (int)Get(game, "perf"), (int)Get(game, "great"), (int)Get(game, "good"), (int)Get(game, "miss") };
        int total = g[0] + g[1] + g[2] + g[3] + g[4];
        Debug.Log($"MAITEST [{name}] {game.songTitles[song]} lv{lvl + 1}: notes {count} slides {sc} judged {total} holds {holds} -> CP {g[0]} P {g[1]} GR {g[2]} GD {g[3]} MISS {g[4]} combo {Get(game, "combo")}");
        float pct = (float)Call(game, "Achievement", Get(game, "lost"), Get(game, "lostB"));
        int dxMax = (int)Get(game, "dxMax"), dx = dxMax - (int)Get(game, "dxLost");
        Debug.Log($"MAITEST [{name}] score {pct:F4}% {Call(game, "Rank", pct)} DX {dx}/{dxMax} maxCombo {Get(game, "maxCombo")} breakBonus {Get(game, "maxBonus")}");
        count += sc;
        if (expect != null && expect[0] == 1 && (Mathf.Abs(pct - 101f) > 0.0001f || dx != dxMax || (int)Get(game, "maxCombo") != count))
        { Debug.LogError($"MAITEST [{name}] perfect run should be 101% / full DX / full combo"); return false; }
        if (total != count) { Debug.LogError($"MAITEST [{name}] judged {total} != notes+slides {count}"); return false; }
        if (!holdFull && holds > 0 && g[0] == total) { Debug.LogError($"MAITEST [{name}] released holds were not downgraded"); return false; }
        if (expect == null) return true;
        for (int k = 0; k < 5; k++)
            if (expect[k] == 1 && g[k] != total) { Debug.LogError($"MAITEST [{name}] expected all grade {k}"); return false; }
        return true;
    }
}

public static class MaiBatchP2
{
    public static void Run()
    {
        UdonSharpCompilerV1.CompileSync();
        if (UdonSharp.UdonSharpProgramAsset.AnyUdonSharpScriptHasError()) { Debug.LogError("MAIBATCH U# errors, setup skipped"); EditorApplication.Exit(1); return; }
        EditorSceneManager.OpenScene(MaiBatchWorld.World);
        MaiP2Setup.Setup();
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("MAIBATCH p2 done");
        EditorApplication.Exit(0);
    }
}

public static class MaiBatchUi
{
    // UI refresh: re-run screen setup (centre text, ring labels) + notes setup (judge ring).
    public static void Run()
    {
        UdonSharpCompilerV1.CompileSync();
        if (UdonSharp.UdonSharpProgramAsset.AnyUdonSharpScriptHasError()) { Debug.LogError("MAIBATCH U# errors, setup skipped"); EditorApplication.Exit(1); return; }
        EditorSceneManager.OpenScene(MaiBatchWorld.World);
        MaiUiSetup.SetupAll();
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("MAIBATCH ui done");
        EditorApplication.Exit(0);
    }
}

public static class MaiBatchStage3
{
    public static void Run()
    {
        UdonSharpCompilerV1.CompileSync();
        if (UdonSharp.UdonSharpProgramAsset.AnyUdonSharpScriptHasError()) { Debug.LogError("MAIBATCH U# errors, setup skipped"); EditorApplication.Exit(1); return; }
        EditorSceneManager.OpenScene(MaiBatchWorld.World);
        MaiSetup.RebuildSongList();   // re-convert charts with slide bodies
        MaiNotesSetup.Setup();
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("MAIBATCH stage3 done");
        EditorApplication.Exit(0);
    }
}

public static class MaiBatchStage2
{
    public static void Run()
    {
        UdonSharpCompilerV1.CompileSync();
        if (UdonSharp.UdonSharpProgramAsset.AnyUdonSharpScriptHasError()) { Debug.LogError("MAIBATCH U# errors, setup skipped"); EditorApplication.Exit(1); return; }
        EditorSceneManager.OpenScene(MaiBatchWorld.World);
        MaiSetup.RebuildSongList();   // re-convert charts with the new flags column
        MaiNotesSetup.Setup();
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("MAIBATCH stage2 done");
        EditorApplication.Exit(0);
    }
}
