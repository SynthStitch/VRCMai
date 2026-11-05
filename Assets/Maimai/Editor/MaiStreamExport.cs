// Streamed songs, step 1: export approved charts to a static site (GitHub Pages) the world reads at runtime.
// The world can't build URLs at runtime, so it bakes one URL per slot (c/NNN.txt, a/NNN.mp3) and whatever sits
// behind a slot is the song. Slots are fixed in Stream/songs.txt so a song keeps its number across exports.
//
//   Stream/songs.txt : one line per approved song: "<slot><TAB><folder under Assets/Maimai/Charts>"  (# = comment)
//   Stream/site/     : output to publish. index.txt + c/NNN.txt (all levels, baked) + a/NNN.mp3
//
// index.txt: line 1 "VRCMAI 1 <slotCount>", then "slot<TAB>title<TAB>artist<TAB>designer<TAB>lv1|..|lv7<TAB>seconds"
// c/NNN.txt: for each level present, a line "@<n>" (1-7) followed by that level's baked chart lines.
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class MaiStreamExport
{
    public const string Dir = "Stream", Songs = Dir + "/songs.txt", Site = Dir + "/site";
    public const int Slots = 256; // URLs baked into the world; raising it needs one re-upload

    [MenuItem("Maimai/Stream: Export Site")]
    public static void Export()
    {
        if (!File.Exists(Songs))
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(Songs, "# approved songs: <slot><TAB><folder in Assets/Maimai/Charts>. Only list songs whose creators said yes.\n");
            Debug.Log("MAISTREAM created " + Songs + " - add songs and export again");
            return;
        }
        Directory.CreateDirectory(Site + "/c");
        Directory.CreateDirectory(Site + "/a");
        var index = new StringBuilder("VRCMAI 1 " + Slots + "\n");
        var credits = new StringBuilder();
        var used = new HashSet<int>();
        int n = 0;
        foreach (string raw in File.ReadAllLines(Songs))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#")) continue;
            string[] f = line.Split('\t');
            if (f.Length < 2 || !int.TryParse(f[0], out int slot) || slot < 0 || slot >= Slots || !used.Add(slot))
            { Debug.LogError("MAISTREAM bad or duplicate line: " + line); continue; }
            string dir = "Assets/Maimai/Charts/" + f[1].Trim();
            string md = dir + "/maidata.txt", track = dir + "/track.mp3";
            if (!File.Exists(md) || !File.Exists(track)) { Debug.LogError("MAISTREAM missing maidata.txt / track.mp3 in " + dir); continue; }

            MaiSetup.ConvertFile(md);
            string text = File.ReadAllText(md).Replace("\r", "");
            var chart = new StringBuilder();
            var lv = new string[7];
            for (int k = 1; k <= 7; k++)
            {
                string cp = dir + "/chart_" + k + ".txt";
                lv[k - 1] = File.Exists(cp) ? (SimaiParse.Field(text, "lv_" + k) ?? "?").Trim() : "";
                if (lv[k - 1] == "") continue;
                chart.Append('@').Append(k).Append('\n').Append(File.ReadAllText(cp).Replace("\r", ""));
            }
            if (chart.Length == 0) { Debug.LogError("MAISTREAM no charts in " + dir); continue; }
            string id = slot.ToString("000");
            File.WriteAllText(Site + "/c/" + id + ".txt", chart.ToString());
            File.Copy(track, Site + "/a/" + id + ".mp3", true);
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(track);
            index.Append(slot).Append('\t').Append(Clean(SimaiParse.Field(text, "title"))).Append('\t')
                 .Append(Clean(SimaiParse.Field(text, "artist"))).Append('\t').Append(Clean(SimaiParse.Field(text, "des"))).Append('\t')
                 .Append(string.Join("|", lv)).Append('\t').Append(clip != null ? Mathf.CeilToInt(clip.length) : 0).Append('\n');
            if (File.Exists(dir + "/credits.txt")) credits.Append("[" + id + "]\n").Append(File.ReadAllText(dir + "/credits.txt")).Append('\n');
            n++;
        }
        File.WriteAllText(Site + "/index.txt", index.ToString());
        File.WriteAllText(Site + "/CREDITS.txt", credits.ToString());
        File.WriteAllText(Site + "/.nojekyll", ""); // serve files as-is on GitHub Pages
        Debug.Log($"MAISTREAM exported {n} songs to {Site}");
    }

    static string Clean(string s) => (s ?? "?").Replace('\t', ' ').Replace('\n', ' ').Trim();

    public static void Batch()
    {
        Export();
        EditorApplication.Exit(0);
    }
}
