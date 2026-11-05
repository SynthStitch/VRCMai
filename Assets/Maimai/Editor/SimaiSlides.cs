// Slide baking: simai slide text -> star path (equal-spaced points) + MajdataPlay judge queues. Pure C#, no Unity deps.
// Judge queues are MajdataPlay's SlideTables (normalised to start 1), mirrored/rotated like SlideDrop.Init,
// chains joined like ConnSlideInfo (next segment's first area dropped, all areas skippable).
// Paths are generated procedurally (MajdataPlay uses hand-made prefabs).
// ponytail: path shapes approximate the prefab art; judging uses the exact tables so play feel matches.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

public static class SimaiSlides
{
    // area codes = MajdataPlay SensorArea order: 0-7 A1-8, 8-15 B1-8, 16 C, 17-24 D1-8, 25-32 E1-8
    public struct Group { public int on, fin; public bool skip, last; public int[] areas; }
    public class Slide
    {
        public double startSec, durSec, judgeSec, staySec;
        public List<List<(double x, double y)>> paths = new List<List<(double, double)>>();
        public List<List<Group>> queues = new List<List<Group>>();   // on/fin = permille of path progress
    }

    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    // name -> "areas on fin [n][L] | ..." (n = not skippable, L = last); const after '#'
    static readonly Dictionary<string, string> Tables = new Dictionary<string, string>
    {
        ["circle2"] = "A1 0 3 n|A2 5 7 L#0.465",
        ["circle3"] = "A1 0 3|A2 7 11 n|A3 13 15 L#0.233",
        ["circle4"] = "A1 0 3|A2 7 11|A3 14 19|A4 21 23 L#0.155",
        ["circle5"] = "A1 0 3|A2 7 11|A3 14 19|A4 23 27|A5 29 31 L#0.116",
        ["circle6"] = "A1 0 3|A2 7 11|A3 14 19|A4 23 27|A5 31 35|A6 37 39 L#0.093",
        ["circle7"] = "A1 0 3|A2 7 11|A3 14 19|A4 23 27|A5 31 35|A6 39 43|A7 45 47 L#0.078",
        ["circle8"] = "A1 0 3|A2 7 11|A3 14 19|A4 23 27|A5 31 35|A6 39 43|A7 46 51|A8 53 55 L#0.066",
        ["circle1"] = "A1 0 3|A2 7 11|A3 14 19|A4 23 27|A5 31 35|A6 39 43|A7 46 51|A8 54 59|A1 61 63 L#0.058",
        ["line3"] = "A1 0 3|A2+B2 6 9 n|A3 10 13 L#0.182",
        ["line4"] = "A1 0 4|B2 6 9|B3 11 14|A4 15 18 L#0.19",
        ["line5"] = "A1 0 4|B1 5 7|C 10 12|B5 13 16|A5 17 19 L#0.152",
        ["line6"] = "A1 0 4|B8 6 9|B7 11 14|A6 15 18 L#0.19",
        ["line7"] = "A1 0 3|A8+B8 6 9 n|A7 10 13 L#0.182",
        ["v1"] = "A1 0 3|B1 4 7|C 8 13|B1 14 16|A1 17 19 L#0.185",
        ["v2"] = "A1 0 3|B1 4 7|C 8 13|B2 14 16|A2 17 19 L#0.15",
        ["v3"] = "A1 0 3|B1 4 7|C 8 13|B3 14 16|A3 17 19 L#0.158",
        ["v4"] = "A1 0 3|B1 4 7|C 8 13|B4 14 16|A4 17 19 L#0.158",
        ["v6"] = "A1 0 3|B1 4 7|C 8 13|B6 14 16|A6 17 19 L#0.158",
        ["v7"] = "A1 0 3|B1 4 7|C 8 13|B7 14 16|A7 17 19 L#0.158",
        ["v8"] = "A1 0 3|B1 4 7|C 8 13|B8 14 16|A8 17 19 L#0.154",
        ["ppqq1"] = "A1 0 3|B1 5 7|C 10 13|B4 15 17|A3 21 26|A2 29 32|A1 33 35 L#0.065",
        ["ppqq2"] = "A1 0 3|B1 5 7|C 9 13|B4 14 17|A3 20 25|A2 26 28 L#0.086",
        ["ppqq3"] = "A1 0 3|B1 4 7|C 9 13|B4 14 17|A3 19 22 L#0.157",
        ["ppqq4"] = "A1 0 3|B1 5 7|C 9 13|B4 14 17|A3 20 25|A2 28 33|B1 34 37|C 39 43|B4 44 46|A4 47 49 L#0.065",
        ["ppqq5"] = "A1 0 3|B1 5 7|C 9 13|B4 14 17|A3 20 25|A2 28 33|B1 34 37|C 39 43|B5 44 46|A5 47 49 L#0.065",
        ["ppqq6"] = "A1 0 3|B1 5 7|C 9 13|B4 14 17|A3 20 25|A2 28 33|B1 34 37|C+B8 38 40|B7+B6 42 44|A6 46 48 L#0.067",
        ["ppqq7"] = "A1 0 3|B1 5 7|C 9 13|B4 14 17|A3 20 25|A2 28 33|B1 34 37|B8 38 42|A7 43 46 L#0.079",
        ["ppqq8"] = "A1 0 3|B1 5 7|C 9 13|B4 14 17|A3 20 25|A2 28 33|B1+A1 35 37|A8 38 41 L#0.0626",
        ["L2"] = "A1 0 3|B8+A8 6 10 n|A7 12 19|B8 21 24|B1 25 28|A2 29 32 L#0.1",
        ["L3"] = "A1 0 3|B8+A8 6 10 n|A7 12 18|B7 20 22|C 25 27|B3 28 31|A3 32 34 L#0.104",
        ["L4"] = "A1 0 3|B8+A8 6 10 n|A7 12 19|B6 21 24|B5 25 28|A4 29 32 L#0.098",
        ["L5"] = "A1 0 3|B8+A8 6 10 n|A7 12 18|B6+A6 21 24 n|A5 27 28 L#0.105",
        ["s"] = "A1 0 4|B8 7 9|B7 10 12|C 14 17|B3 19 21|B4 22 25|A5 27 30 L#0.13",
        ["pq1"] = "A1 0 4|B8 5 8|B7 9 11|B6 12 14|B5 15 17|B4 19 21|B3 22 24|B2 25 29|A1 30 33 L#0.095",
        ["pq2"] = "A1 0 4|B8 5 8|B7 9 11|B6 12 14|B5 16 18|B4 19 21|B3 22 26|A2 27 30 L#0.112",
        ["pq3"] = "A1 0 4|B8 5 8|B7 9 11|B6 12 14|B5 16 18|B4 20 23|A3 25 27 L#0.125",
        ["pq4"] = "A1 0 4|B8 5 8|B7 9 11|B6 12 14|B5 16 20|A4 22 24 L#0.139",
        ["pq5"] = "A1 0 4|B8 5 8|B7 9 12|B6 14 17|A5 19 21 L#0.16",
        ["pq6"] = "A1 0 4|B8 5 8|B7 9 11|B6 13 15|B5 16 18|B4 19 21|B3 22 24|B2 25 27|B1 28 30|B8 31 33|B7 35 38|A6 40 42 L#0.08",
        ["pq7"] = "A1 0 4|B8 7 9|B7 10 12|B6 13 15|B5 16 18|B4 20 22|B3 23 25|B2 26 28|B1 30 32|B8 33 36|A7 37 40 L#0.084",
        ["pq8"] = "A1 0 4|B8 5 8|B7 9 11|B6 12 14|B5 15 17|B4 19 21|B3 22 24|B2 25 27|B1 28 32|A8 33 36 L#0.0895",
        // wifi: left / centre / right queues (modern mode)
        ["wifiL"] = "A1 0 0|B8 2 2|B7 4 4|A6+D6 7 7 L#0.16287",
        ["wifiC"] = "A1 0 0|B1 2 2|C 4 4|A5+B5 7 7 L#0.16287",
        ["wifiR"] = "A1 0 0|B2 2 2|B3 4 4|A4+D5 7 7 L#0.16287",
    };

    static int Code(string a)
    {
        if (a == "C") return 16;
        int i = a[1] - '1';
        switch (a[0]) { case 'A': return i; case 'B': return 8 + i; case 'D': return 17 + i; default: return 25 + i; }
    }

    // SensorArea.Diff
    public static int Rotate(int code, int d)
    {
        if (code == 16) return code;
        d = ((d % 8) + 8) % 8;
        int baseCode = code < 16 ? (code / 8) * 8 : code < 25 ? 17 : 25;
        return baseCode + (code - baseCode + d) % 8;
    }

    // SensorArea.Mirror(A1): reflect across the axis through button 1
    public static int Mirror(int code)
    {
        if (code == 16) return code;
        int baseCode = code < 16 ? (code / 8) * 8 : code < 25 ? 17 : 25;
        int i = code - baseCode;                       // 0..7
        int m = code < 16 ? (8 - i) % 8 : (9 - i) % 8; // A/B: k -> 2-k ; D/E (on the boundaries): k -> 3-k
        return baseCode + m;
    }

    static List<Group> Table(string name, out double cnst)
    {
        string[] t = Tables[name].Split('#');
        cnst = double.Parse(t[1], Inv);
        var q = new List<Group>();
        foreach (string g in t[0].Split('|'))
        {
            string[] f = g.Split(' ');
            var areas = Array.ConvertAll(f[0].Split('+'), Code);
            string flags = f.Length > 3 ? f[3] : "";
            q.Add(new Group { areas = areas, on = int.Parse(f[1]), fin = int.Parse(f[2]), skip = !flags.Contains("n"), last = flags.Contains("L") });
        }
        return q;
    }

    // ---------- geometry (unit circle = judge ring, angle clockwise from +y like the screen) ----------
    static (double x, double y) Polar(double deg, double r) => (r * Math.Sin(deg * Math.PI / 180), r * Math.Cos(deg * Math.PI / 180));
    static double Ang(int lane) => 22.5 + 45 * (lane - 1);
    static (double x, double y) P(int lane) => Polar(Ang(lane), 1);
    static int Wrap(int lane) => ((lane - 1) % 8 + 8) % 8 + 1;
    static int Rel(int s, int e) => ((e - s) % 8 + 8) % 8 + 1; // 1 = same button
    static bool Upper(int s) => s == 7 || s == 8 || s == 1 || s == 2;
    static int MirrorKey(int rel) => rel == 1 ? 1 : 10 - rel;

    static void Line(List<(double, double)> pts, (double x, double y) b)
    {
        var a = pts[pts.Count - 1];
        int n = Math.Max(1, (int)(Dist(a, b) / 0.02));
        for (int i = 1; i <= n; i++) pts.Add((a.Item1 + (b.x - a.Item1) * i / n, a.Item2 + (b.y - a.Item2) * i / n));
    }

    static void Arc(List<(double, double)> pts, (double x, double y) c, double r, double fromDeg, double sweepDeg)
    {
        int n = Math.Max(2, (int)(Math.Abs(sweepDeg) * Math.PI / 180 * r / 0.02));
        for (int i = 0; i <= n; i++) { var p = Polar(fromDeg + sweepDeg * i / n, r); pts.Add((c.x + p.x, c.y + p.y)); }
    }

    static double Dist((double x, double y) a, (double x, double y) b) => Math.Sqrt((a.x - b.x) * (a.x - b.x) + (a.y - b.y) * (a.y - b.y));
    static double DegOf(double x, double y) => Math.Atan2(x, y) * 180 / Math.PI;

    // from P, tangent onto circle (c, r), travel cw (+1) or ccw (-1) round it, leave tangentially to Q
    static void Loop(List<(double, double)> pts, (double x, double y) c, double r, int dir, (double x, double y) q, int extraTurns)
    {
        var p = pts[pts.Count - 1];
        double inDeg = TangentDeg(p, c, r, dir, true), outDeg = TangentDeg(q, c, r, dir, false);
        double sweep = dir > 0 ? Mod(outDeg - inDeg) : -Mod(inDeg - outDeg);
        if (Math.Abs(sweep) < 30) sweep += dir * 360;  // ponytail: tiny arc = the table wants another lap
        sweep += dir * 360 * extraTurns;
        var t0 = Polar(inDeg, r);
        Line(pts, (c.x + t0.x, c.y + t0.y));
        Arc(pts, c, r, inDeg, sweep);
        Line(pts, q);
    }

    static double Mod(double d) => ((d % 360) + 360) % 360;

    // tangent point angle (around c) for a line through external point p; entering = p -> circle
    static double TangentDeg((double x, double y) p, (double x, double y) c, double r, int dir, bool entering)
    {
        double L = Dist(p, c), th = Math.Acos(Math.Min(1, r / L)) * 180 / Math.PI, b = DegOf(p.x - c.x, p.y - c.y);
        foreach (double g in new[] { b + th, b - th })
        {
            var t = Polar(g, r);
            var tan = Polar(g + 90 * dir, 1);                // travel direction at that point
            double dx = entering ? t.x + c.x - p.x : p.x - t.x - c.x, dy = entering ? t.y + c.y - p.y : p.y - t.y - c.y;
            if (dx * tan.x + dy * tan.y > 0) return g;
        }
        return b;
    }

    // one segment's path from s to e (appends to pts, which already ends at P(s)); returns table name (mirrored names handled)
    static string Segment(List<(double, double)> pts, string shape, int s, int e, int turn, out bool mirror)
    {
        mirror = false;
        int rel = Rel(s, e);
        // same validity rules as MajdataPlay's DetectShapeFromText
        if ((shape == "s" || shape == "z" || shape == "w") && rel != 5 || shape == "v" && rel == 5 || shape == "^" && (rel == 1 || rel == 5))
            throw new FormatException("invalid slide " + s + shape + e);
        switch (shape)
        {
            case "-":
                Line(pts, P(e));
                return "line" + rel;
            case ">": case "<": case "^":
            {
                bool cw = shape == "^" ? rel < 5 : (shape == ">") == Upper(s);
                mirror = !cw;
                double sweep = cw ? (rel == 1 ? 360 : (rel - 1) * 45) : -(rel == 1 ? 360 : (9 - rel) * 45);
                pts.RemoveAt(pts.Count - 1);
                Arc(pts, (0, 0), 1, Ang(s), sweep);
                return "circle" + (cw ? rel : MirrorKey(rel));
            }
            case "v":
                Line(pts, (0, 0)); Line(pts, P(e));
                return "v" + rel;
            case "p": case "q":
                mirror = shape == "q";
                Loop(pts, (0, 0), 0.48, mirror ? 1 : -1, P(e), 0);
                return "pq" + (mirror ? MirrorKey(rel) : rel);
            case "pp": case "qq":
            {
                mirror = shape == "qq";
                int mrel = mirror ? MirrorKey(rel) : rel;
                Loop(pts, Polar(Ang(s) + (mirror ? -67.5 : 67.5), 0.47), 0.47, mirror ? 1 : -1, P(e), mrel >= 4 && mrel <= 7 ? 1 : 0);
                return "ppqq" + mrel;
            }
            case "s": case "z":
                mirror = shape == "z";
                Line(pts, Polar(Ang(s) + (mirror ? 80 : -80), 0.5));
                Line(pts, Polar(Ang(s) + (mirror ? -100 : 100), 0.5));
                Line(pts, P(e));
                return "s";
            case "V":
            {
                int trel = Rel(s, turn);
                mirror = trel == 3;
                if (trel != 3 && trel != 7 || (mirror ? MirrorKey(rel) : rel) < 2 || (mirror ? MirrorKey(rel) : rel) > 5) throw new FormatException("invalid V slide");
                Line(pts, P(turn)); Line(pts, P(e));
                return "L" + (mirror ? MirrorKey(rel) : rel);
            }
        }
        throw new FormatException("unknown slide shape " + shape);
    }

    static List<(double, double)> Resample(List<(double x, double y)> src, int maxPts, out double len)
    {
        len = 0;
        var cum = new double[src.Count];
        for (int i = 1; i < src.Count; i++) cum[i] = len += Dist(src[i - 1], src[i]);
        int n = Math.Max(2, Math.Min(maxPts, (int)Math.Round(len / 0.065) + 1));
        var o = new List<(double, double)>();
        int j = 1;
        for (int k = 0; k < n; k++)
        {
            double d = len * k / (n - 1);
            while (j < src.Count - 1 && cum[j] < d) j++;
            double seg = cum[j] - cum[j - 1], u = seg > 0 ? (d - cum[j - 1]) / seg : 0;
            o.Add((src[j - 1].x + (src[j].x - src[j - 1].x) * u, src[j - 1].y + (src[j].y - src[j - 1].y) * u));
        }
        return o;
    }

    // body = text after the head (e.g. "-5[8:1]" or "q7-2[1:1]b"); start = head lane; headSec = head time
    public static Slide Build(string body, int start, double headSec, double bpm, int maxPts = 100)
    {
        var sl = new Slide();
        // ---- tokenize into segments ----
        var shapes = new List<string>(); var ends = new List<int>(); var turns = new List<int>(); var durs = new List<double>();
        double wait = -1;
        int i = 0;
        while (i < body.Length)
        {
            char c = body[i];
            string shape = null;
            if ((c == 'p' || c == 'q') && i + 1 < body.Length && body[i + 1] == c) { shape = c + "" + c; i += 2; }
            else if ("-^<>vpqszVw".IndexOf(c) >= 0) { shape = c.ToString(); i++; }
            else if (c == '[')
            {
                int e = body.IndexOf(']', i);
                Bracket(body.Substring(i + 1, e - i - 1), bpm, out double w, out double d);
                if (durs.Count > 0) durs[durs.Count - 1] = d;
                if (wait < 0) wait = w;
                i = e + 1; continue;
            }
            else { i++; continue; } // modifiers (b, x, ...) handled by the caller
            int turn = 0;
            if (shape == "V") { turn = body[i] - '0'; i++; }
            int end = body[i] - '0'; i++;
            shapes.Add(shape); ends.Add(end); turns.Add(turn); durs.Add(-1);
        }
        if (shapes.Count == 0) throw new FormatException("no slide in " + body);
        double total = 0;
        foreach (double d in durs) if (d > 0) total += d;
        if (total <= 0) throw new FormatException("slide without duration: " + body);
        if (wait < 0) wait = 60 / bpm;
        sl.startSec = headSec + wait;
        sl.durSec = total;

        // ---- wifi: 3 straight paths, 3 queues, whole length ----
        if (shapes[0] == "w")
        {
            int e = ends[0];
            if (Rel(start, e) != 5) throw new FormatException("invalid wifi " + start + "w" + e);
            foreach (int lane in new[] { Wrap(e + 1), e, Wrap(e - 1) })
            {
                var raw = new List<(double, double)> { P(start) };
                Line(raw, P(lane));
                sl.paths.Add(Resample(raw, maxPts / 3, out _));
            }
            foreach (string side in new[] { "wifiL", "wifiC", "wifiR" })
                sl.queues.Add(Place(Table(side, out double wc), false, start, 0, 1000.0 / 7));
            Table("wifiC", out double cw);
            sl.judgeSec = sl.startSec + total * (1 - cw);
            sl.staySec = total * cw;
            return sl;
        }

        // ---- normal / chained ----
        var pts = new List<(double x, double y)> { P(start) };
        var segLen = new List<double>(); var segTables = new List<List<Group>>(); var segMirror = new List<bool>(); var segStart = new List<int>();
        var segConst = new List<double>();
        int cur = start;
        for (int k = 0; k < shapes.Count; k++)
        {
            int before = pts.Count;
            double lenBefore = PathLen(pts);
            string name = Segment(pts, shapes[k], cur, ends[k], turns[k], out bool mirror);
            segLen.Add(PathLen(pts) - lenBefore);
            segTables.Add(Table(name, out double cn));
            segConst.Add(cn);
            segMirror.Add(mirror); segStart.Add(cur);
            cur = ends[k];
        }
        double whole = 0; foreach (double l in segLen) whole += l;
        var queue = new List<Group>();
        double acc = 0;
        for (int k = 0; k < shapes.Count; k++)
        {
            var t = segTables[k];
            int maxP = t[t.Count - 1].fin;
            double pm0 = acc / whole * 1000, pmScale = segLen[k] / whole * 1000 / Math.Max(1, maxP);
            var placed = Place(t, segMirror[k], segStart[k], pm0, pmScale);
            if (k > 0) placed.RemoveAt(0);                 // shared joint area
            if (k < shapes.Count - 1) { var g = placed[placed.Count - 1]; g.last = false; placed[placed.Count - 1] = g; }
            queue.AddRange(placed);
            acc += segLen[k];
        }
        if (shapes.Count > 1 && queue.Count >= 4)          // ConnSlide: every area skippable
            for (int k = 0; k < queue.Count; k++) { var g = queue[k]; g.skip = true; queue[k] = g; }
        sl.queues.Add(queue);
        sl.paths.Add(Resample(pts, maxPts, out _));

        // judge timing from the final segment (its share of the time is its share of the length)
        double endLen = segLen[segLen.Count - 1] / whole * total, kc = segConst[segConst.Count - 1];
        sl.judgeSec = sl.startSec + total - endLen * kc;
        sl.staySec = endLen * kc;
        return sl;
    }

    static double PathLen(List<(double x, double y)> p)
    {
        double l = 0;
        for (int i = 1; i < p.Count; i++) l += Dist(p[i - 1], p[i]);
        return l;
    }

    static List<Group> Place(List<Group> table, bool mirror, int start, double pm0, double pmScale)
    {
        var o = new List<Group>();
        foreach (var g0 in table)
        {
            var g = g0;
            g.areas = Array.ConvertAll(g0.areas, a => Rotate(mirror ? Mirror(a) : a, start - 1));
            g.on = (int)Math.Round(pm0 + g0.on * pmScale);
            g.fin = (int)Math.Round(pm0 + g0.fin * pmScale);
            o.Add(g);
        }
        return o;
    }

    // [a:b]  [bpm#a:b]  [bpm#sec]  [wait##sec]  [wait##a:b]
    static void Bracket(string s, double bpm, out double wait, out double dur)
    {
        int dd = s.IndexOf("##", StringComparison.Ordinal);
        if (dd >= 0)
        {
            wait = double.Parse(s.Substring(0, dd), Inv);
            dur = DurPart(s.Substring(dd + 2), bpm);
            return;
        }
        int h = s.IndexOf('#');
        if (h >= 0)
        {
            if (h > 0) bpm = double.Parse(s.Substring(0, h), Inv);
            wait = 60 / bpm;
            dur = DurPart(s.Substring(h + 1), bpm);
            return;
        }
        wait = 60 / bpm;
        dur = DurPart(s, bpm);
    }

    static double DurPart(string s, double bpm)
    {
        int h = s.IndexOf('#');
        if (h >= 0) { bpm = double.Parse(s.Substring(0, h), Inv); s = s.Substring(h + 1); }
        if (!s.Contains(":")) return double.Parse(s, Inv);
        string[] ab = s.Split(':');
        return 240 / bpm / double.Parse(ab[0], Inv) * double.Parse(ab[1], Inv);
    }

    // chart text: "paths queues" = "x,y;x,y/..." (milli-units) and "on,fin,flags,code[,code];.../..."
    public static string Encode(Slide s)
    {
        var sb = new StringBuilder();
        for (int p = 0; p < s.paths.Count; p++)
        {
            if (p > 0) sb.Append('/');
            for (int k = 0; k < s.paths[p].Count; k++)
            {
                if (k > 0) sb.Append(';');
                sb.Append((int)Math.Round(s.paths[p][k].x * 1000)).Append(',').Append((int)Math.Round(s.paths[p][k].y * 1000));
            }
        }
        sb.Append(' ');
        for (int q = 0; q < s.queues.Count; q++)
        {
            if (q > 0) sb.Append('/');
            for (int k = 0; k < s.queues[q].Count; k++)
            {
                var g = s.queues[q][k];
                if (k > 0) sb.Append(';');
                sb.Append(g.on).Append(',').Append(g.fin).Append(',').Append((g.skip ? 1 : 0) | (g.last ? 2 : 0));
                foreach (int a in g.areas) sb.Append(',').Append(a);
            }
        }
        return sb.ToString();
    }
}
