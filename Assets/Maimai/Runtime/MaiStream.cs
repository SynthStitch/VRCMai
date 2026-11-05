// Streamed songs: reads the song index from the site (see Editor/MaiStreamExport.cs) and drives the side-screen
// browser. Charts are fetched by MaiGame itself (it is the download receiver), per slot, when a song starts.
// The world can't build URLs at runtime, so every slot's chart/audio URL is baked in (Maimai/Stream: Setup Side Screen).
using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.StringLoading;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class MaiStream : UdonSharpBehaviour
{
    public VRCUrl indexUrl;
    public VRCUrl[] chartUrls, audioUrls; // index = slot
    public MaiGame[] games;               // 0 = P1, 1 = P2
    public TextMeshProUGUI list, status;

    [HideInInspector] public int count;
    [HideInInspector] public int[] slot, seconds;
    [HideInInspector] public string[] title, artist, designer, lv; // lv: count * 7

    int cursor;
    const int Rows = 5; // two lines each: title, levels
    const string Dim = "#8A8A8A", Acc = "#FF5A36";

    void Start() { Refresh(); }

    public void Refresh()
    {
        if (indexUrl == null) return;
        status.text = "<color=" + Dim + ">loading song list…</color>";
        VRCStringDownloader.LoadUrl(indexUrl, (IUdonEventReceiver)this);
    }

    public override void OnStringLoadSuccess(IVRCStringDownload result)
    {
        Parse(result.Result);
        status.text = "<color=" + Dim + ">" + count + " online songs</color>";
        Render();
        for (int i = 0; i < games.Length; i++) if (games[i] != null) games[i].StreamIndexChanged();
    }

    public override void OnStringLoadError(IVRCStringDownload result)
    {
        status.text = "<color=" + Acc + ">song list failed:</color> " + result.Error;
    }

    // "VRCMAI 1 <slots>" then "slot<TAB>title<TAB>artist<TAB>designer<TAB>lv1|..|lv7<TAB>seconds"
    void Parse(string text)
    {
        string[] lines = text.Replace("\r", "").Split('\n');
        int n = 0;
        for (int i = 1; i < lines.Length; i++) if (lines[i].Split('\t').Length >= 6) n++;
        slot = new int[n]; seconds = new int[n];
        title = new string[n]; artist = new string[n]; designer = new string[n]; lv = new string[n * 7];
        int k = 0;
        for (int i = 1; i < lines.Length; i++)
        {
            string[] f = lines[i].Split('\t');
            if (f.Length < 6) continue;
            int s;
            if (!int.TryParse(f[0], out s) || s < 0 || s >= chartUrls.Length) continue;
            slot[k] = s; title[k] = f[1]; artist[k] = f[2]; designer[k] = f[3];
            string[] l = f[4].Split('|');
            for (int j = 0; j < 7; j++) lv[k * 7 + j] = j < l.Length ? l[j] : "";
            int sec;
            seconds[k] = int.TryParse(f[5], out sec) ? sec : 0;
            k++;
        }
        count = k;
        if (cursor >= count) cursor = Mathf.Max(0, count - 1);
    }

    public void Up() { Move(-1); }
    public void Down() { Move(1); }
    public void PageUp() { Move(-Rows); }
    public void PageDown() { Move(Rows); }
    void Move(int d) { if (count == 0) return; cursor = Mathf.Clamp(cursor + d, 0, count - 1); Render(); }

    public void ToP1() { Pick(0); }
    public void ToP2() { Pick(1); }
    void Pick(int g)
    {
        if (count == 0 || g >= games.Length || games[g] == null) return;
        if (games[g].PickStream(cursor)) status.text = "<color=" + Acc + ">P" + (g + 1) + "</color>  " + title[cursor];
        else status.text = "<color=" + Dim + ">P" + (g + 1) + " is mid-song</color>";
    }

    void Render()
    {
        if (count == 0) { list.text = "<color=" + Dim + ">no online songs yet</color>"; return; }
        int top = Mathf.Clamp(cursor - Rows / 2, 0, Mathf.Max(0, count - Rows));
        string s = "";
        for (int i = top; i < top + Rows && i < count; i++)
        {
            string lvs = "";
            for (int j = 0; j < 7; j++) if (lv[i * 7 + j] != "") lvs += (lvs == "" ? "" : " ") + lv[i * 7 + j];
            string tt = title[i].Length > 17 ? title[i].Substring(0, 16) + "…" : title[i];
            s += (i == cursor ? "<color=" + Acc + ">▶</color> <b>" : "   <color=#D8D8D8>") + tt +
                 (i == cursor ? "</b>" : "</color>") + "\n     <size=70%><color=" + Dim + ">" + lvs + "</color></size>\n";
        }
        list.text = s + "<size=70%><color=" + Dim + ">" + (cursor + 1) + " / " + count + "</color></size>";
    }
}
