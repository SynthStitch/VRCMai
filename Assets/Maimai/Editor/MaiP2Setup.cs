// Player 2: a copy of the P1 cabinet object (MaiCabinet -> MaiCabinetP2) re-aimed at the model's P2 side.
// Every setup step reads MaiSide, so the same Align / Hitboxes / Balls / Screen / Notes / UI code builds either side.
// P2 reuses P1's baked sensor grid: sensors are baked in screen-local units, and both screens are the same shape.
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

public static class MaiSide
{
    public static string Root = "MaiCabinet", Display = "Display P1", Body = "Body P1", Suffix = "";
    public static Vector3 Field = new Vector3(0, -500, 0); // where this side's screen is rendered (below the world)
    public static bool IsP2 => Suffix != "";

    public static void Use(bool p2)
    {
        Root = p2 ? "MaiCabinetP2" : "MaiCabinet";
        Display = p2 ? "Display P2" : "Display P1";
        Body = p2 ? "Body P2" : "Body P1";
        Suffix = p2 ? "P2" : "";
        Field = new Vector3(p2 ? 20 : 0, -500, 0);
    }

    public static MaiGame Game()
    {
        var b = GameObject.Find(Root + "/StartButton");
        return b != null ? b.GetComponent<MaiGame>() : null;
    }
}

public static class MaiP2Setup
{
    [MenuItem("Maimai/Setup Player 2 (copy of P1 on the P2 side)")]
    public static void Setup()
    {
        try
        {
            MaiSide.Use(false);
            var p1 = MaiSide.Game();
            if (p1 == null) { Debug.LogError("No P1 cabinet (MaiCabinet/StartButton)"); return; }
            var old = GameObject.Find("MaiCabinetP2");
            if (old != null) Undo.DestroyObjectImmediate(old);
            var src = p1.transform.parent.gameObject;
            var copy = Object.Instantiate(src, src.transform.parent);
            copy.name = "MaiCabinetP2";
            Undo.RegisterCreatedObjectUndo(copy, "Player 2");

            // the model only has P1 button hitboxes / light anchors: copy them over by the P1 -> P2 body offset
            var dx = GameObject.Find("DX Unity").transform;
            Vector3 off = GameObject.Find("Body P2").GetComponent<Renderer>().bounds.center - GameObject.Find("Body P1").GetComponent<Renderer>().bounds.center;
            foreach (var n in new[] { "Button Hitbox", "ButtonLight" })
            {
                var stale = dx.Find(n + " P2");
                if (stale != null) Undo.DestroyObjectImmediate(stale.gameObject);
                var g = dx.Find(n);
                var c = Object.Instantiate(g.gameObject, g.parent);
                c.name = n + " P2";
                c.transform.SetPositionAndRotation(g.position + off, g.rotation);
                Undo.RegisterCreatedObjectUndo(c, "Player 2");
            }
            Debug.Log("MAIP2 side offset " + off);

            MaiSide.Use(true);
            var p2 = MaiSide.Game();
            p2.leader = p1;
            UdonSharpEditorUtility.CopyProxyToUdon(p2);
            MaiSetup.Align();
            // Display P2's mesh isn't an exact copy of P1's, so the fit drifts (~4 cm, scale 0.80 vs 0.69):
            // the sides are a pure translation, so place P2's screen as P1's shifted by the body offset
            p2.screen.SetPositionAndRotation(p1.screen.position + off, p1.screen.rotation);
            p2.screen.localScale = p1.screen.localScale;
            MaiSetup.UseHitboxes();
            MaiSetup.AddBalls();
            MaiScreenSetup.Setup();
            MaiNotesSetup.Setup();
            MaiUiSetup.Setup();
            Debug.Log("MAIP2 done: P2 at " + p2.screen.position + ", leader " + p2.leader.name + ", buttons " + p2.buttons.Length + ", balls " + p2.balls.Length);
        }
        finally { MaiSide.Use(false); }
    }
}
