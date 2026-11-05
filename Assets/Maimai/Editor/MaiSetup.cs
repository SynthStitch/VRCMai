using System.IO;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;
using TMPro;

public static class MaiSetup
{
    const string Gen = "Assets/Maimai/Generated";

    // Select a maidata.txt in the Project window -> writes chart_<N>.txt next to it for every &inote_N.
    [MenuItem("Maimai/Convert Selected maidata.txt")]
    static void Convert()
    {
        string path = AssetDatabase.GetAssetPath(Selection.activeObject);
        if (!path.EndsWith(".txt")) { Debug.LogError("Select a maidata.txt first"); return; }
        ConvertFile(path);
        AssetDatabase.Refresh();
    }

    public static void ConvertFile(string path)
    {
        string md = File.ReadAllText(path).Replace("\r", "");
        double first = 0;
        string f = SimaiParse.Field(md, "first");
        if (f != null) double.TryParse(f, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out first);
        string dir = Path.GetDirectoryName(path);
        for (int n = 1; n <= 7; n++)
        {
            string fumen = SimaiParse.Field(md, "inote_" + n);
            if (string.IsNullOrWhiteSpace(fumen)) continue;
            var notes = SimaiParse.Parse(fumen, first);
            if (notes.Count == 0) continue;
            File.WriteAllText(Path.Combine(dir, "chart_" + n + ".txt"), SimaiParse.ToText(notes));
        }
    }

    // Every folder under Charts/ with a maidata.txt + track.(mp3|ogg|wav) becomes a song on the cabinet.
    [MenuItem("Maimai/Rebuild Song List")]
    public static void RebuildSongList()
    {
        var game = FindGame();
        if (game == null) return;
        var titles = new System.Collections.Generic.List<string>();
        var info = new System.Collections.Generic.List<string>();
        var clips = new System.Collections.Generic.List<AudioClip>();
        var charts = new System.Collections.Generic.List<TextAsset>();
        var lv = new System.Collections.Generic.List<string>();
        foreach (string dirAbs in Directory.GetDirectories("Assets/Maimai/Charts"))
        {
            string dir = dirAbs.Replace('\\', '/');
            string md = dir + "/maidata.txt";
            if (!File.Exists(md)) continue;
            AudioClip clip = null;
            foreach (string ext in new[] { "mp3", "ogg", "wav" })
                if (clip == null) clip = AssetDatabase.LoadAssetAtPath<AudioClip>(dir + "/track." + ext);
            if (clip == null) { Debug.LogWarning("No track in " + dir); continue; }
            ConvertFile(md);
            AssetDatabase.Refresh();
            string text = File.ReadAllText(md).Replace("\r", "");
            bool any = false;
            for (int n = 1; n <= 7; n++)
            {
                var ta = AssetDatabase.LoadAssetAtPath<TextAsset>(dir + "/chart_" + n + ".txt");
                charts.Add(ta);
                lv.Add(SimaiParse.Field(text, "lv_" + n) ?? "");
                any |= ta != null;
            }
            if (!any) { charts.RemoveRange(charts.Count - 7, 7); lv.RemoveRange(lv.Count - 7, 7); continue; }
            titles.Add(SimaiParse.Field(text, "title") ?? Path.GetFileName(dir));
            info.Add((SimaiParse.Field(text, "artist") ?? "?") + "  /  chart: " + (SimaiParse.Field(text, "des") ?? "?"));
            clips.Add(clip);
        }
        game.songTitles = titles.ToArray();
        game.songInfo = info.ToArray();
        game.songClips = clips.ToArray();
        game.charts = charts.ToArray();
        game.levelNames = lv.ToArray();
        UdonSharpEditorUtility.CopyProxyToUdon(game);
        EditorUtility.SetDirty(game);
        Debug.Log($"Song list: {titles.Count} songs: " + string.Join(", ", titles));
    }

    [MenuItem("Maimai/Build Prototype Cabinet")]
    static void Build()
    {
        Directory.CreateDirectory(Gen);
        EnsureProgramAsset();

        if (Object.FindObjectOfType<VRC.SDKBase.VRC_SceneDescriptor>() == null)
        {
            var world = AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.vrchat.worlds/Samples/UdonExampleScene/Prefabs/VRCWorld.prefab");
            PrefabUtility.InstantiatePrefab(world);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.localScale = new Vector3(2, 1, 2);
        }

        var root = new GameObject("MaiCabinet");
        Undo.RegisterCreatedObjectUndo(root, "Build cabinet");
        root.transform.position = new Vector3(0, 0, 0.55f); // player spawns at origin facing +Z

        // Screen frame: tilted back a little; local -Z faces the player.
        var screen = new GameObject("Screen").transform;
        screen.SetParent(root.transform, false);
        screen.localPosition = new Vector3(0, 1.15f, 0);
        screen.localRotation = Quaternion.Euler(12, 0, 0);

        const float radius = 0.35f;
        var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        disc.name = "Disc";
        Object.DestroyImmediate(disc.GetComponent<Collider>());
        disc.transform.SetParent(screen, false);
        disc.transform.localRotation = Quaternion.Euler(90, 0, 0);
        disc.transform.localScale = new Vector3(radius * 2.4f, 0.005f, radius * 2.4f);
        disc.GetComponent<Renderer>().sharedMaterial = Mat("Disc", new Color(0.08f, 0.08f, 0.12f));

        var laneMat = Mat("Lane", Color.white);
        for (int i = 0; i < 8; i++)
        {
            float a = (22.5f + 45f * i) * Mathf.Deg2Rad;
            var m = GameObject.CreatePrimitive(PrimitiveType.Cube);
            m.name = "LaneMark" + (i + 1);
            Object.DestroyImmediate(m.GetComponent<Collider>());
            m.transform.SetParent(screen, false);
            m.transform.localPosition = new Vector3(Mathf.Sin(a) * radius, Mathf.Cos(a) * radius, -0.004f);
            m.transform.localScale = new Vector3(0.07f, 0.07f, 0.002f);
            m.transform.localRotation = Quaternion.Euler(0, 0, -45f * i - 22.5f);
            m.GetComponent<Renderer>().sharedMaterial = laneMat;
        }

        Material[] typeMats =
        {
            Mat("Tap", new Color(1f, 0.4f, 0.75f)),
            Mat("Break", new Color(1f, 0.55f, 0.1f)),
            Mat("Hold", new Color(1f, 0.4f, 0.75f)),
            Mat("Star", new Color(0.3f, 0.75f, 1f)),
        };

        var pool = new Transform[48];
        for (int i = 0; i < pool.Length; i++)
        {
            var n = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            n.name = "Note" + i;
            Object.DestroyImmediate(n.GetComponent<Collider>());
            n.transform.SetParent(screen, false);
            n.transform.localScale = new Vector3(0.055f, 0.055f, 0.02f);
            n.SetActive(false);
            pool[i] = n.transform;
        }

        var hudGo = new GameObject("HUD");
        hudGo.transform.SetParent(root.transform, false);
        hudGo.transform.localPosition = new Vector3(0, 1.75f, 0.05f);
        var hud = hudGo.AddComponent<TextMeshPro>();
        hud.fontSize = 1.2f;
        hud.alignment = TextAlignmentOptions.Center;
        hud.rectTransform.sizeDelta = new Vector2(1.2f, 0.4f);
        hud.text = "Press START";

        var audio = root.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 0f; // ponytail: 2D so everyone hears it; make 3D once there are multiple cabinets

        var btn = GameObject.CreatePrimitive(PrimitiveType.Cube);
        btn.name = "StartButton";
        btn.transform.SetParent(root.transform, false);
        btn.transform.localPosition = new Vector3(0.55f, 0.95f, -0.1f);
        btn.transform.localScale = new Vector3(0.12f, 0.12f, 0.12f);
        btn.GetComponent<Renderer>().sharedMaterial = Mat("Start", new Color(0.2f, 0.9f, 0.4f));

        var game = UdonSharpUndo.AddComponent<MaiGame>(btn);
        game.music = audio;
        game.screen = screen;
        game.pool = pool;
        // note materials/pools are (re)built by Maimai/Setup Notes (Stage 2)
        game.hud = hud;
        game.radius = radius;
        UdonSharpEditorUtility.CopyProxyToUdon(game);

        Selection.activeGameObject = root;
        RebuildSongList();
        Debug.Log("MaiCabinet built");
    }

    static MaiGame FindGame()
    {
        var game = MaiSide.Game();
        if (game == null) Debug.LogError("Run Maimai/Build Prototype Cabinet first (" + MaiSide.Root + ")");
        return game;
    }

    // Red/blue pickup balls = the player's hands. Avatar agnostic; desktop can grab them too.
    [MenuItem("Maimai/Add Hand Balls")]
    public static void AddBalls()
    {
        var game = FindGame();
        if (game == null) return;
        if (EnsureProgramAsset("MaiBall")) { Debug.LogWarning("MaiBall compiled for the first time - run Maimai/Add Hand Balls again"); return; }
        Transform root = game.transform.parent, screen = game.screen;
        foreach (string old in new[] { "BallRed", "BallBlue" })
        {
            var o = root.Find(old);
            if (o != null) Undo.DestroyObjectImmediate(o.gameObject);
        }

        var balls = new Transform[2];
        for (int i = 0; i < 2; i++)
        {
            var b = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Undo.RegisterCreatedObjectUndo(b, "Add balls");
            b.name = i == 0 ? "BallRed" : "BallBlue";
            b.transform.SetParent(root, false);
            // in front of the screen at centre height, pushed out horizontally (the tilted normal dives into the lower panel)
            float R = game.radius * screen.lossyScale.x;
            Vector3 outward = Vector3.ProjectOnPlane(-screen.forward, Vector3.up).normalized;
            b.transform.position = screen.position + outward * Mathf.Max(0.35f, R * 1.5f) + screen.right * (i == 0 ? -R * 0.6f : R * 0.6f);
            b.transform.localScale = Vector3.one * 0.07f;
            b.GetComponent<SphereCollider>().isTrigger = true; // don't shove the cabinet/players
            b.GetComponent<Renderer>().sharedMaterial = Mat(b.name, i == 0 ? new Color(1f, 0.2f, 0.2f) : new Color(0.2f, 0.4f, 1f));
            var rb = b.AddComponent<Rigidbody>();
            rb.isKinematic = false; // non-kinematic + no gravity + high drag = floats where you let go
            rb.useGravity = false;
            rb.drag = 20f;
            rb.angularDrag = 20f;
            var pick = b.AddComponent<VRC.SDK3.Components.VRCPickup>();
            pick.AutoHold = VRC.SDKBase.VRC_Pickup.AutoHoldMode.Yes; // click once to hold on desktop            b.AddComponent<VRC.SDK3.Components.VRCObjectSync>();
            var mb = UdonSharpUndo.AddComponent<MaiBall>(b);
            mb.game = game;
            UdonSharpEditorUtility.CopyProxyToUdon(mb);
            balls[i] = b.transform;
        }
        game.balls = balls;
        UdonSharpEditorUtility.CopyProxyToUdon(game);
        Debug.Log("Hand balls added");
    }

    // Snap the playfield onto the model's "Display P1" (centre, facing, size) and hide the placeholder disc.
    [MenuItem("Maimai/Align To DX Model (Display P1)")]
    public static void Align()
    {
        var game = FindGame();
        var disp = GameObject.Find(MaiSide.Display);
        if (game == null || disp == null) { Debug.LogError("Need " + MaiSide.Root + " and a '" + MaiSide.Display + "' object in the scene"); return; }
        var mesh = disp.GetComponent<MeshFilter>().sharedMesh;
        var tr = disp.transform;
        var verts = mesh.vertices;
        var norms = mesh.normals;

        Vector3 c = Vector3.zero, n = Vector3.zero;
        foreach (var v in verts) c += tr.TransformPoint(v);
        c /= verts.Length;
        foreach (var v in norms) n += tr.TransformDirection(v);
        n.Normalize();
        // make n point out of the cabinet, toward the player. The P1 shell sits behind and below the screen,
        // so its centre must be on the back side. (Whole-model bounds fail: the ground plate drags them forward.)
        var bodyGo = GameObject.Find(MaiSide.Body);
        Vector3 bodyC = bodyGo != null ? bodyGo.GetComponent<Renderer>().bounds.center : tr.root.position;
        if (Vector3.Dot(n, bodyC - c) > 0) n = -n;

        var rot = Quaternion.LookRotation(-n, Vector3.ProjectOnPlane(Vector3.up, n)); // local -Z faces player
        Vector3 right = rot * Vector3.right, up = rot * Vector3.up;
        // size from full extent (max-min), independent of where the centre is
        float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
        foreach (var v in verts)
        {
            var d = tr.TransformPoint(v) - c;
            float x = Vector3.Dot(d, right), y = Vector3.Dot(d, up);
            minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
            minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
        }
        float dispR = Mathf.Min(maxX - minX, maxY - minY) * 0.5f; // works for a disc or a square quad

        // centre = average of the 8 nearest button hitboxes (the ring is centred on the screen by design),
        // projected onto the display plane. Vertex-average of the display mesh was skewed upward.
        var hits = new System.Collections.Generic.List<Vector3>();
        foreach (var t in Object.FindObjectsOfType<Transform>())
        {
            if (!t.name.StartsWith("Button Hitbox ")) continue;
            var mf = t.GetComponent<MeshFilter>();
            hits.Add(mf != null ? t.TransformPoint(mf.sharedMesh.bounds.center) : t.position);
        }
        Vector3 planePt = c;
        hits.Sort((a, b) => (a - planePt).sqrMagnitude.CompareTo((b - planePt).sqrMagnitude));
        if (hits.Count >= 8)
        {
            Vector3 hc = Vector3.zero;
            for (int i = 0; i < 8; i++) hc += hits[i];
            hc /= 8;
            c = hc - n * Vector3.Dot(hc - planePt, n);
        }
        else Debug.LogWarning($"Only {hits.Count} 'Button Hitbox N' objects found; using display mesh centre");

        // Display P1 is ONE mesh: round screen + the top sub-screen rectangle above it.
        // Round radius = distance from centre down to the lowest vertex (nothing but circle below centre).
        // Top screen = vertices clearly above the circle.
        float lowY = 0f;
        var topPts = new System.Collections.Generic.List<Vector2>();
        foreach (var v in verts)
        {
            var d = tr.TransformPoint(v) - c;
            float x = Vector3.Dot(d, right), y = Vector3.Dot(d, up);
            lowY = Mathf.Min(lowY, y);
            topPts.Add(new Vector2(x, y));
        }
        dispR = -lowY;
        var topMin = new Vector2(float.MaxValue, float.MaxValue);
        var topMax = new Vector2(float.MinValue, float.MinValue);
        Vector3 topC = Vector3.zero, topN = Vector3.zero;
        int topCount = 0;
        for (int i = 0; i < topPts.Count; i++)
        {
            if (topPts[i].y <= dispR * 1.08f) continue;
            topMin = Vector2.Min(topMin, topPts[i]); topMax = Vector2.Max(topMax, topPts[i]);
            topC += tr.TransformPoint(verts[i]);
            if (i < norms.Length) topN += tr.TransformDirection(norms[i]);
            topCount++;
        }
        bool hasTop = topCount > 0 && topMax.y > topMin.y;
        float s = dispR * 0.92f / game.radius; // ponytail: judge ring at 92% of display radius, tweak by eye

        Transform root = game.transform.parent, screen = game.screen;
        Undo.RecordObjects(new Object[] { screen, game.transform }, "Align cabinet");
        screen.SetPositionAndRotation(c + n * 0.003f, rot);
        screen.localScale = Vector3.one * s;
        var disc = screen.Find("Disc");
        if (disc != null) disc.gameObject.SetActive(false);

        // top sub-screen text: fills the rectangle, auto-sized, world scale 1
        var hudT = root.Find("HUD") ?? root.Find("ScreenField/HUD");
        if (hudT.parent != root) { Debug.Log("Screen already set up: re-run Maimai/Setup Screen + Sensors after this"); }
        var hud = hudT.GetComponent<TextMeshPro>();
        Undo.RecordObject(hud, "Align cabinet");
        if (hasTop)
        {
            Vector2 size = topMax - topMin;
            // the top screen may tilt differently from the round one: use its own vertices' centre and normal
            topC /= topCount;
            topN = topN.sqrMagnitude > 1e-6f ? topN.normalized : n;
            if (Vector3.Dot(topN, n) < 0) topN = -topN;
            var topRot = Quaternion.LookRotation(-topN, Vector3.ProjectOnPlane(Vector3.up, topN));
            hudT.SetPositionAndRotation(topC + topN * 0.004f, topRot);
            hudT.localScale = Vector3.one;
            hud.rectTransform.sizeDelta = new Vector2(size.x * 0.92f, size.y * 0.85f);
        }
        else hudT.SetPositionAndRotation(c + up * dispR * 1.55f + n * 0.03f, rot);
        hud.enableAutoSizing = true;
        hud.fontSizeMin = 0.05f;
        hud.fontSizeMax = 1.5f;
        hud.alignment = TextAlignmentOptions.Center;

        // centre text + button labels are created by Maimai/Setup Screen + Sensors (they live on the screen render)
        UdonSharpEditorUtility.CopyProxyToUdon(game);

        game.transform.position = c + right * dispR * 1.8f - up * dispR * 0.5f + n * 0.05f; // clear of the button ring
        Debug.Log($"Aligned to Display P1: centre {c}, round radius {dispR:F3} m (real cab ~0.3 m), playfield scale {s:F2}, top screen {(hasTop ? "found" : "NOT found")}");
    }

    // find-or-create a TextMeshPro child (local space of parent)
    public static TextMeshPro Text(Transform parent, string name, Vector3 localPos, Vector2 size)
    {
        var t = parent.Find(name);
        if (t == null)
        {
            t = new GameObject(name).transform;
            t.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(t.gameObject, "Text");
            t = t.gameObject.AddComponent<TextMeshPro>().transform; // TMP swaps Transform for RectTransform
        }
        t.localPosition = localPos;
        t.localRotation = Quaternion.identity;
        t.localScale = Vector3.one;
        var tmp = t.GetComponent<TextMeshPro>();
        tmp.rectTransform.sizeDelta = size;
        tmp.alignment = TextAlignmentOptions.Center;
        return tmp;
    }

    // Returns true if the asset had to be created (U# needs a compile before the behaviour can be wired).
    static bool EnsureProgramAsset(string name = "MaiGame")
    {
        string asset = "Assets/Maimai/Runtime/" + name + ".asset";
        if (AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(asset) != null) return false;
        var p = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
        p.sourceCsScript = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/Maimai/Runtime/" + name + ".cs");
        AssetDatabase.CreateAsset(p, asset);
        AssetDatabase.SaveAssets();
        UdonSharpProgramAsset.CompileAllCsPrograms(true);
        return true;
    }

    // Wire the model's "Button Hitbox N" objects nearest the P1 screen as the 8 lane buttons.
    // Lane is picked from each box's angle around the screen centre, so names/ordering don't matter.
    [MenuItem("Maimai/Use Cabinet Button Hitboxes")]
    public static void UseHitboxes()
    {
        var game = FindGame();
        if (game == null) return;
        Transform screen = game.screen;
        var buttons = new BoxCollider[8];
        foreach (var t in Object.FindObjectsOfType<Transform>())
        {
            if (!t.name.StartsWith("Button Hitbox ")) continue;
            var mf = t.GetComponent<MeshFilter>(); // pivots may all sit at the model origin, so use mesh centre
            Vector3 l = screen.InverseTransformPoint(mf != null ? t.TransformPoint(mf.sharedMesh.bounds.center) : t.position);
            float rad = new Vector2(l.x, l.y).magnitude;
            if (rad < game.radius * 0.6f || rad > game.radius * 1.8f || Mathf.Abs(l.z) > game.radius) continue; // other player's side
            float deg = Mathf.Atan2(l.x, l.y) * Mathf.Rad2Deg;
            if (deg < 0) deg += 360;
            int lane = (int)(deg / 45f);
            var box = t.GetComponent<BoxCollider>();
            if (box == null) box = Undo.AddComponent<BoxCollider>(t.gameObject); // auto-fits the mesh
            Undo.RecordObject(box, "Hitbox trigger");
            box.isTrigger = true; // players walk through; balls are tested by maths, not physics
            if (buttons[lane] != null) Debug.LogWarning($"Two hitboxes on lane {lane + 1}: {buttons[lane].name}, {t.name}");
            buttons[lane] = box;
            Debug.Log($"Lane {lane + 1} <- {t.name}");
        }
        for (int i = 0; i < 8; i++)
            if (buttons[i] == null) { Debug.LogError($"No hitbox found for lane {i + 1}; run Align first"); return; }
        game.buttons = buttons;
        UdonSharpEditorUtility.CopyProxyToUdon(game);
        Debug.Log("Cabinet buttons wired");
    }

    static Material Mat(string name, Color c)
    {
        string p = Gen + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null)
        {
            m = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(m, p);
        }
        m.color = c;
        return m;
    }
}
