# Developing VRCMai

How the world is put together, how to rebuild it, and how to test it without opening the editor.

## Layout

```
Assets/Maimai/
  Runtime/            UdonSharp behaviours + shaders (what ships in the world)
    MaiGame.cs        one per cabinet: menus, sync, judging, scoring, drawing, results, P2 link, online charts
    MaiBall.cs        hand-ball pickup; grabbing it makes you the cabinet owner
    MaiScoreboard.cs  room TV: single result or VS card
    MaiStream.cs      online-song index + side-screen browser (experimental)
    NoteShape.shader  every note shape (procedural or sprite skin), sized via MaterialPropertyBlock
    PanelShape.shader UI cards (rounded panels)
  Editor/             editor-only setup, import and batch tools
    SimaiParse.cs     simai -> baked chart lines (one chart_N.txt per level)
    SimaiSlides.cs    slide paths + judge-area queues for every simai slide shape
    MaiSetup.cs       cabinet alignment, hitboxes, balls, song list rebuild
    MaiScreenSetup.cs screen render texture + sensor grid (A/B/C/D/E areas)
    MaiNotesSetup.cs  note pools and materials, skin textures
    MaiUiSetup.cs     HUD cards, button hints, results layout
    MaiP2Setup.cs     clones P1 into MaiCabinetP2 (MaiSide = which side is being set up)
    MaiTvSetup.cs     puts the scoreboard on the room's "TV" mesh
    MaiStream*.cs     online songs: site export, side screen, on/off toggle
    MajnetBrowser.cs  majdata.net search + import window
    MaiBatch*.cs      headless entry points (compile, autoplay test, previews, rebuilds)
  Fonts/              Space Mono, Doto, Noto Sans SC (+ TMP SDF assets)
  Generated/          materials, render textures, sensor meshes made by the setup tools
  Charts/             (not in git) one folder per song: maidata.txt, track.mp3, chart_N.txt, credits.txt
Assets/Scenes/MaiWorld.unity   the world
Stream/songs.txt               slot -> chart folder list for the online site
Tests/SimaiParseTest/          standalone .NET test of the simai parser and slide paths
```

## How a run works

- **Ownership**: the player holding the balls owns the cabinet's `MaiGame`. All judging runs locally on that client, and only results are synced.
- **Clock**: the owner syncs `startServerMs` (VRChat server time). Every client computes song time from it and seeks its own audio to match. If a client has no audio clip, the server clock alone drives the run.
- **Synced state** (manual sync): `song`, `level`, `playing`, `startServerMs`, P2's `joined`/`linked`, and the owner's final counts (`sCrit…sMiss`, `sLost`, `sLostB`, `sDxLost`, `sMaxCombo`, `sFast`, `sLate`), which spectators use to draw the results screen.
- **Charts** are converted in the editor. `SimaiParse` turns `maidata.txt` into one simple line-per-note file per level, with slide paths and judge queues already baked in (`SimaiSlides`). Udon only reads those lines.
- **Drawing**: notes are pooled quads rendered by a camera into the cabinet screen's render texture. Shape and size go through `NoteShape.shader` with a MaterialPropertyBlock (`_Len` for hold length, `_Spread` for touch closing). Dynamic batching would drop per-object scale.
- **Note motion** follows MajdataPlay:
  - `noteV = 107.25 / (71.4184491 * (speed + 0.9975)^-0.985558604) / 4.8`
  - taps spawn at 0.2552 of the radius and grow before traveling
  - touch approach time = `4.2 / speed`

### Scoring (MajdataPlay)
- Base points: tap/touch 500, hold 1000, slide 1500, break 2500. GREAT keeps 80%, GOOD keeps 50%.
- Break: base points lost per grade = 0/0/500/1500/2500; bonus % lost = 0/25/60/70/100.
- DX score: CRITICAL 3, PERFECT 2, GREAT 1.
- Achievement counts down from 101%: `100*(maxBase-lost)/maxBase + (maxBonus-lostB)/maxBonus`. With no breaks, the bonus term is +1.
- If you quit early, unjudged notes count as MISS.

### Two players
- `MaiCabinetP2` is a copy of `MaiCabinet` offset by (1.03, 0, 0). The model only has P1 hitboxes, lights and sensors, so the P2 setup copies them across.
- P2's `leader` is P1. **JOIN** (button 5) waits for P1 to start, then starts at the same server time with music muted (`linked`). If P1 stops, P2 unmutes and plays on alone.
- Both cabinets report to `MaiScoreboard`. A VS card appears when both results arrive within 30 s and one of them was linked.

## Editor menus (Maimai → …)

| Menu | Does |
|---|---|
| Build Prototype Cabinet | bare-bones cabinet for testing without the model |
| Align To DX Model (Display P1) | fits the screen and sensor grid to the model's display |
| Use Cabinet Button Hitboxes / Add Hand Balls | input wiring |
| Setup Screen + Sensors (Stage 1) / Setup Notes (Stage 2) / Setup UI | rebuild each layer |
| Setup Player 2 | rebuild the P2 cabinet from P1 (rebuild P1 first) |
| Setup TV Scoreboard | place and wire the room TV board |
| Rebuild Song List / Convert Selected maidata.txt | re-bake charts after adding songs |
| majdata.net Browser | search and import charts |
| Add CJK Font | fallback font for Chinese/Japanese titles |
| Stream: Export Site / Setup Side Screen / Toggle Side Screen | online songs |

## Headless workflow

Close Unity first, since batch mode can't open a project that's already open. Then run:

```sh
"C:/Program Files/Unity/Hub/Editor/2022.3.22f1/Editor/Unity.exe" -batchmode \
  -projectPath "<repo>" -executeMethod <Method> -logFile build.log
```

| Method | Purpose |
|---|---|
| `MaiBatch.Compile` | UdonSharp compile check |
| `MaiBatchTest.AutoPlay` | simulates perfect input on a chart (`MAI_TEST_TITLE`); must reach 101% / max DX / full combo. Also tests online slot 0 |
| `MaiBatchPreview.Preview` | renders the screens to PNG. Env vars: `MAI_PREVIEW_DIR`, `MAI_TEST_TITLE`, `MAI_PREVIEW_T` (seconds), `MAI_PREVIEW_MENU` (0 play, 1 select, 2 settings, 3 results) |
| `MaiBatchStage3.Run` | rebuild song list + notes |
| `MaiBatchUi.Run` → `MaiBatchP2.Run` → `MaiTvSetup.Batch` → `MaiStreamSetup.Batch` → `MaiStreamToggle.BatchOff` | full scene rebuild, in this order |

Parser test (no Unity needed, .NET 9 SDK): `dotnet run --project Tests/SimaiParseTest` runs the self-check and prints `OK`; pass a folder to parse every `maidata.txt` under it.

## Gotchas

- **New UdonSharp script**: the first batch run fails with "outdated script version" because the program asset was just created. Run it again.
- Batch scripts read behaviour fields via reflection, so include `BindingFlags.Public` (fields that other behaviours read must be public in U#).
- **Audio is silent in VRChat** unless the clip has *Preload Audio Data* on. The importer sets this.
- Rebuild P1 before P2: the P2 setup copies P1's current hitboxes, lights and screen.
- Don't overwrite VPM package folders by hand. Fix packages through VCC. (A room unitypackage once overwrote lilToon with old files.)

## Online songs

Experimental. The side screen is off by default (**Maimai → Stream: Toggle Side Screen**).

VRChat can't build URLs at runtime, so the world bakes **256 slot URLs** under `MaiStreamSetup.BaseUrl` (`https://synthstitch.github.io/vrcmai-songs/`). Whatever sits in a slot is that song, so new songs can go online without re-uploading the world.

1. List approved songs in `Stream/songs.txt` as `<slot><TAB><chart folder>`.
2. Run **Stream: Export Site**. It writes `Stream/site/`:
   - `index.txt`: header `VRCMAI 1 256`, then one line per song: `slot  title  artist  designer  lv1|..|lv7  seconds`
   - `c/NNN.txt`: every level of the chart, each under an `@<level>` header
   - `a/NNN.mp3`: the audio
   - `CREDITS.txt`
3. Publish `Stream/site/` to GitHub Pages at the base URL.

Status:
- **Working**: the index and per-slot charts download (String Loading), songs appear after the built-in list, and they play on the server clock.
- **Not built (step 3)**: streamed audio. It needs an AVPro video player plus VRChat's *Allow Untrusted URLs* for github.io.

Limits: online songs are addressed by their position in the index, so every client needs the same index loaded. majdata.net itself can't be streamed from, because it uses per-chart IDs and the raw simai format, and it isn't on VRChat's trusted URL list.
