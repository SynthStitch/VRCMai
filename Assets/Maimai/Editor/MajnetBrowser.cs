// Maimai/majdata.net Browser: search majdata.net, tick charts, import maidata + track into Assets/Maimai/Charts.
// API (same one the site uses): GET /api3/api/maichart/list?sort=&page=N&search=..., /maichart/{id}/chart, /maichart/{id}/track
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public class MajnetBrowser : EditorWindow
{
    const string Api = "https://majdata.net/api3/api/";
    static readonly HttpClient http = new HttpClient();

    [System.Serializable] class Item { public string id, title, artist, designer; public string[] levels; }
    [System.Serializable] class Wrap { public Item[] items; }

    string search = "tag:Original";
    int page;
    Vector2 scroll;
    Item[] items = new Item[0];
    readonly HashSet<string> pick = new HashSet<string>();

    [MenuItem("Maimai/majdata.net Browser")]
    static void Open() => GetWindow<MajnetBrowser>("majdata.net");

    void OnGUI()
    {
        EditorGUILayout.HelpBox("Default search is the site's Original section (original songs). Charts and songs belong to their creators: " +
                                "each import saves a credits.txt; ask before publishing a public world with their work.", MessageType.Info);
        using (new EditorGUILayout.HorizontalScope())
        {
            search = EditorGUILayout.TextField("Search", search);
            EditorGUILayout.LabelField("Page", GUILayout.Width(32));
            page = Mathf.Max(0, EditorGUILayout.IntField(page, GUILayout.Width(30)));
            if (GUILayout.Button("Fetch", GUILayout.Width(60))) Fetch();
        }
        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (var it in items)
        {
            var lv = new List<string>();
            if (it.levels != null) foreach (var l in it.levels) if (!string.IsNullOrEmpty(l)) lv.Add(l);
            bool on = pick.Contains(it.id);
            bool now = EditorGUILayout.ToggleLeft($"{it.title}  —  {it.artist}   [{string.Join(" / ", lv)}]   chart: {it.designer}", on);
            if (now && !on) pick.Add(it.id);
            if (!now && on) pick.Remove(it.id);
        }
        EditorGUILayout.EndScrollView();
        using (new EditorGUI.DisabledScope(pick.Count == 0))
            if (GUILayout.Button($"Import {pick.Count} selected")) Import();
    }

    void Fetch()
    {
        try
        {
            string json = http.GetStringAsync(Api + "maichart/list?sort=&page=" + page + "&search=" + System.Uri.EscapeDataString(search)).Result;
            items = JsonUtility.FromJson<Wrap>("{\"items\":" + json + "}").items ?? new Item[0];
        }
        catch (System.Exception e) { Debug.LogError("majdata.net fetch failed: " + e.Message); }
    }

    void Import()
    {
        var dirs = new List<string>();
        try
        {
            int k = 0;
            foreach (var it in items)
            {
                if (!pick.Contains(it.id)) continue;
                EditorUtility.DisplayProgressBar("majdata.net", it.title, (float)k++ / pick.Count);
                string safe = Regex.Replace(it.title ?? "", "[^A-Za-z0-9]+", "_").Trim('_');
                string dir = "Assets/Maimai/Charts/" + (safe.Length > 0 ? safe + "_" : "") + it.id.Substring(0, 8);
                Directory.CreateDirectory(dir);
                File.WriteAllBytes(dir + "/maidata.txt", http.GetByteArrayAsync(Api + "maichart/" + it.id + "/chart").Result);
                File.WriteAllBytes(dir + "/track.mp3", http.GetByteArrayAsync(Api + "maichart/" + it.id + "/track").Result);
                File.WriteAllText(dir + "/credits.txt",
                    $"{it.title}\nMusic: {it.artist}\nChart: {it.designer}\nSource: https://majdata.net/song?id={it.id}\n");
                dirs.Add(dir);
            }
        }
        catch (System.Exception e) { Debug.LogError("majdata.net import failed: " + e.Message); }
        finally { EditorUtility.ClearProgressBar(); }

        AssetDatabase.Refresh();
        foreach (string dir in dirs)
        {
            // compressed in memory: full songs decompressed on load would eat ~30MB RAM each
            var imp = AssetImporter.GetAtPath(dir + "/track.mp3") as AudioImporter;
            if (imp == null) continue;
            var s = imp.defaultSampleSettings;
            s.loadType = AudioClipLoadType.CompressedInMemory;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = 0.5f;
            imp.defaultSampleSettings = s;
            imp.SaveAndReimport();
        }
        pick.Clear();
        Debug.Log($"Imported {dirs.Count} charts");
        MaiSetup.RebuildSongList();
    }
}
