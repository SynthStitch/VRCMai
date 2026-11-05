// Room TV scoreboard. Both cabinets call Report() when their results screen opens (on every client, from the
// synced scores), so nothing here is synced. A linked 2P song that ends on both sides shows as VS.
using TMPro;
using UdonSharp;
using UnityEngine;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class MaiScoreboard : UdonSharpBehaviour
{
    public TextMeshPro header, single, singleBig, left, leftBig, right, rightBig;

    string[] who = new string[2], body = new string[2];
    float[] pct = new float[2], at = new float[2];
    bool[] linked = new bool[2];
    const string Dim = "#8A8A8A", Acc = "#FF5A36", Gold = "#FFC94D";
    const float VsWindow = 30f; // seconds between the two sides' results to still count as one VS match

    void Start() { Render(); }

    public void Report(int side, string name, string text, float achievement, bool link)
    {
        who[side] = name;
        body[side] = text;
        pct[side] = achievement;
        at[side] = Time.time;
        linked[side] = link;
        Render();
    }

    string Big(float v) { return v.ToString("0.0000") + "<size=45%>%</size>"; }

    string Name(int s, int win)
    {
        string tag = win == s ? "  <color=" + Gold + ">WIN</color>" : win == -1 ? "  <color=" + Dim + ">DRAW</color>" : "";
        return "<color=" + Acc + ">P" + (s + 1) + "</color>  " + who[s] + tag + "\n";
    }

    void Render()
    {
        bool vs = body[0] != null && body[1] != null && (linked[0] || linked[1]) && Mathf.Abs(at[0] - at[1]) < VsWindow;
        if (vs)
        {
            int win = pct[0] > pct[1] ? 0 : pct[1] > pct[0] ? 1 : -1;
            header.text = "<color=" + Dim + ">04  </color>VS RESULT";
            left.text = Name(0, win) + body[0];
            right.text = Name(1, win) + body[1];
            leftBig.text = Big(pct[0]);
            rightBig.text = Big(pct[1]);
            single.text = "";
            singleBig.text = "";
            return;
        }
        left.text = ""; right.text = ""; leftBig.text = ""; rightBig.text = "";
        if (body[0] == null && body[1] == null)
        {
            header.text = "<color=" + Dim + ">04  </color>SCOREBOARD";
            single.text = "<color=" + Dim + ">finish a song and the result shows up here.\nP2: press JOIN for a VS match.</color>";
            singleBig.text = "<color=#FFFFFF30>--.----</color><size=45%><color=#FFFFFF30>%</color></size>";
            return;
        }
        int s = body[1] == null ? 0 : body[0] == null ? 1 : at[1] > at[0] ? 1 : 0;
        header.text = "<color=" + Dim + ">04  </color>RESULT";
        single.text = Name(s, -2) + body[s];
        singleBig.text = Big(pct[s]);
    }
}
