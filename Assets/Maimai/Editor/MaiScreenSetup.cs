// Stage 1 setup (run after Align / Add Hand Balls / Use Cabinet Button Hitboxes):
//  - Screen: the playfield lives far below the world (y=-500, under respawn height) and an ortho camera renders it
//    into a RenderTexture shown on the model's Display P1 material, using that mesh's own UVs (round screen + top
//    sub-monitor are one mesh, like the real 1080x1920 game output).
//  - Sensors: bakes the 34 "Touch Hitbox" meshes (A1-8,B1-8,C1-2,D1-8,E1-8) into a lookup grid + glow shapes.
//  - Button lights: the model's "Button Light 1-8" light up while pressed.
using System.Collections.Generic;
using System.IO;
using TMPro;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

public static class MaiScreenSetup
{
    const string Gen = "Assets/Maimai/Generated";
    public const float BallRadius = 0.035f; // matches the 0.07 m hand balls

    public static readonly string[] SensorNames =
    {
        "A1","A2","A3","A4","A5","A6","A7","A8", "B1","B2","B3","B4","B5","B6","B7","B8", "C1","C2",
        "D1","D2","D3","D4","D5","D6","D7","D8", "E1","E2","E3","E4","E5","E6","E7","E8",
    };

    [MenuItem("Maimai/Setup Screen + Sensors (Stage 1)")]
    public static void Setup()
    {
        var game = MaiSide.Game();
        var disp = GameObject.Find(MaiSide.Display);
        Vector3 FieldOrigin = MaiSide.Field;
        if (game == null || disp == null) { Debug.LogError("Need MaiCabinet (Build + Align first) and Display P1"); return; }
        Directory.CreateDirectory(Gen);
        Transform screen = game.screen, root = game.transform.parent;

        // ---------- 1. Display P1 layout ----------
        // Display P1 is a planar quad textured like the real 1080x1920 portrait output: round screen = bottom
        // square (masked to a circle by the bezel), sub-monitor = the rectangle above. Map it linearly in
        // screen-local units (same units as the playfield: radius 0.35 = judge ring).
        var mesh = disp.GetComponent<MeshFilter>().sharedMesh;
        var tr = disp.transform;
        Vector3[] verts = mesh.vertices;
        Vector2[] uvs = mesh.uv;
        float S = screen.lossyScale.x;                // metres per screen-local unit
        float roundR = game.radius / 0.92f;           // round display radius, screen-local
        var lMin = new Vector2(float.MaxValue, float.MaxValue); var lMax = -lMin;
        var aMin = lMin; var aMax = -lMin;
        var loc = new Vector2[verts.Length];
        for (int i = 0; i < verts.Length; i++)
        {
            Vector3 l = screen.InverseTransformPoint(tr.TransformPoint(verts[i]));
            loc[i] = new Vector2(l.x, l.y);
            lMin = Vector2.Min(lMin, loc[i]); lMax = Vector2.Max(lMax, loc[i]);
            aMin = Vector2.Min(aMin, uvs[i]); aMax = Vector2.Max(aMax, uvs[i]);
        }
        Vector2 lMid = (lMin + lMax) * 0.5f;
        float sx = 0, sy = 0; // does u grow to the right / v grow upward?
        for (int i = 0; i < verts.Length; i++) { sx += (loc[i].x - lMid.x) * uvs[i].x; sy += (loc[i].y - lMid.y) * uvs[i].y; }

        // material tiling so the RT exactly covers this mesh's UV box, oriented right/up
        var mat = disp.GetComponent<Renderer>().sharedMaterial;
        Vector2 du = aMax - aMin;
        var scale = new Vector2(1f / du.x, 1f / du.y);
        var offset = new Vector2(-aMin.x / du.x, -aMin.y / du.y);
        if (sx < 0) { scale.x = -scale.x; offset.x = aMax.x / du.x; }
        if (sy < 0) { scale.y = -scale.y; offset.y = aMax.y / du.y; }

        float worldW = (lMax.x - lMin.x) * S, worldH = (lMax.y - lMin.y) * S;
        int H = 1536, W = Mathf.Clamp(Mathf.RoundToInt(H * worldW / worldH), 256, 2048);
        // screen-local point -> field world point (camera is centred on the quad's middle)
        System.Func<Vector2, Vector3> toWorld = p => FieldOrigin + new Vector3((p.x - lMid.x) * S, (p.y - lMid.y) * S, 0);

        bool hasTop = lMax.y > roundR * 1.1f;
        // ponytail: the bezel window only shows the upper part of the sub-monitor area; calibrated by eye from a
        // headless render (MaiBatchPreview). Tweak SubMonitorVisible if the text clips.
        const float SubMonitorVisible = 0.44f;
        float visH = (lMax.y - roundR) * SubMonitorVisible;
        Vector2 topMid = new Vector2(lMid.x, lMax.y - visH * 0.5f);
        Vector2 topSize = new Vector2(lMax.x - lMin.x, visH);

        string rtPath = Gen + "/MaiScreen" + MaiSide.Suffix + ".renderTexture";
        var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(rtPath);
        if (rt == null || rt.width != W || rt.height != H)
        {
            if (rt != null) AssetDatabase.DeleteAsset(rtPath);
            rt = new RenderTexture(W, H, 16) { name = "MaiScreen" + MaiSide.Suffix, antiAliasing = 4 };
            AssetDatabase.CreateAsset(rt, rtPath);
        }
        Undo.RecordObject(mat, "Screen material");
        mat.mainTexture = rt;
        mat.mainTextureScale = scale;
        mat.mainTextureOffset = offset;

        // ---------- 2. field + camera ----------
        var field = root.Find("ScreenField");
        if (field == null)
        {
            field = new GameObject("ScreenField").transform;
            field.SetParent(root, false);
            Undo.RegisterCreatedObjectUndo(field.gameObject, "Field");
        }
        field.position = FieldOrigin;
        field.rotation = Quaternion.identity;

        var camT = field.Find("ScreenCamera");
        if (camT == null)
        {
            camT = new GameObject("ScreenCamera").transform;
            camT.SetParent(field, false);
            camT.gameObject.AddComponent<Camera>();
        }
        camT.position = FieldOrigin + new Vector3(0, 0, -1);
        camT.rotation = Quaternion.identity;
        var cam = camT.GetComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = worldH * 0.5f;
        cam.aspect = worldW / worldH;
        cam.nearClipPlane = 0.5f;
        cam.farClipPlane = 1.5f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.targetTexture = rt;

        // round playfield: same local units as the old Screen (radius 0.35 = judge ring), so note maths is unchanged
        var play = field.Find("Playfield");
        if (play == null)
        {
            play = new GameObject("Playfield").transform;
            play.SetParent(field, false);
            Undo.RegisterCreatedObjectUndo(play.gameObject, "Playfield");
        }
        play.position = toWorld(Vector2.zero); // round-screen centre is screen-local (0,0)
        play.rotation = Quaternion.identity;
        play.localScale = Vector3.one * screen.lossyScale.x;
        // move everything drawn on the screen out of the cabinet and into the field
        var move = new List<Transform>();
        foreach (Transform ch in screen) if (ch.name.StartsWith("Note") || ch.name.StartsWith("LaneMark") || ch.name == "CenterText" || ch.name == "MenuLabels") move.Add(ch);
        foreach (var ch in move) Undo.SetTransformParent(ch, play, "Move to field");
        foreach (var ch in move) { ch.localRotation = Quaternion.identity; }

        // centre text on the main screen, under the notes (notes sit at local z -0.01)
        var center = MaiSetup.Text(play, "CenterText", new Vector3(0, 0, -0.004f), new Vector2(game.radius * 1.3f, game.radius * 1.0f));
        center.enableAutoSizing = false; // fixed size: combo must not jump when the judgement line comes and goes
        center.fontSize = 0.55f;
        center.lineSpacing = -20f;
        center.color = new Color(1, 1, 1, 0.75f); // dim so notes stay readable

        // button hints just inside the ring (menu only)
        var labels = play.Find("MenuLabels");
        if (labels == null) { labels = new GameObject("MenuLabels").transform; labels.SetParent(play, false); Undo.RegisterCreatedObjectUndo(labels.gameObject, "Labels"); }
        string[] hint = { "LV +", "NEXT", "NEXT", "OK", "OK", "PREV", "PREV", "LV -" }; // MaiGame rewrites these per menu page
        var hints = new TextMeshPro[8];
        for (int i = 0; i < 8; i++)
        {
            float a = (22.5f + 45f * i) * Mathf.Deg2Rad;
            var t = MaiSetup.Text(labels, "Label" + (i + 1), new Vector3(Mathf.Sin(a), Mathf.Cos(a), 0) * game.radius * 0.8f + new Vector3(0, 0, -0.004f), new Vector2(0.2f, 0.08f));
            t.text = hint[i];
            t.enableAutoSizing = false;
            t.fontSize = 0.26f;
            t.fontStyle = FontStyles.Bold;
            t.color = Color.white; // colour comes from rich-text tags
            hints[i] = t;
        }
        game.hintTexts = hints;

        game.center = center;
        game.menuLabels = labels.gameObject;

        // top sub-monitor text
        var hudT = root.Find("HUD") ?? field.Find("HUD");
        Undo.SetTransformParent(hudT, field, "HUD to field");
        var hud = hudT.GetComponent<TextMeshPro>();
        if (hasTop)
        {
            hudT.position = toWorld(topMid);
            hud.rectTransform.sizeDelta = topSize * S * 0.9f;
        }
        hudT.rotation = Quaternion.identity;
        hudT.localScale = Vector3.one;

        // screen look: unlit note colours
        var unlit = Shader.Find("Unlit/Color");
        foreach (var lm in play.GetComponentsInChildren<Renderer>(true))
            if (lm.name.StartsWith("LaneMark")) lm.sharedMaterial.shader = unlit;

        // ---------- 3. touch sensors ----------
        if (!MaiSide.IsP2) BakeSensors(game, screen, play); // P2 keeps the grid + glow shapes copied from P1

        // ---------- 4. button lights ----------
        // "Button Light N" are empty anchors (MaiDXR hung real lights on them). Put a glow disc on each,
        // facing the player, toggled while that button is held.
        var lights = new GameObject[8];
        var glow = MatUnlit("ButtonLightOn", new Color(1f, 0.95f, 0.5f));
        foreach (var t in Object.FindObjectsOfType<Transform>(true))
        {
            if (t == null || !t.name.StartsWith("Button Light ")) continue; // old Glows get destroyed mid-loop
            Vector3 l = screen.InverseTransformPoint(t.position);
            float rad = new Vector2(l.x, l.y).magnitude;
            if (rad < game.radius * 0.6f || rad > game.radius * 1.8f || Mathf.Abs(l.z) > game.radius) continue; // P2 side
            float deg = Mathf.Atan2(l.x, l.y) * Mathf.Rad2Deg;
            if (deg < 0) deg += 360;
            var old = t.Find("Glow");
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            g.name = "Glow";
            Object.DestroyImmediate(g.GetComponent<Collider>());
            g.transform.SetParent(t, false);
            g.transform.rotation = screen.rotation * Quaternion.Euler(90, 0, 0); // disc faces along screen normal
            g.transform.localScale = Vector3.one;
            float d = 0.07f; // metres
            var ls = t.lossyScale;
            g.transform.localScale = new Vector3(d / ls.x, 0.004f / ls.y, d / ls.z);
            g.GetComponent<Renderer>().sharedMaterial = glow;
            g.SetActive(false);
            Undo.RegisterCreatedObjectUndo(g, "Glow");
            lights[(int)(deg / 45f)] = g;
        }
        bool allLights = true;
        foreach (var g in lights) allLights &= g != null;
        game.buttonLights = allLights ? lights : new GameObject[0];
        if (!allLights) Debug.LogWarning("Button Light 1-8 anchors not all found near P1; button lights disabled");

        // start cube clear of the button ring (outer ring ~1.5x display radius)
        float rm = roundR * S;
        // outer side of each cabinet (the gap between them holds the online-songs screen)
        game.transform.position = screen.position + screen.right * rm * (MaiSide.IsP2 ? 1.8f : -1.8f) - screen.up * rm * 0.5f - screen.forward * 0.05f;

        UdonSharpEditorUtility.CopyProxyToUdon(game);
        EditorUtility.SetDirty(game);
        AssetDatabase.SaveAssets();
        Debug.Log($"Screen: RT {W}x{H}, quad local x {lMin.x:F3}..{lMax.x:F3} y {lMin.y:F3}..{lMax.y:F3}, top screen {(hasTop ? "yes" : "NO")}, uv flips x:{sx < 0} y:{sy < 0}. Sensors baked: {game.sensorFx.Length}. Lights: {allLights}");
    }

    // Grid: each cell stores the sensors a ball centred there would overlap (point-to-triangle distance <= ball radius).
    static void BakeSensors(MaiGame game, Transform screen, Transform play)
    {
        const int N = 96;
        float half = game.radius * 1.25f;
        float ballR = BallRadius / screen.lossyScale.x;
        var tris = new List<Vector2[]>[SensorNames.Length];
        var fxMat = MatUnlit("SensorGlow", new Color(0.12f, 0.3f, 0.6f));
        var fxRoot = play.Find("SensorFx");
        if (fxRoot != null) Undo.DestroyObjectImmediate(fxRoot.gameObject);
        fxRoot = new GameObject("SensorFx").transform;
        fxRoot.SetParent(play, false);
        Undo.RegisterCreatedObjectUndo(fxRoot.gameObject, "Sensor fx");
        var fx = new GameObject[SensorNames.Length];

        var byName = new Dictionary<string, GameObject>();
        foreach (var t0 in Object.FindObjectsOfType<Transform>(true)) // include inactive: sensor meshes are often hidden
            if (t0.parent != null && t0.parent.name == "Touch Hitbox") byName[t0.name] = t0.gameObject;
        for (int k = 0; k < SensorNames.Length; k++)
        {
            if (!byName.TryGetValue(SensorNames[k], out var go)) { Debug.LogError("Missing sensor Touch Hitbox/" + SensorNames[k]); return; }
            var src = go.GetComponent<MeshFilter>().sharedMesh;
            var sr = go.GetComponent<Renderer>();
            if (sr != null && sr.enabled) { Undo.RecordObject(sr, "Hide sensor"); sr.enabled = false; } // bake-only, not visible
            var v = src.vertices;
            var t = src.triangles;
            var flat = new Vector3[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                Vector3 l = screen.InverseTransformPoint(go.transform.TransformPoint(v[i]));
                flat[i] = new Vector3(l.x, l.y, 0);
            }
            tris[k] = new List<Vector2[]>();
            for (int i = 0; i < t.Length; i += 3)
                tris[k].Add(new[] { (Vector2)flat[t[i]], (Vector2)flat[t[i + 1]], (Vector2)flat[t[i + 2]] });

            // glow shape on the display, behind the notes
            var m = new Mesh { name = "Sensor_" + SensorNames[k], vertices = flat, triangles = t };
            m.RecalculateBounds();
            string mp = Gen + "/Sensor_" + SensorNames[k] + ".asset";
            AssetDatabase.DeleteAsset(mp);
            AssetDatabase.CreateAsset(m, mp);
            var g = new GameObject(SensorNames[k]);
            g.transform.SetParent(fxRoot, false);
            g.transform.localPosition = new Vector3(0, 0, 0.002f);
            g.AddComponent<MeshFilter>().sharedMesh = m;
            g.AddComponent<MeshRenderer>().sharedMaterial = fxMat;
            g.SetActive(false);
            fx[k] = g;
        }

        var grid = new long[N * N];
        int hits = 0;
        for (int iy = 0; iy < N; iy++)
            for (int ix = 0; ix < N; ix++)
            {
                var p = new Vector2(((ix + 0.5f) / N * 2 - 1) * half, ((iy + 0.5f) / N * 2 - 1) * half);
                long mask = 0;
                for (int k = 0; k < tris.Length; k++)
                    foreach (var tri in tris[k])
                        if (TriDist(p, tri[0], tri[1], tri[2]) <= ballR) { mask |= 1L << k; break; }
                grid[iy * N + ix] = mask;
                if (mask != 0) hits++;
            }
        game.sensorGrid = grid;
        game.gridN = N;
        game.gridHalf = half;
        game.sensorFx = fx;
        Debug.Log($"Sensor grid {N}x{N}: {hits} touchable cells");
    }

    // distance from p to a filled triangle (0 inside)
    public static float TriDist(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Cross(b - a, p - a), d2 = Cross(c - b, p - b), d3 = Cross(a - c, p - c);
        bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
        if (!(neg && pos)) return 0;
        return Mathf.Min(SegDist(p, a, b), Mathf.Min(SegDist(p, b, c), SegDist(p, c, a)));
    }
    static float Cross(Vector2 u, Vector2 v) => u.x * v.y - u.y * v.x;
    static float SegDist(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-12f));
        return (p - (a + ab * t)).magnitude;
    }

    static Material MatUnlit(string name, Color col)
    {
        string p = Gen + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null) { m = new Material(Shader.Find("Unlit/Color")); AssetDatabase.CreateAsset(m, p); }
        m.shader = Shader.Find("Unlit/Color");
        m.color = col;
        EditorUtility.SetDirty(m);
        return m;
    }

    // Noto Sans SC (SIL OFL) as a dynamic fallback for TMP's default font: CJK titles stop rendering as boxes.
    [MenuItem("Maimai/Add CJK Font")]
    public static void AddCjkFont()
    {
        const string fontPath = "Assets/Maimai/Fonts/NotoSansSC-Regular.otf", assetPath = "Assets/Maimai/Fonts/NotoSansSC SDF.asset";
        var font = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
        if (font == null) { Debug.LogError("Missing " + fontPath); return; }
        var fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (fa == null)
        {
            fa = TMP_FontAsset.CreateFontAsset(font, 64, 6, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic);
            fa.name = "NotoSansSC SDF";
            AssetDatabase.CreateAsset(fa, assetPath);
            fa.atlasTextures[0].name = "NotoSansSC Atlas";
            AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
            fa.material.name = "NotoSansSC Material";
            AssetDatabase.AddObjectToAsset(fa.material, fa);
        }
        var def = TMP_Settings.defaultFontAsset;
        if (def.fallbackFontAssetTable == null) def.fallbackFontAssetTable = new List<TMP_FontAsset>();
        if (!def.fallbackFontAssetTable.Contains(fa)) def.fallbackFontAssetTable.Add(fa);
        EditorUtility.SetDirty(def);
        AssetDatabase.SaveAssets();
        Debug.Log("CJK fallback added to " + def.name);
    }
}
