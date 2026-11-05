// MaiGame: song select, synced start, MajdataPlay-rule judging, drawing into the cabinet screen render.
// Lives on the Start button (Interact = start / abort for desktop).
//
// Menu (when not playing), arcade style on the ring buttons or keys 1-8:
//   song select: 2/3 next song   6/7 prev song   1 harder   8 easier   4/5 -> settings
//   settings:    1/8 row up/down   2/3 value +   6/7 value -   4 start   5 back
//   speed / offset are per player and local (not synced, not saved: reset each visit).
//
// Judging follows MajdataPlay (NoteDrop / TapDrop / HoldDrop / TouchDrop / TouchHoldDrop / NoteLongDrop):
//   tap:   CP <=1f, P <=3f, GR <=6f, GD <=9f (both sides)               f = 1/60 s
//   touch: early only within 9f (always CP); late CP <=9f, P <=12f, GR <=15f, GD <=18f
//   hold:  head judged like tap/touch; release time counted inside [head+6f, end-12f] (touch hold: +15f / -12f),
//          2f forgiveness per release; held share caps the grade (100% / >=67% / >=33% / >=5%).
//   one click judges one note, oldest pending first (MajdataPlay's per-area queue).
//   slide: MajdataPlay SlideDrop/WifiDrop. Baked judge queues of sensor areas (SimaiSlides); an area is done when touched
//          then left (last area: touched); the next area may be checked early if the current one is skippable or on.
//          Checkable from head-50ms. Judged when every queue is empty, against judgeMs (= star entering the last area,
//          re-measured on this cabinet's sensor grid in FitJudge):
//          CP <= 14f + min(stay/4, 22f), GR <= 29f, else GD. Too late (end + 36f): 1 area left = GOOD, else MISS.
// ponytail: touch groups (>50% judged -> rest copy), DX score weighting (stage 4) not yet.
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using TMPro;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class MaiGame : UdonSharpBehaviour
{
    [Header("Songs (filled by Maimai/Rebuild Song List)")]
    public string[] songTitles;
    public string[] songInfo;        // "artist  /  chart: designer"
    public AudioClip[] songClips;
    public TextAsset[] charts;       // index song*7 + level(0..6); null = no chart at that level
    public string[] levelNames;      // same indexing, e.g. "13+"
    public AudioSource music;

    [Header("Screen (filled by Maimai/Setup Screen + Sensors / Stage 2)")]
    public Transform screen;         // cabinet screen frame (input space); local XY = screen plane, -Z faces player
    public Transform[] pool;         // lane-note quads (tap / star / hold head), children of Playfield
    public Transform[] bars;         // hold-body quads
    public Transform[] touchPool;    // touch-note quads
    public Material[] noteMats;      // kind*3 + variant; kind 0 tap,1 star,2 hold head,3 hold bar,4 touch; variant 0 normal,1 each,2 break
    public float[] sensX, sensY;     // 34 sensor centroids, playfield-local
    public TextMeshPro hud;          // top sub-screen: song info, live score, results (spectators read this)
    public TextMeshPro center;       // main screen centre: combo/judgement in play, title/difficulty in menu
    public GameObject menuLabels;    // button hints around the ring, menu only
    public TextMeshPro[] hintTexts;  // the 8 hints, index = lane-1 (text changes per menu page)
    public TextMeshPro bigText;      // main screen dot-matrix number: combo / level / setting value
    public Transform centerT, bigT;  // their transforms (TMP .transform is not exposed to Udon)
    public TextMeshPro hudBigLabel, hudBig, hudStats; // top screen cards 02 (achievement) and 03 (judge counts)
    public Material ringMat;         // dotted judge ring, _Progress = song progress
    public float radius = 0.35f;     // judge ring radius (screen-local units)
    public float approach = 0.52f;   // seconds a lane note is visible (grow at spawn + travel to ring)
    float noteV = 2.43f;             // travel speed in judge-ring radii per second
    public float touchApproach = 0.6f; // seconds a touch note's marker shrinks in
    public float noteSize = 0.09f;

    [Header("Slides (filled by Maimai/Setup Slides (Stage 3))")]
    public Transform[] slideArrows;  // slot * arrowsPerSlot chevrons
    public Transform[] slideStars;   // slot * 3 (wifi has three stars)
    public int arrowsPerSlot = 40;

    [Header("Input")]
    public Transform[] balls;        // hand-ball pickups (avatar agnostic input)
    public BoxCollider[] buttons;    // cabinet outer buttons, index = lane-1
    public float buttonMargin = 0.02f; // metres of slop around each button box (~ball radius)
    public GameObject[] buttonLights; // glow discs on the model's Button Light anchors, index = lane-1
    public long[] sensorGrid;        // baked touch-sensor lookup, gridN x gridN over screen-local [-gridHalf, gridHalf]
    public int gridN;
    public float gridHalf;
    public GameObject[] sensorFx;    // 34 sensor glow shapes, bit order: A1-8 0-7, B1-8 8-15, C1 16, C2 17, D1-8 18-25, E1-8 26-33

    [Header("Calibration")]
    public float offsetMs = 0f;      // + = notes later. Tune per headset/audio path. Player-adjustable in settings.
    public float speed = 7f;         // maimai-style note speed 1-10; approach = 5.6 / speed s (7 -> 0.8 s)
    public float touchDepth = 0.06f; // how close to the screen plane a ball counts as touching
    public float leadInSec = 3f;

    [UdonSynced] int startServerMs;
    [UdonSynced] bool playing;
    [UdonSynced] int song, level = 4;
    [UdonSynced] int sCombo, sCrit, sPerf, sGreat, sGood, sMiss; // owner's result, for spectators
    [UdonSynced] int sLost, sLostB, sDxLost, sMaxCombo;

    const float F = 1f / 60f; // MajdataPlay frame
    string[] diffNames = { "EASY", "BASIC", "ADVANCED", "EXPERT", "MASTER", "Re:MASTER", "UTAGE" };
    string[] gradeNames = { "CRITICAL", "PERFECT", "GREAT", "GOOD", "MISS" };
    string[] gradeCols = { "#FFE36BFF", "#FFA640FF", "#FF7AD0FF", "#6BE37AFF", "#9A9A9AFF" };
    string[] diffCols = { "#5FC4FF", "#55D23A", "#FFBA01", "#FF6B6B", "#B865F0", "#E3C2FF", "#FF6FFD" };
    float judgeAt, lastRing = -1f;
    int menuPage, setRow;            // 0 song select, 1 settings (local)
    string[] hints0 = { "LV +", "NEXT", "NEXT", "OK", "OK", "PREV", "PREV", "LV -" };
    string[] hints1 = { "UP", "+", "+", "START", "BACK", "-", "-", "DOWN" };

    // chart (per note)
    int[] nMs, nLane, nType, nDur, nFlags, nSens;
    long[] nMask;
    bool[] nEach;
    // run state (per note): 0 pending, 1 active hold, 2 done
    int[] nState, nGrade;
    float[] nErr, nReleased, nWait;
    bool[] nReleasing;
    int[] act = new int[64];
    int actCount;
    int count, head, drawHead, lastNotes, lastBars, lastTouch;
    float endSec;
    int localStart = int.MinValue, loadedSong = -1, loadedLevel = -1;
    int crit, perf, great, good, miss, combo, maxCombo;
    // MajdataPlay/DX scoring, "dropping" style: start at max, subtract what each judgement loses
    int lost, lostB, dxLost;          // base score lost, break bonus lost, DX score lost
    int maxBase, maxBonus, dxMax;     // per chart
    string lastJudge = "";
    int[] handLane;
    VRC_Pickup[] ballPickups;
    float lastRaw = -1f, lastRawReal, lastSeek;
    bool audioStarted;
    float nextSync;
    Renderer[] poolR, barR, touchR;
    long sensorMask;
    float lastHoldT = -1f;
    int buttonMask;

    // slides (per slide; paths/queues per slide*3+k; groups = judge areas)
    int sCount, slideHead, slideDrawHead;
    int[] sHead, sStart, sDur, sJudge, sStay, sFlags, sPathN, sQN, sState, sSlot;
    bool[] sEach;
    float[] sHid;
    int[] pFirst, pLen, qFirst, qLen, qPos;
    float[] ptX, ptY, gOn, gFin;
    int[] gFlags, gState;
    long[] gMaskA, gMaskB;
    int[] slotSlide, slotArrowN;
    float[] slotHid, arrowFrac;
    Renderer[] arrowR, starR;

    void Start()
    {
        poolR = Renderers(pool);
        barR = Renderers(bars);
        touchR = Renderers(touchPool);
        InitSlots();
        handLane = new int[balls.Length];
        ballPickups = new VRC_Pickup[balls.Length];
        for (int i = 0; i < balls.Length; i++) ballPickups[i] = (VRC_Pickup)balls[i].GetComponent(typeof(VRC_Pickup));
        if (songTitles.Length == 0) { hud.text = "No songs - run Maimai/Rebuild Song List"; return; }
        if (center == null) center = hud;
        ApplySpeed();
        FixLevel(1);
        Select();
        UpdateHud();
    }

    Renderer[] Renderers(Transform[] ts)
    {
        var r = new Renderer[ts.Length];
        for (int i = 0; i < ts.Length; i++) r[i] = ts[i].GetComponent<Renderer>();
        return r;
    }

    // Called by MaiBall when someone else grabs a ball.
    // ponytail: the new owner's counters start from zero; carry them over via synced values if it matters.
    public void TakeOver()
    {
        Networking.SetOwner(Networking.LocalPlayer, gameObject);
        UpdateHud();
    }

    // ---------- song select ----------

    void Select()
    {
        loadedSong = song;
        loadedLevel = level;
        music.Stop();
        music.clip = songClips[song];
        LoadChart(charts[song * 7 + level]);
    }

    // keep level on a difficulty this song actually has, searching in dir (+1 harder / -1 easier)
    void FixLevel(int dir)
    {
        for (int k = 0; k < 7; k++)
        {
            if (charts[song * 7 + level] != null) return;
            level = (level + dir + 7) % 7;
        }
    }

    void ApplySpeed()
    {
        // MajdataPlay speed curve (units/s, judge ring = 4.8 units)
        noteV = 107.25f / (71.4184491f * Mathf.Pow(speed + 0.9975f, -0.985558604f)) / 4.8f;
        approach = 1.265625f / noteV; // from scale 0 (-1.275 units) to the ring
        touchApproach = 4.2f / speed;
    }

    void SettingsPress(int lane)
    {
        if (lane == 4) { menuPage = 0; StartSong(); return; }
        if (lane == 5) menuPage = 0;
        else if (lane == 1 || lane == 8) setRow = 1 - setRow; // ponytail: 2 rows, up == down
        else
        {
            int d = lane == 2 || lane == 3 ? 1 : -1;
            if (setRow == 0) { speed = Mathf.Clamp(speed + 0.5f * d, 1f, 10f); ApplySpeed(); }
            else offsetMs = Mathf.Clamp(offsetMs + 5f * d, -500f, 500f);
        }
        UpdateHud();
    }

    void MenuPress(int lane)
    {
        if (menuPage == 1) { SettingsPress(lane); return; }
        int n = songTitles.Length;
        if (lane == 2 || lane == 3) { song = (song + 1) % n; FixLevel(1); }
        else if (lane == 6 || lane == 7) { song = (song + n - 1) % n; FixLevel(1); }
        else if (lane == 1) { level = (level + 1) % 7; FixLevel(1); }
        else if (lane == 8) { level = (level + 6) % 7; FixLevel(-1); }
        else { menuPage = 1; UpdateHud(); return; }
        lastJudge = "";
        Select();
        RequestSerialization();
        UpdateHud();
    }

    void LoadChart(TextAsset chart)
    {
        string[] lines = chart.text.Split('\n');
        int n = lines.Length;
        nMs = new int[n]; nLane = new int[n]; nType = new int[n]; nDur = new int[n]; nFlags = new int[n]; nSens = new int[n];
        nMask = new long[n]; nEach = new bool[n];
        nState = new int[n]; nGrade = new int[n]; nErr = new float[n]; nReleased = new float[n]; nWait = new float[n]; nReleasing = new bool[n];
        count = 0;
        float last = 0f;
        for (int i = 0; i < n; i++)
        {
            string[] f = lines[i].Trim().Split(' ');
            if (f.Length < 4 || f[2] == "5") continue;
            int c = count;
            nMs[c] = int.Parse(f[0]);
            nLane[c] = int.Parse(f[1]);
            nType[c] = int.Parse(f[2]);
            nDur[c] = int.Parse(f[3]);
            nFlags[c] = f.Length > 4 ? int.Parse(f[4]) : (nType[c] == 1 ? 1 : 0);
            if (nType[c] == 4)
            {
                int letter = nLane[c] / 10, num = nLane[c] % 10;
                int k = letter == 0 ? num - 1 : letter == 1 ? 7 + num : letter == 2 ? 16 : letter == 3 ? 17 + num : 25 + num;
                nSens[c] = k;
                nMask[c] = letter == 2 ? (3L << 16) : (1L << k);
            }
            else nMask[c] = 1L << (nLane[c] - 1);
            last = Mathf.Max(last, (nMs[c] + nDur[c]) / 1000f);
            count++;
        }
        // each: two or more notes of the same family (lane / touch) on the same timestamp
        for (int i = 0; i < count; i++)
            for (int j = i + 1; j < count && nMs[j] == nMs[i]; j++)
                if ((nType[i] == 4) == (nType[j] == 4)) { nEach[i] = true; nEach[j] = true; }
        last = Mathf.Max(last, LoadSlides(lines));
        endSec = last > 0f ? last + 3f : 5f;
        maxBase = 0; maxBonus = 0; dxMax = 3 * (count + sCount);
        for (int i = 0; i < count; i++) { maxBase += NoteBase(i); if ((nFlags[i] & 1) != 0) maxBonus += 100; }
        for (int i = 0; i < sCount; i++) { maxBase += SlideBase(i); if ((sFlags[i] & 1) != 0) maxBonus += 100; }
    }

    // base score: tap/touch 500, hold 1000, slide 1500, any break 2500
    int NoteBase(int i) { return (nFlags[i] & 1) != 0 ? 2500 : nType[i] == 2 || (nType[i] == 4 && nDur[i] > 0) ? 1000 : 500; }
    int SlideBase(int s) { return (sFlags[s] & 1) != 0 ? 2500 : 1500;
    }

    long AreaMask(int code) { return code < 16 ? 1L << code : code == 16 ? 3L << 16 : 1L << (code + 1); }

    // type-5 lines: "headMs lane 5 durMs flags startMs judgeMs stayMs x,y;x,y/... on,fin,flags,area[,area];.../..."
    float LoadSlides(string[] lines)
    {
        FreeAllSlots();
        int ns = 0, np = 0, ng = 0;
        for (int i = 0; i < lines.Length; i++)
        {
            string[] f = lines[i].Trim().Split(' ');
            if (f.Length < 10 || f[2] != "5") continue;
            ns++;
            string[] ps = f[8].Split('/');
            for (int k = 0; k < ps.Length; k++) np += ps[k].Split(';').Length;
            string[] qs = f[9].Split('/');
            for (int k = 0; k < qs.Length; k++) ng += qs[k].Split(';').Length;
        }
        sHead = new int[ns]; sStart = new int[ns]; sDur = new int[ns]; sJudge = new int[ns]; sStay = new int[ns]; sFlags = new int[ns];
        sPathN = new int[ns]; sQN = new int[ns]; sState = new int[ns]; sSlot = new int[ns]; sEach = new bool[ns]; sHid = new float[ns];
        pFirst = new int[ns * 3]; pLen = new int[ns * 3]; qFirst = new int[ns * 3]; qLen = new int[ns * 3]; qPos = new int[ns * 3];
        ptX = new float[np]; ptY = new float[np];
        gOn = new float[ng]; gFin = new float[ng]; gFlags = new int[ng]; gState = new int[ng]; gMaskA = new long[ng]; gMaskB = new long[ng];
        int s = 0, p = 0, g = 0;
        float last = 0f;
        for (int i = 0; i < lines.Length; i++)
        {
            string[] f = lines[i].Trim().Split(' ');
            if (f.Length < 10 || f[2] != "5") continue;
            sHead[s] = int.Parse(f[0]); sDur[s] = int.Parse(f[3]); sFlags[s] = int.Parse(f[4]);
            sStart[s] = int.Parse(f[5]); sJudge[s] = int.Parse(f[6]); sStay[s] = int.Parse(f[7]);
            sSlot[s] = -1;
            string[] ps = f[8].Split('/');
            sPathN[s] = Mathf.Min(3, ps.Length);
            for (int k = 0; k < sPathN[s]; k++)
            {
                string[] pts = ps[k].Split(';');
                pFirst[s * 3 + k] = p; pLen[s * 3 + k] = pts.Length;
                for (int j = 0; j < pts.Length; j++)
                {
                    string[] xy = pts[j].Split(',');
                    ptX[p] = int.Parse(xy[0]) / 1000f; ptY[p] = int.Parse(xy[1]) / 1000f;
                    p++;
                }
            }
            string[] qs = f[9].Split('/');
            sQN[s] = Mathf.Min(3, qs.Length);
            for (int k = 0; k < sQN[s]; k++)
            {
                string[] gs = qs[k].Split(';');
                qFirst[s * 3 + k] = g; qLen[s * 3 + k] = gs.Length;
                for (int j = 0; j < gs.Length; j++)
                {
                    string[] gf = gs[j].Split(',');
                    gOn[g] = int.Parse(gf[0]) / 1000f; gFin[g] = int.Parse(gf[1]) / 1000f; gFlags[g] = int.Parse(gf[2]);
                    gMaskA[g] = AreaMask(int.Parse(gf[3]));
                    gMaskB[g] = gf.Length > 4 ? AreaMask(int.Parse(gf[4])) : 0L;
                    g++;
                }
            }
            FitJudge(s);
            last = Mathf.Max(last, (sStart[s] + sDur[s]) / 1000f);
            s++;
        }
        sCount = ns;
        for (int i = 0; i < ns; i++)
            for (int j = i + 1; j < ns && sHead[j] == sHead[i]; j++) { sEach[i] = true; sEach[j] = true; }
        return last;
    }

    // MajdataPlay's judge point is "star enters the last area" (its Const is measured on its own path art with a fingertip).
    // Re-measure that moment on this cabinet's sensor grid with a ball centred on the star; keeps long slides from judging early.
    void FitJudge(int s)
    {
        if (sensorGrid == null || sensorGrid.Length == 0) return;
        float best = 0f;
        for (int k = 0; k < sQN[s]; k++)
        {
            int pk = k < sPathN[s] ? k : 0;
            int n = pLen[s * 3 + pk], f0 = pFirst[s * 3 + pk];
            int g = qFirst[s * 3 + k] + qLen[s * 3 + k] - 1;
            long m = gMaskA[g] | gMaskB[g];
            if (!OnPath(f0, n, n - 1, m)) continue;
            int i = n - 1;
            while (i > 0 && OnPath(f0, n, i - 1, m)) i--;
            float lo = i - 1, hi = i;
            if (i > 0)
                for (int b = 0; b < 6; b++) { float mid = (lo + hi) * 0.5f; if (OnPath(f0, n, mid, m)) hi = mid; else lo = mid; }
            best = Mathf.Max(best, hi / (n - 1));
        }
        if (best <= 0f) return;
        sJudge[s] = sStart[s] + Mathf.RoundToInt(sDur[s] * best);
        sStay[s] = sStart[s] + sDur[s] - sJudge[s];
    }

    bool OnPath(int f0, int n, float idx, long mask)
    {
        int i0 = Mathf.Min(n - 2, (int)idx);
        float fr = idx - i0;
        float x = Mathf.Lerp(ptX[f0 + i0], ptX[f0 + i0 + 1], fr) * radius, y = Mathf.Lerp(ptY[f0 + i0], ptY[f0 + i0 + 1], fr) * radius;
        return (SensorAt(x, y) & mask) != 0;
    }

    void ResetSlides()
    {
        FreeAllSlots();
        slideHead = 0; slideDrawHead = 0;
        for (int i = 0; i < sCount; i++) { sState[i] = 0; sHid[i] = 0f; sSlot[i] = -1; }
        for (int i = 0; i < qPos.Length; i++) qPos[i] = 0;
        for (int i = 0; i < gState.Length; i++) gState[i] = 0;
    }

    // ---------- start / stop / sync ----------

    public override void Interact()
    {
        Networking.SetOwner(Networking.LocalPlayer, gameObject);
        if (playing) EndSong(); else StartSong();
    }

    void StartSong()
    {
        startServerMs = Networking.GetServerTimeInMilliseconds() + (int)(leadInSec * 1000);
        playing = true;
        sCombo = sCrit = sPerf = sGreat = sGood = sMiss = sLost = sLostB = sDxLost = sMaxCombo = 0;
        RequestSerialization();
        ResetRun();
    }

    void EndSong()
    {
        playing = false;
        // stopped early: whatever wasn't judged counts as MISS
        for (int i = 0; i < count; i++) if (nState[i] != 2) { nState[i] = 2; Count(4, NoteBase(i), (nFlags[i] & 1) != 0); }
        for (int i = 0; i < sCount; i++) if (sState[i] != 2) { sState[i] = 2; Count(4, SlideBase(i), (sFlags[i] & 1) != 0); }
        actCount = 0;
        Snap();
        RequestSerialization();
        StopLocal();
    }

    void StopLocal()
    {
        music.Stop();
        HideFrom(pool, 0, lastNotes); lastNotes = 0;
        HideFrom(bars, 0, lastBars); lastBars = 0;
        HideFrom(touchPool, 0, lastTouch); lastTouch = 0;
        FreeAllSlots();
        UpdateRing(0f);
        UpdateHud();
    }

    void HideFrom(Transform[] p, int from, int to)
    {
        for (int i = from; i < to; i++) p[i].gameObject.SetActive(false);
    }

    public override void OnDeserialization()
    {
        if (song != loadedSong || level != loadedLevel) Select();
        if (playing && startServerMs != localStart) ResetRun();
        if (!playing && music.isPlaying) StopLocal();
        if (!Networking.IsOwner(gameObject)) UpdateHud();
    }

    void ResetRun()
    {
        localStart = startServerMs;
        head = 0; drawHead = 0; actCount = 0; lastHoldT = -1f;
        crit = perf = great = good = miss = combo = maxCombo = lost = lostB = dxLost = 0;
        lastJudge = "";
        for (int i = 0; i < count; i++) { nState[i] = 0; nReleased[i] = 0; nWait[i] = 0; nReleasing[i] = false; }
        ResetSlides();
        music.Stop();
        lastRaw = -1f;
        audioStarted = false;
        UpdateHud();
    }

    // ---------- clock ----------
    // Server time gets everyone to the same start; once audio runs, the audio position is the clock.
    // timeSamples advances in DSP-buffer steps (~10-20ms), so interpolate with realtime between steps.
    // ponytail: AudioSource clock only. Video-player mode = swap music.timeSamples for videoPlayer.GetTime().
    float SongTime()
    {
        float server = (Networking.GetServerTimeInMilliseconds() - startServerMs) / 1000f;
        if (!music.isPlaying)
        {
            if (!audioStarted && server >= 0f && server < music.clip.length)
            {
                music.time = server;
                music.Play();
                audioStarted = true;
            }
            return server;
        }
        float raw = music.timeSamples / (float)music.clip.frequency;
        float now = Time.realtimeSinceStartup;
        if (raw != lastRaw) { lastRaw = raw; lastRawReal = now; }
        float t = raw + (now - lastRawReal);
        // drift guard: if audio wandered from shared time (hitch, late join), snap it back
        if (Mathf.Abs(t - server) > 0.1f)
        {
            if (now - lastSeek > 1f) { music.time = server; lastSeek = now; lastRaw = -1f; }
            return server;
        }
        return t;
    }

    // ---------- loop ----------

    void Update()
    {
        bool owner = Networking.IsOwner(gameObject);
        if (!playing)
        {
            if (owner && songTitles.Length > 0) ReadInput(0f);
            return;
        }
        if (startServerMs != localStart) return;
        float t = SongTime() - offsetMs / 1000f;

        if (owner)
        {
            ReadInput(t);
            MissLateNotes(t);
            UpdateHolds(t);
            UpdateSlides(t);
        }
        Draw(t, owner);
        DrawSlides(t, owner);
        if (lastJudge != "" && Time.time - judgeAt > 0.6f) { lastJudge = ""; UpdateHud(); }
        UpdateRing(t);

        if (owner && t > Mathf.Min(endSec, music.clip.length + 0.5f)) { EndSong(); return; }

        if (owner && Time.time > nextSync)
        {
            nextSync = Time.time + 0.5f;
            Snap();
            RequestSerialization();
        }
    }

    // pending notes past their late window: taps -> MISS; holds -> head MISS but body still counts
    void MissLateNotes(float t)
    {
        while (head < count && nState[head] != 0) head++;
        for (int i = head; i < count; i++)
        {
            float late = t - nMs[i] / 1000f;
            if (late <= 9 * F) break;
            if (nState[i] != 0) continue;
            bool touch = nType[i] == 4;
            if (late <= (touch ? 18 * F : 9 * F)) continue;
            if (nType[i] == 2 || (touch && nDur[i] > 0)) StartHold(i, 4, -999f);
            else Finish(i, 4, -999f);
        }
    }

    void StartHold(int i, int grade, float err)
    {
        nState[i] = 1;
        nGrade[i] = grade;
        nErr[i] = err;
        if (actCount < act.Length) act[actCount++] = i;
        else Finish(i, grade, err); // ponytail: >64 simultaneous holds can't happen in real charts
    }

    // MajdataPlay HoldDrop body check + NoteLongDrop.HoldEndJudge
    void UpdateHolds(float t)
    {
        float dt = lastHoldT < 0f ? 0f : t - lastHoldT; // song-clock delta, like MajTimeline.DeltaTime
        lastHoldT = t;
        int laneHeld = buttonMask | (int)(sensorMask & 0xFF);
        for (int a = actCount - 1; a >= 0; a--)
        {
            int i = act[a];
            bool touch = nType[i] == 4;
            float start = nMs[i] / 1000f, len = nDur[i] / 1000f, end = start + len;
            float ignore = (touch ? 15 * F : 6 * F) + 12 * F;
            bool pressed = touch ? (sensorMask & nMask[i]) != 0 : ((laneHeld >> (nLane[i] - 1)) & 1) != 0;
            if (t >= start + (touch ? 15 * F : 6 * F) && t <= end - 12 * F)
            {
                if (pressed) { nWait[i] = 0; nReleasing[i] = false; }
                else if (!nReleasing[i] && nWait[i] <= 2 * F) nWait[i] += dt;
                else { nReleased[i] += nWait[i] + dt; nWait[i] = 0; nReleasing[i] = true; }
            }
            if (t < end) continue;
            float reality = Mathf.Clamp(len - ignore, 0f, len - 0.3f);
            int g = nGrade[i];
            if (reality > 0f) g = HoldEnd(g, Mathf.Clamp01((reality - nReleased[i]) / reality));
            act[a] = act[--actCount];
            Finish(i, g, nErr[i]);
        }
    }

    // coarse version of MajdataPlay's table (grades 0 CP, 1 P, 2 GR, 3 GD, 4 MISS)
    int HoldEnd(int head, float pct)
    {
        if (pct >= 1f) return head <= 1 ? head : head <= 3 ? 2 : 3;
        if (pct >= 0.67f) return head <= 1 ? 1 : head <= 3 ? 2 : 3;
        if (pct >= 0.33f) return head <= 3 ? 2 : 3;
        if (pct >= 0.05f) return 3;
        return head <= 3 ? 3 : 4;
    }

    // ---------- slides: judging (owner) ----------

    void UpdateSlides(float t)
    {
        while (slideHead < sCount && sState[slideHead] != 0) slideHead++;
        for (int s = slideHead; s < sCount; s++)
        {
            if (t < sHead[s] / 1000f - 0.05f) break;
            if (sState[s] != 0) continue;
            int rem = 0;
            for (int k = 0; k < sQN[s]; k++)
            {
                SlideQueue(s, s * 3 + k);
                rem += qLen[s * 3 + k] - qPos[s * 3 + k];
            }
            if (rem == 0) JudgeSlide(s, t);
            else if (t > (sStart[s] + sDur[s]) / 1000f + 36 * F)
                FinishSlide(s, rem == 1 ? 3 : 4, rem == 1 ? t - sJudge[s] / 1000f : -999f);
        }
    }

    // SlideDrop.SensorCheck
    void SlideQueue(int s, int qi)
    {
        while (qPos[qi] < qLen[qi])
        {
            int g = qFirst[qi] + qPos[qi];
            AreaCheck(g);
            if (qPos[qi] + 1 < qLen[qi] && ((gFlags[g] & 1) != 0 || AreaOn(g)))
            {
                int g2 = g + 1;
                AreaCheck(g2);
                if (AreaDone(g2)) { Hide(s, gFin[g2]); qPos[qi] += 2; continue; }
                if (AreaOn(g2)) { Hide(s, gOn[g2]); qPos[qi] += 1; continue; }
            }
            if (AreaDone(g)) { Hide(s, gFin[g]); qPos[qi]++; }
            else if (AreaOn(g)) Hide(s, gOn[g]);
            return;
        }
    }

    // SlideArea.Check, OR policy over up to two areas. bits: 1 A on, 2 A off-after-on, 4 B on, 8 B off-after-on
    void AreaCheck(int g)
    {
        int st = gState[g];
        if ((sensorMask & gMaskA[g]) != 0) st |= 1; else if ((st & 1) != 0) st |= 2;
        if (gMaskB[g] != 0) { if ((sensorMask & gMaskB[g]) != 0) st |= 4; else if ((st & 4) != 0) st |= 8; }
        gState[g] = st;
    }
    bool AreaOn(int g) { return (gState[g] & 5) != 0; }
    bool AreaDone(int g) { return (gFlags[g] & 2) != 0 ? (gState[g] & 5) != 0 : (gState[g] & 10) != 0; }
    void Hide(int s, float frac) { if (frac > sHid[s]) sHid[s] = frac; }

    // SlideBase.Judge (modern); perfect segments collapse to CRITICAL like JudgeGradeCorrection
    void JudgeSlide(int s, float t)
    {
        float diff = t - sJudge[s] / 1000f;
        float p3 = 14 * F + Mathf.Min(sStay[s] / 4000f, 22 * F);
        float a = Mathf.Abs(diff);
        FinishSlide(s, a <= p3 ? 0 : a <= 29 * F ? 2 : 3, diff);
    }

    void FinishSlide(int s, int g, float err)
    {
        sState[s] = 2;
        sHid[s] = 1f;
        Count(g, SlideBase(s), (sFlags[s] & 1) != 0);
        ShowJudge(g, err, "SLIDE ", true);
    }

    // ---------- slides: drawing (all clients). Arrows are placed once per slot, then only hidden as progress moves. ----------

    void InitSlots()
    {
        if (slideArrows == null || slideStars == null || arrowsPerSlot <= 0) return;
        arrowR = Renderers(slideArrows);
        starR = Renderers(slideStars);
        int slots = Mathf.Min(slideArrows.Length / arrowsPerSlot, slideStars.Length / 3);
        slotSlide = new int[slots]; slotArrowN = new int[slots]; slotHid = new float[slots];
        arrowFrac = new float[slideArrows.Length];
        for (int k = 0; k < slots; k++) slotSlide[k] = -1;
    }

    bool SlideGone(int s, float t, bool owner)
    {
        return owner ? sState[s] == 2 : t > (sStart[s] + sDur[s]) / 1000f + 0.1f;
    }

    void DrawSlides(float t, bool owner)
    {
        if (slotSlide == null) return;
        int slots = slotSlide.Length;
        for (int k = 0; k < slots; k++)
            if (slotSlide[k] >= 0 && SlideGone(slotSlide[k], t, owner)) FreeSlot(k);
        while (slideDrawHead < sCount && (sStart[slideDrawHead] + sDur[slideDrawHead]) / 1000f < t - 1f) slideDrawHead++;
        for (int s = slideDrawHead; s < sCount; s++)
        {
            if (sHead[s] / 1000f - approach > t) break;
            if (sSlot[s] >= 0 || SlideGone(s, t, owner)) continue;
            int k = 0;
            while (k < slots && slotSlide[k] >= 0) k++;
            if (k == slots) break; // ponytail: more than `slots` slides on screen -> extras judged but not drawn
            UseSlot(k, s);
        }
        float spin = t * -160f;
        for (int k = 0; k < slots; k++)
        {
            int s = slotSlide[k];
            if (s < 0) continue;
            float st = sStart[s] / 1000f, head = sHead[s] / 1000f;
            float u = Mathf.Clamp01((t - st) / Mathf.Max(0.001f, sDur[s] / 1000f));
            float hid = owner ? sHid[s] : u;
            if (hid != slotHid[k])
            {
                slotHid[k] = hid;
                int b = k * arrowsPerSlot;
                for (int j = 0; j < slotArrowN[k]; j++)
                {
                    bool on = arrowFrac[b + j] > hid;
                    if (slideArrows[b + j].gameObject.activeSelf != on) slideArrows[b + j].gameObject.SetActive(on);
                }
            }
            float alpha = st > head ? Mathf.Clamp01((t - head) / (st - head)) : 1f;
            for (int p = 0; p < sPathN[s]; p++)
            {
                Transform star = slideStars[k * 3 + p];
                if (t < head) { if (star.gameObject.activeSelf) star.gameObject.SetActive(false); continue; }
                int n = pLen[s * 3 + p], f0 = pFirst[s * 3 + p];
                float x = u * (n - 1);
                int i0 = Mathf.Min(n - 2, (int)x);
                float fr = x - i0;
                star.localPosition = new Vector3(Mathf.Lerp(ptX[f0 + i0], ptX[f0 + i0 + 1], fr) * radius, Mathf.Lerp(ptY[f0 + i0], ptY[f0 + i0 + 1], fr) * radius, -0.011f);
                float sz = noteSize * (0.6f + 0.5f * alpha);
                star.localScale = new Vector3(sz, sz, 1f);
                star.localRotation = Quaternion.Euler(0, 0, spin);
                if (!star.gameObject.activeSelf) star.gameObject.SetActive(true);
            }
        }
    }

    void UseSlot(int k, int s)
    {
        slotSlide[k] = s;
        sSlot[s] = k;
        slotHid[k] = -1f;
        int variant = (sFlags[s] & 1) != 0 ? 2 : sEach[s] ? 1 : 0;
        int b = k * arrowsPerSlot, a = 0;
        float asz = noteSize * 0.55f;
        for (int p = 0; p < sPathN[s]; p++)
        {
            int n = pLen[s * 3 + p], f0 = pFirst[s * 3 + p];
            for (int i = 1; i < n && a < arrowsPerSlot; i++)
            {
                Transform tr = slideArrows[b + a];
                float dx = ptX[f0 + i] - ptX[f0 + i - 1], dy = ptY[f0 + i] - ptY[f0 + i - 1];
                tr.localPosition = new Vector3(ptX[f0 + i] * radius, ptY[f0 + i] * radius, -0.009f);
                tr.localRotation = Quaternion.Euler(0, 0, -Mathf.Atan2(dx, dy) * Mathf.Rad2Deg);
                tr.localScale = new Vector3(asz, asz, 1f);
                arrowR[b + a].sharedMaterial = noteMats[15 + variant];
                arrowFrac[b + a] = i / (float)(n - 1);
                a++;
            }
            starR[k * 3 + p].sharedMaterial = noteMats[3 + variant];
        }
        slotArrowN[k] = a;
    }

    void FreeSlot(int k)
    {
        int b = k * arrowsPerSlot;
        for (int j = 0; j < slotArrowN[k]; j++) slideArrows[b + j].gameObject.SetActive(false);
        for (int p = 0; p < 3; p++) slideStars[k * 3 + p].gameObject.SetActive(false);
        int s = slotSlide[k];
        if (s >= 0 && sSlot != null && s < sSlot.Length) sSlot[s] = -1;
        slotSlide[k] = -1;
        slotArrowN[k] = 0;
    }

    void FreeAllSlots()
    {
        if (slotSlide == null) return;
        for (int k = 0; k < slotSlide.Length; k++) if (slotSlide[k] >= 0) FreeSlot(k);
    }

    // ---------- drawing (every client renders from the same chart + clock) ----------

    const float Spawn = 0.2552f; // 1.225 / 4.8: where taps appear (edge of the inner sensors)

    void Draw(float t, bool owner)
    {
        while (drawHead < count && (nMs[drawHead] + nDur[drawHead]) / 1000f < t - 0.2f) drawHead++;
        int ns = 0, bs = 0, ts = 0;
        float spin = t * -160f;
        for (int i = drawHead; i < count; i++)
        {
            float start = nMs[i] / 1000f;
            float dt = start - t;
            if (dt > approach) break;
            bool isHold = nDur[i] > 0;
            // owner hides judged notes; spectators hide notes once they pass the ring (holds once they end)
            if (owner ? nState[i] == 2 : (isHold ? t > start + nDur[i] / 1000f : dt < -0.05f)) continue;
            int variant = (nFlags[i] & 1) != 0 ? 2 : nEach[i] ? 1 : 0;

            if (nType[i] == 4)
            {
                if (ts >= touchPool.Length || dt > touchApproach) continue;
                int k = nSens[i];
                float x = nLane[i] / 10 == 2 ? 0f : sensX[k], y = nLane[i] / 10 == 2 ? 0f : sensY[k];
                float u = Mathf.Clamp01(dt / touchApproach);
                Transform tt = touchPool[ts];
                tt.localPosition = new Vector3(x, y, -0.012f);
                float s = noteSize * (1.1f + 1.4f * u);
                tt.localScale = new Vector3(s, s, 1f);
                touchR[ts].sharedMaterial = noteMats[12 + variant];
                if (ts >= lastTouch) tt.gameObject.SetActive(true);
                ts++;
                continue;
            }

            if (ns >= pool.Length) continue;
            float ang = (22.5f + 45f * (nLane[i] - 1)) * Mathf.Deg2Rad;
            float sx = Mathf.Sin(ang), sy = Mathf.Cos(ang);
            float d = 1f - dt * noteV;                // distance from centre in radii (1 = ring)
            float p = Mathf.Clamp(d, Spawn, 1f);
            int kind = nType[i] == 3 ? 1 : nType[i] == 2 ? 2 : 0;
            Transform tr = pool[ns];
            tr.localPosition = new Vector3(sx * radius * p, sy * radius * p, -0.01f);
            float sz = noteSize * (d < Spawn ? Mathf.Clamp01(d * 1.92f + 0.51f) : 1f);
            tr.localScale = new Vector3(sz, sz, 1f);
            tr.localRotation = kind == 1 ? Quaternion.Euler(0, 0, spin) : Quaternion.identity;
            poolR[ns].sharedMaterial = noteMats[kind * 3 + variant];
            if (ns >= lastNotes) tr.gameObject.SetActive(true);
            ns++;

            if (isHold && bs < bars.Length)
            {
                float pt = Mathf.Clamp(1f - (start + nDur[i] / 1000f - t) * noteV, Spawn, 1f);
                if (p - pt > 0.001f)
                {
                    Transform b = bars[bs];
                    float mid = (p + pt) * 0.5f * radius;
                    b.localPosition = new Vector3(sx * mid, sy * mid, -0.008f);
                    b.localRotation = Quaternion.Euler(0, 0, -(22.5f + 45f * (nLane[i] - 1)));
                    b.localScale = new Vector3(noteSize * 0.7f, (p - pt) * radius, 1f);
                    barR[bs].sharedMaterial = noteMats[9 + variant];
                    if (bs >= lastBars) b.gameObject.SetActive(true);
                    bs++;
                }
            }
        }
        HideFrom(pool, ns, lastNotes); lastNotes = ns;
        HideFrom(bars, bs, lastBars); lastBars = bs;
        HideFrom(touchPool, ts, lastTouch); lastTouch = ts;
    }

    // ---------- input ----------
    // One pass per frame: buttons (box test), touch sensors (baked grid lookup), keys.
    // Rising edges are clicks; held state drives holds, button lights and sensor glow.
    void ReadInput(float t)
    {
        int btn = 0;
        long sens = 0;
        for (int i = 0; i < balls.Length; i++)
        {
            if (!ballPickups[i].IsHeld) { handLane[i] = 0; continue; }
            Vector3 world = balls[i].position;
            int lane = ButtonLane(world);
            if (lane != 0)
            {
                btn |= 1 << (lane - 1);
                if (lane != handLane[i]) ButtonClick(lane, t, i);
            }
            handLane[i] = lane;
            Vector3 l = screen.InverseTransformPoint(world);
            if (Mathf.Abs(l.z) < touchDepth) sens |= SensorAt(l.x, l.y);
        }

        // desktop (Udon can't index KeyCode[])
        if (Input.GetKey(KeyCode.Alpha1)) { btn |= 1; if (Input.GetKeyDown(KeyCode.Alpha1)) ButtonClick(1, t, -1); }
        if (Input.GetKey(KeyCode.Alpha2)) { btn |= 2; if (Input.GetKeyDown(KeyCode.Alpha2)) ButtonClick(2, t, -1); }
        if (Input.GetKey(KeyCode.Alpha3)) { btn |= 4; if (Input.GetKeyDown(KeyCode.Alpha3)) ButtonClick(3, t, -1); }
        if (Input.GetKey(KeyCode.Alpha4)) { btn |= 8; if (Input.GetKeyDown(KeyCode.Alpha4)) ButtonClick(4, t, -1); }
        if (Input.GetKey(KeyCode.Alpha5)) { btn |= 16; if (Input.GetKeyDown(KeyCode.Alpha5)) ButtonClick(5, t, -1); }
        if (Input.GetKey(KeyCode.Alpha6)) { btn |= 32; if (Input.GetKeyDown(KeyCode.Alpha6)) ButtonClick(6, t, -1); }
        if (Input.GetKey(KeyCode.Alpha7)) { btn |= 64; if (Input.GetKeyDown(KeyCode.Alpha7)) ButtonClick(7, t, -1); }
        if (Input.GetKey(KeyCode.Alpha8)) { btn |= 128; if (Input.GetKeyDown(KeyCode.Alpha8)) ButtonClick(8, t, -1); }

        // sensor clicks: A zones hit lane notes first (arcade), any zone can hit a touch note
        long rise = sens & ~sensorMask;
        if (rise != 0)
            for (int k = 0; k < 34; k++)
            {
                if (((rise >> k) & 1L) == 0) continue;
                if (k < 8 && LaneClick(k + 1, t)) continue;
                if (playing) TouchClick(k, t);
            }

        if (sens != sensorMask && sensorFx != null)
            for (int k = 0; k < sensorFx.Length; k++)
            {
                bool on = ((sens >> k) & 1L) != 0;
                if (on != (((sensorMask >> k) & 1L) != 0)) sensorFx[k].SetActive(on);
            }
        sensorMask = sens;

        if (btn != buttonMask && buttonLights != null)
            for (int k = 0; k < buttonLights.Length; k++)
                buttonLights[k].SetActive(((btn >> k) & 1) != 0);
        buttonMask = btn;
    }

    void ButtonClick(int lane, float t, int hand)
    {
        if (hand >= 0) ballPickups[hand].PlayHaptics();
        if (!LaneClick(lane, t) && playing) TouchClick(lane - 1, t); // button ring can hit A-zone touch notes
    }

    // oldest pending lane note (tap/break/star/hold head) on this lane inside the tap window. Returns true if used.
    bool LaneClick(int lane, float t)
    {
        if (!playing) { MenuPress(lane); return true; }
        for (int i = head; i < count; i++)
        {
            float err = t - nMs[i] / 1000f;
            if (err < -9 * F) break;
            if (nState[i] != 0 || nType[i] == 4 || nLane[i] != lane || err > 9 * F) continue;
            float a = Mathf.Abs(err);
            int g = a <= F ? 0 : a <= 3 * F ? 1 : a <= 6 * F ? 2 : 3;
            if (nType[i] == 2) { StartHold(i, g, err); ShowJudge(g, err, "HOLD ", false); }
            else Finish(i, g, err);
            return true;
        }
        return false;
    }

    // oldest pending touch note covering sensor k inside the touch window (early 9f, late 18f)
    bool TouchClick(int k, float t)
    {
        for (int i = head; i < count; i++)
        {
            float err = t - nMs[i] / 1000f;
            if (err < -9 * F) break;
            if (nState[i] != 0 || nType[i] != 4 || ((nMask[i] >> k) & 1L) == 0 || err > 18 * F) continue;
            int g = err <= 9 * F ? 0 : err <= 12 * F ? 1 : err <= 15 * F ? 2 : 3;
            if (nDur[i] > 0) { StartHold(i, g, err); ShowJudge(g, err, "HOLD ", false); }
            else Finish(i, g, err);
            return true;
        }
        return false;
    }

    // Cabinet button hitboxes (index = lane-1). Box test in each box's own space, padded by buttonMargin metres.
    int ButtonLane(Vector3 world)
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            BoxCollider b = buttons[i];
            Transform bt = b.transform;
            Vector3 l = bt.InverseTransformPoint(world) - b.center;
            Vector3 h = b.size * 0.5f;
            Vector3 s = bt.lossyScale;
            if (Mathf.Abs(l.x) <= h.x + buttonMargin / Mathf.Abs(s.x) &&
                Mathf.Abs(l.y) <= h.y + buttonMargin / Mathf.Abs(s.y) &&
                Mathf.Abs(l.z) <= h.z + buttonMargin / Mathf.Abs(s.z)) return i + 1;
        }
        return 0;
    }

    // Cell value = bitmask of sensors a ball centred there would touch.
    long SensorAt(float x, float y)
    {
        if (sensorGrid == null || sensorGrid.Length == 0) return 0;
        int ix = (int)((x / gridHalf + 1f) * 0.5f * gridN);
        int iy = (int)((y / gridHalf + 1f) * 0.5f * gridN);
        if (ix < 0 || iy < 0 || ix >= gridN || iy >= gridN) return 0;
        return sensorGrid[iy * gridN + ix];
    }

    // ---------- results ----------

    void Finish(int i, int g, float err)
    {
        nState[i] = 2;
        Count(g, NoteBase(i), (nFlags[i] & 1) != 0);
        ShowJudge(g, err, "", true);
    }

    // b = base score of the note. Normal: P 100% / GR 80% / GD 50%. Break: CP/P 2500, GR 2000, GD 1000 + bonus CP 100 / P 75 / GR 40 / GD 30.
    // DX: CP 3, P 2, GR 1, GD/MISS 0.
    // ponytail: one PERFECT / GREAT tier each (real break splits P 75/50 and GR 2000/1500/1250 by timing).
    void Count(int g, int b, bool brk)
    {
        if (g == 0) crit++; else if (g == 1) perf++; else if (g == 2) great++; else if (g == 3) good++; else miss++;
        if (g < 4) combo++; else combo = 0;
        if (combo > maxCombo) maxCombo = combo;
        dxLost += g < 3 ? g : 3;
        if (brk)
        {
            lost += g <= 1 ? 0 : g == 2 ? 500 : g == 3 ? 1500 : 2500;
            lostB += g == 0 ? 0 : g == 1 ? 25 : g == 2 ? 60 : g == 3 ? 70 : 100;
        }
        else lost += g <= 1 ? 0 : g == 2 ? b / 5 : g == 3 ? b / 2 : b;
    }

    void Snap()
    {
        sCombo = combo; sCrit = crit; sPerf = perf; sGreat = great; sGood = good; sMiss = miss;
        sLost = lost; sLostB = lostB; sDxLost = dxLost; sMaxCombo = maxCombo;
    }

    void ShowJudge(int g, float err, string prefix, bool showMs)
    {
        string fl = err < -900f || g == 0 || g == 4 ? "" : (err < 0 ? "<color=#6FB8FFFF>FAST</color> " : "<color=#FF5A36FF>LATE</color> ");
        string ms = err < -900f || !showMs || g == 4 ? "" : (err > 0 ? "+" : "") + Mathf.RoundToInt(err * 1000) + "ms";
        lastJudge = "<color=" + gradeCols[g] + "><size=150%><b>" + prefix + gradeNames[g] + "</b></size></color>\n<size=70%>" + fl + ms + "</size>";
        judgeAt = Time.time;
        UpdateHud();
    }

    // maimai DX rank thresholds
    string Rank(float pct)
    {
        if (pct >= 100.5f) return "SSS+"; if (pct >= 100f) return "SSS"; if (pct >= 99.5f) return "SS+"; if (pct >= 99f) return "SS";
        if (pct >= 98f) return "S+"; if (pct >= 97f) return "S"; if (pct >= 94f) return "AAA"; if (pct >= 90f) return "AA";
        if (pct >= 80f) return "A"; if (pct >= 75f) return "BBB"; if (pct >= 70f) return "BB"; if (pct >= 60f) return "B";
        if (pct >= 50f) return "C"; return "D";
    }

    // AP+ all CP, AP no GR/GD/MISS, FC+ no GD/MISS, FC no MISS (from counts; a hold-release MISS still breaks FC)
    string Badge(int p, int gr, int gd, int m)
    {
        if (m > 0) return "";
        if (gd > 0) return "  <color=#6BE37A>FC</color>";
        if (gr > 0) return "  <color=#6BE37A>FC+</color>";
        if (p > 0) return "  <color=#FFC94D>AP</color>";
        return "  <color=#FFC94D>AP+</color>";
    }

    // 100% base + 1% break bonus (a chart with no breaks gets the 1% free). Dropping: shows 101.00 at the start.
    float Achievement(int l, int lb)
    {
        if (maxBase == 0) return 0f;
        return Mathf.Max(0f, 100f * (maxBase - l) / maxBase + (maxBonus > 0 ? (float)(maxBonus - lb) / maxBonus : 1f));
    }

    string Pct(float pct) { return pct.ToString("0.00") + "%"; }

    void UpdateHud()
    {
        if (hud == null || songTitles.Length == 0) return;
        BuildHud(Networking.IsOwner(gameObject));
    }

    // ---------- UI text (style: dark cards, small numbered mono labels, dot-matrix hero numbers, one orange accent) ----------
    const string Dim = "#8A8A8A", Acc = "#FF5A36";

    string Label(string num, string name) { return "<color=" + Dim + ">" + num + "  " + name + "</color>"; }

    string Artist()
    {
        string info = songInfo[song];
        int cut = info.IndexOf("  /  ");
        return cut >= 0 ? info.Substring(0, cut) : info;
    }

    string Short(string s, int n) { return s.Length > n ? s.Substring(0, n - 1) + "…" : s; }

    string Pad(string s, int n) { while (s.Length < n) s = " " + s; return s; }

    // difficulty tabs: selected one named in its colour with an accent dot, others just their level, dimmed
    string Tabs()
    {
        string tabs = "";
        for (int k = 0; k < 7; k++)
        {
            if (charts[song * 7 + k] == null) continue;
            if (tabs != "") tabs += "   ";
            tabs += k == level
                ? "<color=" + diffCols[k] + "><b>" + diffNames[k] + "</b></color>"
                : "<color=" + diffCols[k] + "70>" + levelNames[song * 7 + k] + "</color>";
        }
        return tabs;
    }

    string Row(int r, string name, string val)
    {
        return r == setRow ? "<color=" + Acc + ">●</color> <b>" + name + "</b>  " + val : "<color=" + Dim + ">○ " + name + "  " + val + "</color>";
    }

    void BuildHud(bool owner)
    {
        int c = owner ? crit : sCrit, p = owner ? perf : sPerf, gr = owner ? great : sGreat, gd = owner ? good : sGood, m = owner ? miss : sMiss;
        bool any = c + p + gr + gd + m > 0;
        float pct = Achievement(owner ? lost : sLost, owner ? lostB : sLostB);
        int dx = dxMax - (owner ? dxLost : sDxLost), mc = owner ? maxCombo : sMaxCombo;
        string lv = levelNames[song * 7 + level];
        if (menuLabels != null) menuLabels.SetActive(!playing);
        if (!playing && hintTexts != null && hintTexts.Length == 8)
            for (int k = 0; k < 8; k++)
            {
                bool go = menuPage == 0 ? (k == 3 || k == 4) : k == 3;
                hintTexts[k].text = "<color=" + (go ? Acc : "#D8D8D8") + ">" + (menuPage == 0 ? hints0[k] : hints1[k]) + "</color>";
            }

        // ----- main screen ----- (play: judgement high, combo centre; menu: card text top, big number bottom)
        if (centerT != null) centerT.localPosition = new Vector3(0, radius * (playing ? 0.32f : 0.18f), -0.004f);
        if (bigT != null) bigT.localPosition = new Vector3(0, radius * (playing ? -0.12f : -0.38f), -0.004f);
        if (playing)
        {
            int cb = owner ? combo : sCombo;
            center.text = owner ? lastJudge : "";
            if (bigText != null) bigText.text = cb > 1 ? cb + "\n<size=22%><color=" + Dim + ">COMBO</color></size>" : "";
        }
        else if (menuPage == 0)
        {
            center.text = "<size=55%>" + Label("01", "SELECT SONG") + "   <color=" + Dim + ">" + (song + 1) + "/" + songTitles.Length + "</color></size>\n" +
                          "<b>" + songTitles[song] + "</b>\n<size=60%><color=" + Dim + ">" + Short(Artist(), 30) + "</color></size>\n" +
                          "<size=55%>" + Tabs() + "</size>";
            if (bigText != null) bigText.text = "<color=" + diffCols[level] + ">" + lv + "</color>";
        }
        else
        {
            string of = (offsetMs > 0 ? "+" : "") + Mathf.RoundToInt(offsetMs);
            center.text = "<size=55%>" + Label("02", "SETTINGS") + "</size>\n" +
                          "<size=90%>" + Row(0, "SPEED ", speed.ToString("0.0")) + "\n" + Row(1, "OFFSET", of + "ms") + "</size>\n" +
                          "<size=45%><color=" + Dim + ">" + (setRow == 0 ? "notes travel " + approach.ToString("0.00") + "s" : "+ = notes later") + "</color></size>";
            if (bigText != null) bigText.text = setRow == 0 ? speed.ToString("0.0") : of;
        }
        if (center == hud) return;

        // ----- top screen: 01 song card / 02 achievement card / 03 judge card -----
        hud.text = "<size=55%>" + Label("01", playing ? "NOW PLAYING" : "SELECT SONG") + (playing && !owner ? "  <color=" + Acc + ">●</color> <color=" + Dim + ">SPECTATING</color>" : "") + "</size>\n" +
                   "<b>" + songTitles[song] + "</b>\n" +
                   "<size=60%><color=" + Dim + ">" + Short(Artist(), 34) + "</color></size>\n" +
                   "<size=60%><color=" + diffCols[level] + ">●</color> " + diffNames[level] + " " + lv + "</size>";
        if (hudBigLabel != null) hudBigLabel.text = Label("02", playing ? "ACHIEVEMENT" : "LAST PLAY");
        if (hudBig != null)
            hudBig.text = playing || any
                ? pct.ToString("0.00") + "<size=45%>%</size>" + (playing ? "" : "\n<size=40%><color=#FFC94D>" + Rank(pct) + "</color>" + Badge(p, gr, gd, m) + "</size>" +
                   "\n<size=22%><color=" + Dim + ">DX " + dx + "/" + dxMax + "   MAX " + mc + "</color></size>")
                : "<color=#FFFFFF30>--.--</color><size=45%><color=#FFFFFF30>%</color></size>";
        if (hudStats != null)
            hudStats.text = "<size=80%>" + Label("03", "JUDGE") + "</size>\n" +
                            "<color=#FFE36B>CRIT </color>" + Pad("" + c, 5) + "\n<color=#FFA640>PERF </color>" + Pad("" + p, 5) +
                            "\n<color=#FF7AD0>GREAT</color>" + Pad("" + gr, 5) + "\n<color=#6BE37A>GOOD </color>" + Pad("" + gd, 5) +
                            "\n<color=" + Dim + ">MISS </color>" + Pad("" + m, 5);
    }

    // dotted judge ring: lit orange up to song progress (local, cheap: only when it moved)
    void UpdateRing(float t)
    {
        if (ringMat == null) return;
        float pr = playing && endSec > 0f ? Mathf.Clamp01(t / endSec) : 0f;
        if (Mathf.Abs(pr - lastRing) < 0.004f) return;
        lastRing = pr;
        ringMat.SetFloat("_Progress", pr);
    }
}
