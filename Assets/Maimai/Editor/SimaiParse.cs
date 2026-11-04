// Minimal simai -> flat note list. Pure C#, no Unity deps (unit-tested outside Unity).
// Output line format (one note per line, sorted by time): "timeMs lane type durMs flags"
//   type: 0 tap, 1 break, 2 hold, 3 star (slide head), 4 touch (durMs > 0 = touch hold), 5 slide body
//   type 5 lines carry extra fields: "startMs judgeMs stayMs paths queues" (see SimaiSlides.Encode); timeMs = head time
//   flags: 1 break, 2 ex, 4 firework (touch 'f')
//   lane: 1-8 for buttons; for touch, encoded as zoneLetterIndex*10+num (A1=1, B3=13, C=20, E5=45)
// ponytail: mine notes ('m') are dropped, not judged.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

public static class SimaiParse
{
    public struct Note { public int ms, lane, type, durMs, flags; public string extra; }

    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    // Returns value of "&key=" up to the next line starting with '&', or null.
    public static string Field(string maidata, string key)
    {
        string tag = "&" + key + "=";
        int i = maidata.IndexOf(tag, StringComparison.Ordinal);
        if (i < 0) return null;
        i += tag.Length;
        int end = maidata.IndexOf("\n&", i, StringComparison.Ordinal);
        return (end < 0 ? maidata.Substring(i) : maidata.Substring(i, end - i)).Trim();
    }

    public static List<Note> Parse(string fumen, double firstSec)
    {
        var notes = new List<Note>();
        double bpm = 120, t = firstSec, absStep = -1;
        int div = 4;
        var group = new StringBuilder();

        for (int i = 0; i < fumen.Length; i++)
        {
            char c = fumen[i];
            if (c == '|' && i + 1 < fumen.Length && fumen[i + 1] == '|')
            {
                while (i < fumen.Length && fumen[i] != '\n') i++;
                continue;
            }
            if (char.IsWhiteSpace(c)) continue;
            if (c == '(')
            {
                int e = fumen.IndexOf(')', i);
                bpm = double.Parse(fumen.Substring(i + 1, e - i - 1), Inv);
                i = e; continue;
            }
            if (c == '{')
            {
                int e = fumen.IndexOf('}', i);
                string s = fumen.Substring(i + 1, e - i - 1);
                if (s.StartsWith("#")) absStep = double.Parse(s.Substring(1), Inv);
                else { div = int.Parse(s, Inv); absStep = -1; }
                i = e; continue;
            }
            if (c == ',')
            {
                string g = group.ToString();
                group.Clear();
                if (g == "E") break;
                if (g.Length > 0) ParseGroup(g, t, bpm, notes);
                t += absStep > 0 ? absStep : 240.0 / bpm / div;
                continue;
            }
            if (c == '[')  // copy bracket verbatim so '/' or ',' inside never splits
            {
                int e = fumen.IndexOf(']', i);
                group.Append(fumen, i, e - i + 1);
                i = e; continue;
            }
            group.Append(c);
        }
        notes.Sort((a, b) => a.ms != b.ms ? a.ms.CompareTo(b.ms) : a.lane.CompareTo(b.lane));
        return notes;
    }

    static void ParseGroup(string g, double t, double bpm, List<Note> notes)
    {
        foreach (string part in g.Split('/', '`'))
        {
            if (part.Length == 0) continue;
            // "15" shorthand = each tap on 1 and 5
            if (part.Length >= 2 && IsAllLaneDigits(part))
            {
                foreach (char d in part) notes.Add(new Note { ms = Ms(t), lane = d - '0', type = 0 });
                continue;
            }
            ParseNote(part, t, bpm, notes);
        }
    }

    static bool IsAllLaneDigits(string s)
    {
        foreach (char ch in s) if (ch < '1' || ch > '8') return false;
        return true;
    }

    static void ParseNote(string p, double t, double bpm, List<Note> notes)
    {
        char c0 = p[0];
        var n = new Note { ms = Ms(t) };
        string rest;
        if (c0 >= '1' && c0 <= '8')
        {
            n.lane = c0 - '0';
            rest = p.Substring(1);
            int sh = rest.IndexOfAny("-^<>vpqszVw".ToCharArray());
            string headMods = sh >= 0 ? rest.Substring(0, sh) : rest;
            if (headMods.Contains("m")) return; // mine
            if (sh >= 0)
            {
                foreach (string body in rest.Substring(sh).Split('*'))
                {
                    if (body.Length == 0 || body.Contains("m")) continue;
                    var sl = SimaiSlides.Build(body, n.lane, t, bpm);
                    notes.Add(new Note
                    {
                        ms = n.ms, lane = n.lane, type = 5, durMs = Ms(sl.durSec), flags = body.Contains("b") ? 1 : 0,
                        extra = Ms(sl.startSec) + " " + Ms(sl.judgeSec) + " " + Ms(sl.staySec) + " " + SimaiSlides.Encode(sl)
                    });
                }
                if (headMods.Contains("?") || headMods.Contains("!")) return; // headless slide
                n.type = 3;
            }
            else if (rest.Contains("h")) { n.type = 2; n.durMs = HoldMs(rest, bpm); }
            else if (rest.Contains("$")) n.type = 3;
            if (headMods.Contains("b")) { n.flags |= 1; if (n.type == 0) n.type = 1; }
            if (headMods.Contains("x")) n.flags |= 2;
        }
        else if (c0 >= 'A' && c0 <= 'E')
        {
            int num = p.Length > 1 && char.IsDigit(p[1]) ? p[1] - '0' : 0;
            n.lane = (c0 - 'A') * 10 + num;
            n.type = 4;
            rest = p.Substring(num > 0 ? 2 : 1);
            if (rest.Contains("h")) n.durMs = HoldMs(rest, bpm);
            if (rest.Contains("f")) n.flags |= 4;
        }
        else return; // unknown token, skip
        notes.Add(n);
    }

    // [a:b] = b notes of 1/a; [#s] = s seconds; [bpm#a:b] = custom bpm
    static int HoldMs(string s, double bpm)
    {
        int o = s.IndexOf('['), e = s.IndexOf(']');
        if (o < 0 || e < o) return 0;
        string inner = s.Substring(o + 1, e - o - 1);
        int hash = inner.IndexOf('#');
        if (hash >= 0)
        {
            string after = inner.Substring(hash + 1);
            if (!after.Contains(":")) return (int)Math.Round(double.Parse(after, Inv) * 1000);
            if (hash > 0) bpm = double.Parse(inner.Substring(0, hash), Inv);
            inner = after;
        }
        string[] ab = inner.Split(':');
        return (int)Math.Round(240.0 / bpm / double.Parse(ab[0], Inv) * double.Parse(ab[1], Inv) * 1000);
    }

    static int Ms(double sec) => (int)Math.Round(sec * 1000);

    public static string ToText(List<Note> notes)
    {
        var sb = new StringBuilder();
        foreach (var n in notes) sb.Append(n.ms).Append(' ').Append(n.lane).Append(' ').Append(n.type).Append(' ').Append(n.durMs).Append(' ').Append(n.flags).Append(n.extra != null ? " " + n.extra : "").Append('\n');
        return sb.ToString();
    }
}
