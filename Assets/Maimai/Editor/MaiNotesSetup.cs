// Stage 2/3 setup: note visuals on the screen render.
//  - 18 NoteShape materials (kind x variant): tap ring, slide-head star, hold hexagon, hold bar, touch marker, slide chevron;
//    variants normal / each (yellow) / break (orange)
//  - quad pools under Playfield: 64 lane notes, 16 hold bars, 40 touch markers, 12 slide slots (100 chevrons + 3 stars each)
//  - touch sensor centroids (from the baked sensor glow meshes)
using System.Collections.Generic;
using System.IO;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

public static class MaiNotesSetup
{
    const string Gen = "Assets/Maimai/Generated";

    [MenuItem("Maimai/Setup Notes (Stage 2)")]
    public static void Setup()
    {
        var btn = GameObject.Find("MaiCabinet/StartButton");
        var game = btn != null ? btn.GetComponent<MaiGame>() : null;
        var play = GameObject.Find("MaiCabinet/ScreenField/Playfield");
        if (game == null || play == null) { Debug.LogError("Run Maimai/Setup Screen + Sensors (Stage 1) first"); return; }
        Directory.CreateDirectory(Gen);

        var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Maimai/Runtime/NoteShape.shader");
        Color pink = new Color(1f, 0.45f, 0.78f), yellow = new Color(1f, 0.86f, 0.2f), orange = new Color(1f, 0.5f, 0.12f), blue = new Color(0.35f, 0.75f, 1f);
        Color[][] cols =
        {
            new[] { pink, yellow, orange },   // 0 tap ring
            new[] { blue, yellow, orange },   // 1 star
            new[] { pink, yellow, orange },   // 2 hold head
            new[] { pink, yellow, orange },   // 3 hold bar
            new[] { blue, yellow, orange },   // 4 touch
            new[] { blue, yellow, orange },   // 5 slide chevron
        };
        string[] kinds = { "Tap", "Star", "HoldHead", "HoldBar", "Touch", "Arrow" }, vars = { "", "Each", "Break" };
        var mats = new Material[18];
        for (int k = 0; k < 6; k++)
            for (int v = 0; v < 3; v++)
            {
                string p = Gen + "/Note_" + kinds[k] + vars[v] + ".mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(p);
                if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, p); }
                m.shader = shader;
                var c = cols[k][v];
                if (k == 3) c.a = 0.55f;
                m.SetColor("_Color", c);
                m.SetFloat("_Shape", k);
                m.SetFloat("_Thick", k == 2 ? 0.3f : 0.28f);
                EditorUtility.SetDirty(m);
                mats[k * 3 + v] = m;
            }

        // fresh quad pools (old sphere notes removed)
        var old = new List<GameObject>();
        foreach (Transform ch in play.transform)
            if (ch.name.StartsWith("Note") || ch.name.StartsWith("Bar") || ch.name.StartsWith("Touch") || ch.name.StartsWith("Arrow") || ch.name.StartsWith("SlideStar")) old.Add(ch.gameObject);
        foreach (var o in old) Undo.DestroyObjectImmediate(o);
        game.pool = Pool(play.transform, "Note", 64);
        game.bars = Pool(play.transform, "Bar", 16);
        game.touchPool = Pool(play.transform, "Touch", 40);
        game.arrowsPerSlot = 100;
        game.slideArrows = Pool(play.transform, "Arrow", 12 * 100);
        game.slideStars = Pool(play.transform, "SlideStar", 12 * 3);
        game.noteMats = mats;

        // judge ring under the notes (quad uv radius 0.985 = game.radius)
        string rp = Gen + "/JudgeRing.mat";
        var rm = AssetDatabase.LoadAssetAtPath<Material>(rp);
        if (rm == null) { rm = new Material(shader); AssetDatabase.CreateAsset(rm, rp); }
        rm.shader = shader;
        rm.SetColor("_Color", new Color(1f, 1f, 1f, 0.9f));
        rm.SetColor("_Accent", new Color(1f, 0.353f, 0.212f, 1f));
        rm.SetFloat("_Progress", 0f);
        game.ringMat = rm;
        rm.SetFloat("_Shape", 6);
        EditorUtility.SetDirty(rm);
        var ring = play.transform.Find("JudgeRing");
        if (ring == null)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "JudgeRing";
            Object.DestroyImmediate(q.GetComponent<Collider>());
            q.transform.SetParent(play.transform, false);
            Undo.RegisterCreatedObjectUndo(q, "Ring");
            ring = q.transform;
        }
        ring.localPosition = new Vector3(0, 0, -0.003f);
        ring.localRotation = Quaternion.identity;
        ring.localScale = Vector3.one * (game.radius * 2f / 0.985f);
        ring.GetComponent<Renderer>().sharedMaterial = rm;

        // sensor centroids (glow meshes are already in playfield-local coordinates)
        int n = game.sensorFx.Length;
        game.sensX = new float[n];
        game.sensY = new float[n];
        for (int k = 0; k < n; k++)
        {
            var v = game.sensorFx[k].GetComponent<MeshFilter>().sharedMesh.vertices;
            Vector3 c = Vector3.zero;
            foreach (var q in v) c += q;
            c /= Mathf.Max(1, v.Length);
            game.sensX[k] = c.x;
            game.sensY[k] = c.y;
        }

        UdonSharpEditorUtility.CopyProxyToUdon(game);
        EditorUtility.SetDirty(game);
        AssetDatabase.SaveAssets();
        Debug.Log($"Notes: {game.pool.Length} lane, {game.bars.Length} bars, {game.touchPool.Length} touch, {game.slideArrows.Length} arrows, {game.slideStars.Length} slide stars, {mats.Length} materials, {n} sensor centroids");
    }

    static Transform[] Pool(Transform parent, string name, int count)
    {
        var r = new Transform[count];
        for (int i = 0; i < count; i++)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = name + i;
            Object.DestroyImmediate(q.GetComponent<Collider>());
            q.transform.SetParent(parent, false);
            q.SetActive(false);
            Undo.RegisterCreatedObjectUndo(q, "Pool");
            r[i] = q.transform;
        }
        return r;
    }
}
