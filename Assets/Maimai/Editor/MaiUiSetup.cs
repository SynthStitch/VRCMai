// UI skin: dark rounded cards, small numbered mono labels (Space Mono), dot-matrix hero numbers (Doto), orange accent.
// Fonts are SIL OFL (Assets/Maimai/Fonts/*-OFL.txt); Noto Sans SC stays the CJK fallback.
// Lays out: top sub-monitor = 3 cards (01 song, 02 achievement, 03 judge); main screen = menu card + big number.
// Run order matters (it reads the HUD rect Setup Screen just wrote), so the menu item runs Screen -> Notes -> UI.
using System.Collections.Generic;
using TMPro;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

public static class MaiUiSetup
{
    const string Gen = "Assets/Maimai/Generated", Fonts = "Assets/Maimai/Fonts";

    [MenuItem("Maimai/Setup UI (screen + notes + cards)")]
    public static void SetupAll()
    {
        MaiScreenSetup.Setup();
        MaiNotesSetup.Setup();
        Setup();
    }

    public static void Setup()
    {
        var game = GameObject.Find("MaiCabinet/StartButton").GetComponent<MaiGame>();
        var noto = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Fonts + "/NotoSansSC SDF.asset");
        var mono = FontAsset("SpaceMono-Regular.ttf", "SpaceMono SDF", noto, null);
        var dots = FontAsset("Doto.ttf", "Doto SDF", mono, noto);

        var panel = AssetDatabase.LoadAssetAtPath<Material>(Gen + "/Panel.mat");
        if (panel == null) { panel = new Material(Shader.Find("Maimai/Panel")); AssetDatabase.CreateAsset(panel, Gen + "/Panel.mat"); }
        panel.shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Maimai/Runtime/PanelShape.shader");
        panel.SetColor("_Color", new Color(0.016f, 0.016f, 0.018f, 0.97f)); // raw value: the screen RT is linear, ~#222 on screen
        panel.SetColor("_Border", new Color(1f, 1f, 1f, 0.07f));
        EditorUtility.SetDirty(panel);

        // ---------- main screen (playfield-local units, judge ring radius = game.radius) ----------
        var play = GameObject.Find("MaiCabinet/ScreenField/Playfield").transform;
        float r = game.radius;
        var labels = game.menuLabels.transform;
        Card(labels, "MenuCard", new Vector3(0, 0, -0.001f), new Vector2(r * 1.3f, r * 1.3f), panel);

        var center = game.center;
        Style(center, mono, 0.36f, TextAlignmentOptions.Center); // MaiGame moves it per state
        center.transform.localPosition = new Vector3(0, r * 0.18f, -0.004f);
        center.rectTransform.sizeDelta = new Vector2(r * 1.18f, r * 0.8f);
        center.color = Color.white;

        var big = MaiSetup.Text(play, "BigText", new Vector3(0, -r * 0.38f, -0.004f), new Vector2(r * 1.18f, r * 0.5f));
        Style(big, dots, 0.8f, TextAlignmentOptions.Center);
        big.color = new Color(1, 1, 1, 0.9f);
        game.bigText = big;
        game.bigT = big.transform;
        game.centerT = center.transform;

        foreach (var h in game.hintTexts) { Style(h, mono, 0.2f, TextAlignmentOptions.Center); h.color = Color.white; }

        // ---------- top sub-monitor (field world units) ----------
        var hud = game.hud;
        var field = hud.transform.parent;
        Vector3 c0 = hud.transform.localPosition;
        float W = hud.rectTransform.sizeDelta.x, H = hud.rectTransform.sizeDelta.y, g = 0.05f * H, pad = 0.09f * H;
        float inner = W - 2 * g, w1 = inner * 0.42f, w2 = inner * 0.32f, w3 = inner - w1 - w2;
        float x1 = -W / 2 + w1 / 2, x2 = x1 + w1 / 2 + g + w2 / 2, x3 = x2 + w2 / 2 + g + w3 / 2;

        var cards = field.Find("HudCards");
        if (cards == null) { cards = new GameObject("HudCards").transform; cards.SetParent(field, false); Undo.RegisterCreatedObjectUndo(cards.gameObject, "Cards"); }
        cards.localPosition = c0;
        cards.localRotation = Quaternion.identity;
        cards.localScale = Vector3.one;
        Card(cards, "Card1", new Vector3(x1, 0, 0.002f), new Vector2(w1, H), panel);
        Card(cards, "Card2", new Vector3(x2, 0, 0.002f), new Vector2(w2, H), panel);
        Card(cards, "Card3", new Vector3(x3, 0, 0.002f), new Vector2(w3, H), panel);

        // song card: the old HUD text moves into card 1
        hud.transform.localPosition = c0 + new Vector3(x1, 0, 0);
        hud.rectTransform.sizeDelta = new Vector2(w1 - 2 * pad, H - 2 * pad);
        Style(hud, mono, 1.0f * H, TextAlignmentOptions.TopLeft);
        hud.lineSpacing = 14f;
        hud.color = Color.white;

        game.hudBigLabel = MaiSetup.Text(cards, "BigLabel", new Vector3(x2, 0, 0), new Vector2(w2 - 2 * pad, H - 2 * pad));
        Style(game.hudBigLabel, mono, 0.48f * H, TextAlignmentOptions.TopLeft);
        game.hudBig = MaiSetup.Text(cards, "Big", new Vector3(x2, -0.1f * H, 0), new Vector2(w2 - pad, H * 0.8f));
        Style(game.hudBig, dots, 2.0f * H, TextAlignmentOptions.Center);
        game.hudStats = MaiSetup.Text(cards, "Stats", new Vector3(x3, 0, 0), new Vector2(w3 - 2 * pad, H - 2 * pad));
        Style(game.hudStats, mono, 0.62f * H, TextAlignmentOptions.TopLeft);
        game.hudStats.lineSpacing = -8f;

        UdonSharpEditorUtility.CopyProxyToUdon(game);
        EditorUtility.SetDirty(game);
        AssetDatabase.SaveAssets();
        Debug.Log($"UI: hud {W:F3}x{H:F3}, cards {w1:F3}/{w2:F3}/{w3:F3}, fonts {mono.name} + {dots.name}");
    }

    static void Style(TextMeshPro t, TMP_FontAsset f, float size, TextAlignmentOptions align)
    {
        t.font = f;
        t.enableAutoSizing = false;
        t.fontSize = size;
        t.alignment = align;
        t.enableWordWrapping = true;              // long / CJK titles wrap instead of being cut
        t.overflowMode = TextOverflowModes.Overflow;
        t.richText = true;
        EditorUtility.SetDirty(t);
    }

    static void Card(Transform parent, string name, Vector3 pos, Vector2 size, Material m)
    {
        var t = parent.Find(name);
        if (t == null)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = name;
            Object.DestroyImmediate(q.GetComponent<Collider>());
            q.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(q, "Card");
            t = q.transform;
        }
        t.localPosition = pos;
        t.localRotation = Quaternion.identity;
        t.localScale = new Vector3(size.x, size.y, 1);
        t.GetComponent<Renderer>().sharedMaterial = m;
    }

    static TMP_FontAsset FontAsset(string file, string name, TMP_FontAsset fb1, TMP_FontAsset fb2)
    {
        string path = Fonts + "/" + name + ".asset";
        var fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (fa == null)
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(Fonts + "/" + file);
            fa = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic);
            fa.name = name;
            AssetDatabase.CreateAsset(fa, path);
            fa.atlasTextures[0].name = name + " Atlas";
            AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
            fa.material.name = name + " Material";
            AssetDatabase.AddObjectToAsset(fa.material, fa);
        }
        fa.fallbackFontAssetTable = new List<TMP_FontAsset>();
        if (fb1 != null) fa.fallbackFontAssetTable.Add(fb1);
        if (fb2 != null) fa.fallbackFontAssetTable.Add(fb2);
        EditorUtility.SetDirty(fa);
        return fa;
    }
}
