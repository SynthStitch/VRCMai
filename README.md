# VRCMai

A maimai-style rhythm game world for VRChat, written in UdonSharp. You play on a full-size cabinet holding two
hand-ball pickups, with up to two players side by side. Judging, scoring and note motion are ported from
[MajdataPlay](https://github.com/LingFeng-bbben/MajdataPlay), so this project is licensed under **GPL-3.0** (see `LICENSE`).

> PC only. The world isn't optimized for Quest/Android.

## Features

- **Full simai chart support**: taps, holds, breaks, EX, slides (every shape, including wifi `w`), touch and touch-hold, each/both-hand coloring.
- **MajdataPlay judging**: frame windows for tap, touch, hold and slide, area-queue slide judging, fast/late counts.
- **maimai DX scoring**: achievement % up to 101 with the break bonus, DX score, ranks (D through SSS+), FC/FC+/AP/AP+, max combo.
- **Two cabinets (VS)**: P2 can *join* P1. Both start together, P2's music is muted, and P2 takes over the audio if P1 quits.
- **Room TV scoreboard**: shows the latest result (single) or a side-by-side VS card with WIN/DRAW.
- **Results screen** on the cabinet after every song. Spectators see it too.
- **Note skin**: procedural shapes, or an AstroDX-format sprite skin if one is present locally.
- **Per-player settings**: note speed (1–10) and judge offset (±500 ms). These are local and reset every visit, by design.
- **Low-latency input**: whoever grabs a ball becomes the cabinet owner, so hits are judged on their own machine.
- **Online songs (experimental, off by default)**: a side-screen browser that downloads charts from a static site. See [docs/DEVELOPING.md](docs/DEVELOPING.md#online-songs).

## How to play

1. Pick up the **red** and **blue** balls next to a cabinet. Grabbing them makes you that cabinet's player.
2. Touch the 8 ring buttons with the balls (desktop: keys **1–8**; Interact on the Start button starts or aborts).

| Page | Buttons |
|---|---|
| Song select | 2/3 next song · 6/7 previous song · 1 harder · 8 easier · 4/5 settings |
| Settings | 1/8 switch row (speed / offset) · 2/3 value + · 6/7 value − · 4 **start** · 5 back |
| Results | any button closes it (it also closes on its own after 15 s) |
| P2 cabinet | button 5 **JOIN**/**LEAVE**: follow P1's song and start |

Touch notes and slides are hit by moving a ball through the screen's sensor areas (A/B/C/D/E, same layout as the arcade).

## Building the world

Requirements: **Unity 2022.3.22f1** and the **VRChat Creator Companion (VCC)**.

This repo is **the system only**: scripts, shaders, setup tools and fonts. Your world scene, room and cabinet model
are yours to bring.

1. Clone the repo and add it to VCC as an existing project. VCC restores the packages listed in `Packages/vpm-manifest.json`.
2. Create or open your world scene, and add charts to `Assets/Maimai/Charts` (see *Adding songs* below).
3. Pick a cabinet:
   - **No model**: run **Maimai → Build Prototype Cabinet**. It creates a simple playable cabinet (and a VRCWorld + floor if the scene has none).
   - **Your own cabinet model**: name its parts `Display P1` (screen), `Body P1`, `Button Hitbox…` and `ButtonLight…`. Then run **Build Prototype Cabinet → Align To DX Model → Use Cabinet Button Hitboxes**.
4. Run **Setup Screen + Sensors → Setup Notes → Setup UI → Add Hand Balls**.
5. Optional:
   - **Setup Player 2**: the model needs a `Body P2` and `Display P2`.
   - **Setup TV Scoreboard**: uses any mesh named `TV`.
6. Build & upload with the VRChat SDK panel.

Adding songs:
- **Maimai → majdata.net Browser**: search, tick songs and Import. This downloads `maidata.txt` and `track.mp3`, writes `credits.txt`, sets the audio import (preload on) and rebuilds the list.
- **Manually**: put a folder with `maidata.txt` and `track.mp3` in `Assets/Maimai/Charts/`, then run **Maimai → Rebuild Song List**.

> Only upload music you have the right to use. VRChat's terms apply to private worlds too.

For the setup menus, the headless batch/test workflow and how the code is laid out, see **[docs/DEVELOPING.md](docs/DEVELOPING.md)**.

## Not included

The repo has no world. These stay local, either because they're third-party or because they're the author's own world:

| What | Where it goes |
|---|---|
| World scene (`MaiWorld.unity`) | `Assets/Scenes/` |
| Cabinet model  | your own folder |
| Charts and audio | `Assets/Maimai/Charts/` |
| AstroDX sample skin | `Assets/Maimai/Skin/` (optional; procedural notes are used without it) |
| VPM packages | `Packages/` (restored by VCC) |

## Sources and credits

### Code and game rules
- **MajdataPlay** (GPL-3.0): judge windows, hold/touch/slide judging, scoring and DX score, note speed and approach formulas. https://github.com/LingFeng-bbben/MajdataPlay
- **simai** chart format: the community format used by Simai, MajdataView/MajdataEdit, AstroDX and majdata.net. https://w.atwiki.jp/simai/
- **majdata.net**: chart and audio source, API used by the editor browser. https://majdata.net
- **AstroDX**: skin layout and sample skin template (used locally only). https://github.com/2394425147/astrodx
- **AstroDX Skin Developer Tool** (reference for the skin format): https://github.com/MarsF0/AstroDX-Skin-Developer-Tool
- *maimai* is a SEGA arcade game. This is an unofficial fan project and isn't affiliated with or endorsed by SEGA.

### Platform and packages
- VRChat Worlds SDK 3.10.6 and UdonSharp: https://creators.vrchat.com/worlds/
- VRChat Creator Companion: https://vcc.docs.vrchat.com/
- VRWorld Toolkit 3.5.1: https://github.com/oneVR/VRWorldToolkit
- lilToon 2.4.1: https://github.com/lilxyzw/lilToon
- Poiyomi Toon 10.0.28: https://github.com/poiyomi/PoiyomiToonShader
- TextMesh Pro (Unity)
- Lighting reference: VRC Library, *Create your first VRChat world: Lighting*. https://vrclibrary.com/wiki/books/create-your-first-vrchat-world/page/lighting

### Fonts (SIL Open Font License 1.1; license texts in `Assets/Maimai/Fonts`)
- Space Mono: Colophon Foundry / Google Fonts. https://fonts.google.com/specimen/Space+Mono
- Doto: https://fonts.google.com/specimen/Doto
- Noto Sans SC: Google. https://fonts.google.com/noto/specimen/Noto+Sans+SC

### Songs used in the private build
Original songs from majdata.net. The audio and charts aren't in this repo. Each song's folder has a `credits.txt` with the exact source link.

| Title | Music | Chart |
|---|---|---|
| Transcend My Love | Sephid feat. aria | Dororo & 缀梦音游嘉年华 |
| Memories in the Dusk | FreshP & DJ Momo | Akatsune |
| Sempre Vivacissimo | Polymath9 | 吓你●ω●吓你●ω● (Panpan & Delice) |
| 月下繚乱 ~2022 Re:Master~ | minus7LUFS | Neya & 缀梦音游嘉年华 |
| Ποσειδών | 1112 vs. Star | KuSA & 缀梦音游嘉年华 |
| ずんだオーバーダンス | 魔法使いシラガ | 魔法使いシラガ |
| 勇气之火 | Noble_Rose 有川石良 | bbben |
| 时钟流转之岛 | bbben | Sakurago / おにねこ |
| 老撸采样包与塔菲叫与凤鸣 | ueqqq | ororod |
| 宇宙夢中 | 汪ワンー feat. 東條空助 | Dororo feat. Renekton |
| DYDY之歌 | ueqqq | Dororo |
| EX-AID | DJ MOMO | — |
| MajTitle | bbben | 伍舟之雨 |
| 風の種 | KeyLord | おにねこ |
| 王道偶像.mp5 | bbben | — |
| (untitled) | Supa7onyz | 6v9 |

Thanks to all the composers and chart designers. Ask the creators before including their songs in a **public** world.

## License

GPL-3.0. See `LICENSE`. Third-party assets keep their own licenses and aren't covered by it.
