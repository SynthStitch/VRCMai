using System;
using System.Globalization;
using System.IO;

class P
{
    static void Eq(object a, object b, string m) { if (!a.Equals(b)) throw new Exception($"{m}: got {a}, want {b}"); }

    // every shape from every start: the generated path must pass the judge areas in order (rough sensor zones)
    static void SlideCheck()
    {
        string[] forms = { "-{0}", ">{0}", "<{0}", "^{0}", "v{0}", "p{0}", "q{0}", "pp{0}", "qq{0}", "s{0}", "z{0}", "V{1}{0}", "V{2}{0}", "w{0}" };
        int ok = 0, bad = 0;
        for (int s = 1; s <= 8; s++)
            foreach (string f in forms)
                for (int e = 1; e <= 8; e++)
                {
                    int rel = ((e - s) % 8 + 8) % 8 + 1;
                    int turnL = ((s - 3) % 8 + 8) % 8 + 1, turnR = ((s + 1) % 8 + 8) % 8 + 1;
                    string body = string.Format(f, e, turnL, turnR) + "[8:1]";
                    SimaiSlides.Slide sl;
                    try { sl = SimaiSlides.Build(body, s, 0, 120); } catch (Exception) { continue; } // invalid combos
                    for (int q = 0; q < sl.queues.Count; q++)
                    {
                        if (Visits(sl.paths[sl.paths.Count == 1 ? 0 : q], sl.queues[q])) ok++;
                        else { bad++; Console.WriteLine("path misses areas: " + s + body + " q" + q); }
                    }
                }
        Console.WriteLine("slide paths ok " + ok + ", bad " + bad);
        if (bad > 0) throw new Exception("slide geometry");
    }

    static int Zone(double x, double y)
    {
        double r = Math.Sqrt(x * x + y * y), a = (Math.Atan2(x, y) * 180 / Math.PI + 360) % 360;
        int sec = (int)(a / 45) % 8;
        return r < 0.28 ? 16 : r < 0.62 ? 8 + sec : sec;
    }

    static bool Visits(System.Collections.Generic.List<(double x, double y)> path, System.Collections.Generic.List<SimaiSlides.Group> q)
    {
        int ptr = 0;
        for (int i = 1; i < path.Count && ptr < q.Count; i++)
            for (int k = 0; k <= 10 && ptr < q.Count; k++)
            {
                double x = path[i - 1].x + (path[i].x - path[i - 1].x) * k / 10, y = path[i - 1].y + (path[i].y - path[i - 1].y) * k / 10;
                int z = Zone(x, y);
                bool hit = false;
                foreach (int a in q[ptr].areas) if (a == z || (a >= 17 && (z < 8))) hit = true; // D/E: accept the A ring
                if (hit) ptr++;
            }
        return ptr == q.Count;
    }

    static void Main(string[] args)
    {
        if (args.Length == 1 && Directory.Exists(args[0]))
        {
            // bulk: parse every level of every maidata.txt under the folder, report counts / failures
            int bad = 0;
            foreach (string f in Directory.GetFiles(args[0], "maidata.txt", SearchOption.AllDirectories))
            {
                string src = File.ReadAllText(f).Replace("\r", "");
                double first = double.TryParse(SimaiParse.Field(src, "first"), NumberStyles.Float, CultureInfo.InvariantCulture, out var fv) ? fv : 0;
                var line = new System.Text.StringBuilder(Path.GetFileName(Path.GetDirectoryName(f)) + " ");
                for (int n = 1; n <= 7; n++)
                {
                    string fumen = SimaiParse.Field(src, "inote_" + n);
                    if (string.IsNullOrWhiteSpace(fumen)) continue;
                    try
                    {
                        var ns = SimaiParse.Parse(fumen, first);
                        int taps = ns.FindAll(x => x.type != 4).Count;
                        line.Append($" lv{n}:{ns.Count}n/{taps}btn/{(ns.Count > 0 ? ns[ns.Count - 1].ms / 1000 : 0)}s");
                    }
                    catch (Exception e) { bad++; line.Append($" lv{n}:FAIL({e.GetType().Name}: {e.Message})"); }
                }
                Console.WriteLine(line);
            }
            Console.WriteLine(bad == 0 ? "ALL PARSED" : bad + " FAILED");
            return;
        }
        if (args.Length == 1)
        {
            string src = File.ReadAllText(args[0]).Replace("\r", "");
            var ns = SimaiParse.Parse(SimaiParse.Field(src, "inote_1"), double.Parse(SimaiParse.Field(src, "first") ?? "0", CultureInfo.InvariantCulture));
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[0])), "chart_1.txt"), SimaiParse.ToText(ns));
            Console.WriteLine(ns.Count + " notes");
            return;
        }

        string md = "&title=x\n&first=1.0\n&inote_5=(120){4}\n1,2b,|| comment\n1/5,15,{8}3h[4:1],4-8[4:1],\n{#0.25}A1,Ch[#1.5],E5,6?-2[8:1],7`8,\nE\n&inote_6=junk";
        var notes = SimaiParse.Parse(SimaiParse.Field(md, "inote_5"), double.Parse(SimaiParse.Field(md, "first"), CultureInfo.InvariantCulture));
        // 120bpm {4}: 0.5s/comma. {8}: 0.25s. {#0.25}: 0.25s.
        Eq(notes.Count, 15, "count");
        Eq(notes[0].ms, 1000, "first offset"); Eq(notes[1].type, 1, "break"); Eq(notes[1].flags, 1, "break flag");
        Eq(notes[2].ms, 2000, "each time"); Eq(notes[5].lane, 5, "15 shorthand");
        var hold = notes.Find(n => n.type == 2); Eq(hold.ms, 3000, "hold t"); Eq(hold.durMs, 500, "hold [4:1]");
        var star = notes.Find(n => n.type == 3); Eq(star.ms, 3250, "{8} step"); Eq(star.lane, 4, "star lane");
        var ch = notes.Find(n => n.lane == 20); Eq(ch.durMs, 1500, "touch hold #"); Eq(ch.ms, 3750, "abs step");
        Eq(notes.Exists(n => n.lane == 6 && n.type != 5), false, "headless slide has no head");
        var hs = notes.Find(n => n.lane == 6 && n.type == 5); Eq(hs.durMs, 250, "slide [8:1] at 120"); Eq(hs.extra.Split(' ')[0], "4750", "slide waits one beat");
        var s48 = notes.Find(n => n.lane == 4 && n.type == 5); Eq(s48.durMs, 500, "4-8 [4:1]");
        SlideCheck();
        string Areas(string body, int st) { var q = SimaiSlides.Build(body, st, 0, 120).queues[0]; return string.Join(" ", q.ConvertAll(g => string.Join("+", g.areas))); }
        Eq(Areas(">1[8:1]", 3), "2 1 0", "3>1 runs ccw A3 A2 A1 (mirrored circle3)");
        Eq(Areas("-6[8:1]", 2), "1 9 16 13 5", "2-6 = line5 rotated: A2 B2 C B6 A6");
        Eq(Areas("-4q7[4:1]", 1).Split(' ').Length, 4 + 12 - 1, "chain drops the shared joint area");
        Eq(notes.Find(n => n.lane == 45).type, 4, "E5 is touch not end");
        Eq(SimaiParse.Parse("(120){4}1bxh[4:1],Cf,", 0)[0].flags, 3, "break+ex hold"); Eq(SimaiParse.Parse("(120){4}1bxh[4:1],Cf,", 0)[1].flags, 4, "firework");
        Console.WriteLine("OK");
    }
}
