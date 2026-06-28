# morBreaker

[![Unity](https://img.shields.io/badge/Unity-6000.4.11f1-black?logo=unity)](https://unity.com/)
[![Render Pipeline](https://img.shields.io/badge/Render%20Pipeline-URP%2017.4-blue)](https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@17.4/manual/index.html)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Platforms](https://img.shields.io/badge/platforms-Windows%20%7C%20WebGL-informational)](#platforms--scope)
[![Status](https://img.shields.io/badge/status-MVP%20complete-brightgreen)](#features)
[![Made with](https://img.shields.io/badge/made%20with-spare%20time-ff69b4)](#)

A 2D brick-breaker (a.k.a. block-breaker / bat-and-ball) game built in Unity 6. Move the paddle, launch the
ball, and clear every brick — levels speed up as you go. Designed in a portrait 9:16
aspect, it targets two platforms: a **Windows** standalone build for classic PC play, and
a **WebGL** build you can embed as a free page on any website.

> **A personal, spare-time, non-commercial project.** No commercial goals, no ads,
> no tracking, no data collection.

## Features

- Bouncing ball with paddle "english" (where you hit steers the bounce) that **speeds up
  on every wall bounce and every non-breaking brick hit** — the ramp steepens each level
  and is capped, then resets when you lose a ball
- **10 hand-tuned levels** of rising difficulty: from all single-hit bricks at level 1 to a
  mix of 1-, 2- and 3-hit bricks at level 10
- Runtime-generated brick grid with per-row durability and colour-coded hit points
- Score, lives, level progression, and a **win bonus** for clearing the final level
- **Local high-score list** (name + score + level + date + a "beat-the-game" flag) with an
  in-game top-10 panel (`F1`), saved in the browser's `localStorage` — never sent anywhere
- Keyboard **and** pointer/touch controls (built on the Unity Input System)
- Lightweight HUD that scales to any screen size

## Platforms & scope

morBreaker is intentionally small and deliberately scoped to **two build targets**:

| Target | Why |
| --- | --- |
| **Windows** (Standalone, x86_64) | A classic desktop PC build you can download and run. |
| **WebGL** | Embeddable as a free, in-browser page on any website you control. |

**In scope:** single-player brick-breaker, 10 hand-tuned levels, score/lives/win bonus, a
**device-local** high-score list (browser `localStorage` on WebGL), keyboard + pointer/touch
input, and a screen-scaling HUD — all original assets, MIT-licensed, no data collection.

**Out of scope** (by design): mobile/console builds, multiplayer or online play, any
server/backend or shared/global leaderboard, accounts, ads, analytics, in-app purchases, or
any paid dependency. The earlier idea of a same-origin score API was dropped in favour of
purely local storage.

## Getting started

### Prerequisites

- [Unity **6000.4.11f1**](https://unity.com/releases/editor/archive) (see [`ProjectSettings/ProjectVersion.txt`](ProjectSettings/ProjectVersion.txt))
- The **Windows Build Support (IL2CPP)** and/or **WebGL Build Support** module(s) for that Editor version

### Run it

1. Clone the repo and open the project folder in Unity Hub with the matching Editor version.
2. Let Unity import and resolve packages on first open (see [Local setup](#local-setup) below).
3. Open [`Assets/Scenes/SampleScene.unity`](Assets/Scenes/SampleScene.unity).
4. Press **Play**.

### Local setup

To keep the repository minimal, some restorable files are **not** committed (they are
listed in [`.gitignore`](.gitignore)). Unity regenerates them locally:

- **`Library/`, `Temp/`, `Logs/`, `obj/`, `UserSettings/`, IDE solution files
  (`*.csproj`, `*.sln`, `*.slnx`)** — regenerated automatically the first time you open
  the project in the Unity Editor. No action needed.
- **`Assets/Plugins/NuGet/` (~18 MB of editor-only tooling DLLs)** — these belong to the
  [Unity MCP plugin](https://openupm.com/packages/com.ivanmurzak.unity.mcp/) and are
  **only needed for AI-assisted editing, not to play or build the game.** The package is
  already declared in [`Packages/manifest.json`](Packages/manifest.json) and resolves
  from the OpenUPM scoped registry on first open; the plugin then restores the NuGet DLLs
  itself. If they don't appear, open the plugin's window from the Unity menu
  (**Window ▸ AI Game Developer / MCP**) to trigger the NuGet restore. If you don't intend
  to use the AI tooling, you can safely ignore this folder entirely.

> The game's own code lives entirely in [`Assets/Scripts/`](Assets/Scripts/) and has **no
> dependency** on the MCP tooling — a clean clone plays and builds without it.

- **`.claude/`** — local Claude Code settings and the ~75 auto-generated MCP skill docs are
  **not** committed. The exception is the three hand-written shared skills
  (`release`, `work-issue`, `list-issues`), which **are** committed (the
  [`.gitignore`](.gitignore) re-includes exactly those folders).

### Controls

| Action | Keyboard | Pointer / touch |
| --- | --- | --- |
| Move paddle | `A` / `D` or `←` / `→` | Hold and drag horizontally |
| Launch ball | `Space` / `↑` | Tap / click |
| Enter name (if leaderboard enabled) | Type, then `Enter` | Type, then **Submit** |
| Show / hide top-10 scores | `F1` | — |
| Restart (after results) | `Space` / `Enter` | Tap / click |

## How it works

All gameplay lives in [`Assets/Scripts/`](Assets/Scripts/) (namespace `MorBreaker`). The
pieces are decoupled and communicate through static C# events rather than direct
references:

- **`BallController`** — bouncing ball that accelerates on each bounce; raises `Launched` / `Lost`.
- **`PaddleController`** — keyboard + pointer movement, clamped to the playfield.
- **`BrickController`** — one destructible brick; raises `Destroyed` / `Hit` / `Damaged`.
- **`BrickSpawner`** — builds the brick grid at runtime from a level layout; raises `LevelCleared`.
- **`LevelTable`** — pure design data: the 10-level layout curve and per-level speed ramp.
- **`GameManager`** — the hub: owns score/lives/level state and the HUD, drives the
  win/lose flow, the optional name prompt, the `F1` scores panel, and reacts to the events above.
- **`Leaderboard`** — local high-score list (see below). No network, no server, no accounts.

## Privacy & data

morBreaker has **no analytics, no telemetry, and never talks to any server or third party**.
Nothing the game produces ever leaves your device. All gameplay state lives in memory for
the session; the **only** thing persisted is the local high-score list (below), kept on your
own machine.

## High scores (local only)

When a game ends (win or lose) the player *may voluntarily* type a short nickname (max 10
characters); that nickname plus the run's score, level, date (no time), and a "beat-the-game"
flag are saved to a **local high-score list**. Press **`F1`** any time to see the top 10.

- **WebGL builds** store the list in the browser's
  [`localStorage`](Assets/Plugins/WebGL/MorBreakerLocalStore.jslib) under the key
  `morBreaker.highscores`. It is **per-browser and per-device** (a score set in Chrome on
  your desktop won't appear in Firefox or on your phone), and it **survives between sessions**
  until the player clears the site's data (clearing only the browser *cache* does not remove
  it; private/incognito windows discard it when closed).
- **Editor / other platforms** keep the list in memory only (lost when play stops).
- The `completed` flag is `true` only when the player **beat the game** (cleared the final
  level), distinguishing a winner from someone who merely *reached* level 10 and then died.
- Nicknames are sanitised to ≤10 chars from `[A-Za-z0-9 _-]`. Set `enableLeaderboard = false`
  on the `Leaderboard` component (on the `GameManager` object) to turn the feature off, or
  change `storageKey` / `maxStored` to taste.

> Because the list is purely local, "cheating" it only affects the player's own browser —
> there is nothing to spoof, no secret to leak, and no abuse surface.

## Contributing

This is a personal, non-commercial learning project, but issues and suggestions are welcome.
Please read [`CONTRIBUTING.md`](CONTRIBUTING.md) first — it covers the constraints that govern
all work here (**free resources only**, **no copyrighted/trademarked material**, **no data
collection**, **Windows + WebGL only**) and the coding conventions.

## Built with Claude Code 🤖

morBreaker was built almost entirely with **[Claude Code](https://claude.com/claude-code)**,
Anthropic's agentic coding tool, driving the Unity Editor live through the
[Unity MCP plugin](https://openupm.com/packages/com.ivanmurzak.unity.mcp/)
(`com.ivanmurzak.unity.mcp`, MIT). Claude Code wrote the gameplay scripts, wired the scene and
prefabs, ran the tests, and helped maintain these docs.

> **This is also a proof-of-concept.** It's my **first attempt at pairing Claude Code with a
> Unity MCP server** — a deliberate test of how well an AI agent can design and build a small
> but complete game inside Unity, end to end. If you're curious about AI-assisted game
> development, this repo is meant to be a readable, real-world example you can learn from and
> reproduce. **Give it a try — AI-assisted development is genuinely worth exploring.**

The MCP tooling is **editor-only**: it is not part of the game, ships in **no** build, and a
clean clone plays and builds without it (see [Local setup](#local-setup)). It is included
purely as a convenience for development.

## Releases & CI

A **release** is a [GitHub Release](https://github.com/KingMordas/mor-breaker/releases) carrying
two build artifacts — the **Windows** standalone ZIP and the **WebGL** ZIP (the latter is what
you drop onto a web page to host the in-browser version). There is no server deploy.

Continuous integration is deliberately minimal (this is a free-tier public repo):

- **On every PR to `main`:** a `changelog-preview` check lints the commits as
  [Conventional Commits](https://www.conventionalcommits.org/) and requires a
  [`CHANGELOG.md`](CHANGELOG.md) entry. `main` is squash-only and branch-protected to require it.
- **At release time:** the `security` workflow (gitleaks / semgrep / trivy / checkov) and the
  Unity tests run as the quality gate; then the version is bumped, both targets are built locally
  from the Editor, and the GitHub Release is published with the two ZIPs.

Maintainer tooling lives in three Claude Code skills — `/work-issue`, `/list-issues`, `/release`
— documented in [`CLAUDE.md`](CLAUDE.md). Commit/PR conventions are in
[`CONTRIBUTING.md`](CONTRIBUTING.md).

## Forking this project

The game code, assets, and builds are deliberately generic — there is no hard-coded website,
server, or service, so a fork plays and publishes anywhere with no code changes. If you fork it
to publish under your own name, the only things you'd typically change are **identity** fields:

| What | Where | Notes |
| --- | --- | --- |
| Company / app id | [`ProjectSettings/ProjectSettings.asset`](ProjectSettings/ProjectSettings.asset) — `companyName`, `applicationIdentifier` (`com.mordware.morbreaker`) | A reverse-DNS bundle id you control. Change to your own (e.g. `com.yourname.morbreaker`). Safe to leave as-is for a private build, but use your own before publishing a store/app build. |
| Copyright holder | [`LICENSE`](LICENSE) | MIT requires the original copyright line to be **kept**; add your own line for substantial changes rather than replacing it. |
| Repo references | [`README.md`](README.md), [`CLAUDE.md`](CLAUDE.md), `.claude/skills/*` | Point the maintainer tooling (`/release`, `/work-issue`, `/list-issues`) and links at *your* GitHub repo. |

The WebGL build is plain static files (it uses Unity's default template and only the browser's
`localStorage`), so you host it on **any** web page — your own site, GitHub Pages, itch.io, etc.
The Windows build is a standalone player anyone can download and run.

## License

Released under the [MIT License](LICENSE). Contributions are accepted under the same license.
