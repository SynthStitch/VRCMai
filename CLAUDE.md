# VRCMai — maimai-style rhythm world for VRChat

UdonSharp, Unity 2022.3.22f1, VRChat SDK 3.10.6. Ports MajdataPlay rules (GPL-3.0). Repo: github.com/SynthStitch/VRCMai (main).
World scene: `Assets/Scenes/MaiWorld.unity` (`MaiBatchWorld.World`). PC only — not optimizing for Quest.

## User preferences
- Short replies, commit + push after each finished unit, fix forward.
- No PlayerData saving; settings reset each visit.
- UI style from reference images: dark cards, dot-matrix numbers (Doto), Space Mono, orange accent `#FF5A36`.
- Never publish/commit third-party assets (room model, lightmaps, SALVATOR, skins, charts, VPM packages — see .gitignore).
- Don't replace package folders (e.g. lilToon) — ask the user to fix via VCC.

## Headless workflow (Unity must be CLOSED — check `tasklist | grep -ci unity.exe`)
```
"/c/Program Files/Unity/Hub/Editor/2022.3.22f1/Editor/Unity.exe" -batchmode -projectPath "C:/Users/danie/Documents/!avatar/Unity project/maimai" -executeMethod <X> -logFile <log>
```
- `MaiBatch.Compile` — U# compile check
- `MaiBatchTest.AutoPlay` (env `MAI_TEST_TITLE`) — perfect run must give 101% / full DX / full combo; also tests streamed slot 0
- `MaiBatchPreview.Preview` (env `MAI_PREVIEW_DIR`, `MAI_TEST_TITLE`, `MAI_PREVIEW_T`, `MAI_PREVIEW_MENU` 0 play/1 select/2 settings/3 results) → preview_screen.png, _p2, _both
- `MaiBatchStage3.Run` — rebuild song list + notes after importing charts
- Full rebuild order: `MaiBatchUi.Run` → `MaiBatchP2.Run` → `MaiTvSetup.Batch` → `MaiStreamSetup.Batch` → `MaiStreamToggle.BatchOff`

## Gotchas
- New U# behaviour: first batch run fails "outdated script version" — just rerun.
- Reflection in batch scripts needs `BindingFlags.Public` (fields are public for cross-behaviour reads).
- Dynamic batching drops object scale: shaders get size via MaterialPropertyBlock (`_Len` holds, `_Spread` touch). PanelShape has DisableBatching.
- Bash heredocs mangle `\n` in C# strings — use Write/Edit or a python patch file.
- Audio clips need `preloadAudioData = true` or there's no sound in VRChat.
- P2 = copy of MaiCabinet offset (1.03,0,0); model has only P1 hitboxes/lights, P2 copies them. P2 screen = P1 screen + offset. Rebuild P1 before P2.

## Key files
- `Assets/Maimai/Runtime/MaiGame.cs` — game, scoring (MajdataPlay values), results, 2P link, streaming hooks
- `NoteShape.shader` — procedural + AstroDX sprite skin (holds 9-slice, touch wedges close at hit)
- `MaiScoreboard.cs` — room TV, single + VS
- `MaiStream.cs` + `Editor/MaiStreamExport.cs` / `MaiStreamSetup.cs` — online songs (side screen, off by default)
- `Editor/MajnetBrowser.cs` — Maimai → majdata.net Browser to import charts

## State (2026-11-05)
Done: scoring, results, 2P linked play, TV scoreboard, sprite skin, room merge, streaming steps 1–2. User confirmed audio + 2P work in VRChat.
Built-in songs: all 17 majdata.net Originals (no more exist). Other majdata charts = copyrighted music → user's call, not mine.
Deferred: streaming step 3 (AVPro audio, needs "Allow Untrusted URLs"), Pages repo `synthstitch/vrcmai-songs` (doesn't exist yet), auto-sync Action, AstroDX manifest.json. Ask composers before going public. Third-party files still in git history (0d1cb5f, 472ba03) — force-push only if asked.
