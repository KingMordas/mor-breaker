# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

**morBreaker** is a 2D brick-breaker (block-breaker / bat-and-ball) game built in Unity 6 (6000.4.11f1), URP. It is a personal, spare-time, non-commercial game.

**Build targets (output perimeter): Windows (Standalone, x86_64) and WebGL — nothing else.** Windows is the classic downloadable PC build; the WebGL build is embeddable as a free page on any website. Out of scope by design: mobile/console builds, multiplayer/online play, any server/backend or shared leaderboard, accounts, ads, analytics, IAP, or paid dependencies. Every change should build and run on both targets. App id `com.mordware.morbreaker`, company `mordware`.

### Project constraints (govern all work here)

1. **Free resources only.** Use only free-to-use tools, packages, and assets. The author neither can nor wants to pay for external stuff — never introduce a paid dependency, asset, or service.
2. **Personal, spare-time project.** No commercial goals, and no affiliation with the author's real job (which is deliberately undisclosed — do not infer, reference, or ask about it).
3. **No copyright or privacy violations, ever.** Use only original assets, names, and code — no Arkanoid sprites/sounds/level layouts or trademarked names. No analytics, no telemetry, no identifying PlayerPrefs, and send NOTHING to any third-party service. All game state (score/lives/level) lives in memory only, and every *gameplay* script's doc comment asserts "Stores no data of any kind" — preserve this when editing those scripts (`BallController`, `PaddleController`, `BrickController`, `BrickSpawner`, `GameManager`, `LevelTable`).
   - **The one deliberate exception is `Leaderboard.cs`** (the optional high-score list, on by default), and even it **never leaves the device**. When a game ends it stores a *voluntarily-typed nickname* (≤10 sanitised chars) + score + level + date (`yyyy-MM-dd`, no time) + a `completed` flag (true only if the player beat the final level, distinguishing a winner from someone who merely reached level 10) in a **local high-score list**: the browser's `localStorage` in WebGL builds (via `Assets/Plugins/WebGL/MorBreakerLocalStore.jslib`, key `morBreaker.highscores`), or in memory in the Editor / other platforms. **No server, no network, no API key, no third party** — there is nothing to transmit and nothing to secure. (Earlier drafts considered a same-origin backend; that was dropped in favour of pure local storage, so the game makes no network calls of any kind.)
4. **Will likely be published** as a free public GitHub repository under the **MIT license**. Keep the codebase shareable and license-clean.
5. **Security first.** Always watch for security concerns — code-writing best practices, general vulnerabilities, unsafe dependencies, anything that could leak data or be exploited. Flag them proactively.
6. **Keep docs current.** Always keep `README.md` and `CLAUDE.md` up to date with the actual state of the project as part of any change that affects them.
7. **Ask when in doubt.** You have autonomy for routine work, but important decisions require user approval — ask for details before making them. Proactively flag anything that seems wrong, against best practices, or obsolete, and suggest the better path.

## Working in this repo (Unity MCP)

This Unity project is driven through the **Unity MCP plugin** (`com.ivanmurzak.unity.mcp`), exposed as Claude Code skills. **Prefer these skills over editing `.unity`/`.prefab`/`.asset` YAML or `.cs` files by hand** — they go through the live Editor, keep the AssetDatabase consistent, and validate C# via Roslyn before writing.

Common workflows:
- **Edit/create scripts**: `script-read`, `script-update-or-create`, `script-delete` (these refresh the AssetDatabase and wait for compilation).
- **Inspect/modify the scene & GameObjects**: `scene-open`, `scene-get-data`, `gameobject-find`, `gameobject-modify`, `gameobject-component-add/modify`.
- **Prefabs**: `assets-prefab-open` / `-save` / `-close`, `assets-prefab-instantiate`.
- **Run/observe**: `editor-application-set-state` (enter/exit play mode), `screenshot-game-view`, `console-get-logs`.
- **Tests**: `tests-run` (EditMode/PlayMode). Precondition: all open scenes must be saved (dirty scenes abort the run).

There is no command-line build/lint/test loop — Unity is the build system. Compilation happens in the Editor; `assets-refresh` forces recompilation after external file changes.

### What is NOT committed

To keep the repo minimal, [`.gitignore`](.gitignore) excludes restorable/dev-only files. Beyond the standard Unity generated folders (`Library/`, `Temp/`, `Logs/`, `obj/`, `UserSettings/`, IDE `*.csproj`/`*.sln`/`*.slnx`):
- **`Assets/Plugins/NuGet/`** — the MCP plugin's vendored DLLs (editor-only tooling, ~18 MB; the game never references them). Restored by the `com.ivanmurzak.unity.mcp` package on resolve. The README's "Local setup" section documents this.
- **`.claude/`** — local Claude settings + auto-generated SKILL docs. If you add a *shared* `.claude/settings.json` or a custom skill worth distributing, unignore it specifically.

If you change what is/isn't ignored, update the README "Local setup" section to match.

The two **shared custom skills** — `release` and `perform-activity` (under
`.claude/skills/`) — are the exception: they are committed (the `.gitignore` re-includes
exactly those folders). The auto-generated MCP skill docs and local settings remain ignored.

## Commits, changelog & release

This repo follows a lightweight, GitHub-driven workflow.
There is no server to deploy to: a **release** is a GitHub Release carrying two build artifacts
(the Windows and WebGL ZIPs). Two skills drive it — `/perform-activity` (do a piece of work →
optional branch + PR → confirm-merge) and `/release` (gate → version → build → GitHub Release).

> **No issue-driven tooling (deliberate, security).** Earlier drafts drove work from GitHub
> issues via `/work-issue` and `/list-issues`. Those were **removed**: on a public repo, issue
> and comment bodies are attacker-controllable, and ingesting them into the agent's context is a
> **prompt-injection vector** (a spammer commenting on an issue could smuggle instructions). The
> workflow is now driven by **trusted, locally-typed prompts** through `/perform-activity` — the
> agent reads **no** issue/comment/attachment text. GitHub Issues may still be used by humans for
> bug reports; the *agent* simply never auto-ingests them.

1. **Conventional Commits.** Commit subjects follow [Conventional Commits](https://www.conventionalcommits.org/):
   `type(scope): description` — imperative, ~50 chars, no trailing period (e.g.
   `feat(ball): add per-level speed cap`). `type` ∈ `feat`/`fix`/`docs`/`style`/`refactor`/
   `perf`/`test`/`build`/`ci`/`chore`; `scope` (optional) names the area (`ball`, `paddle`,
   `brick`, `spawner`, `gamemanager`, `leaderboard`, `level`, `ui`, `build`, …). A breaking
   change uses `type(scope)!:` and/or a `BREAKING CHANGE:` footer. After a blank line comes a
   **body** explaining *what* changed **and why** (bullets for several items), flagging any
   guardrail-relevant touches (a new dependency, a privacy/data-flow change, a gameplay
   doc-comment edit, `CLAUDE.md`/`README.md` doc updates). Commits end with the
   `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>` trailer.
   The conventional `type`/breaking-marker **drives the SemVer bump** — but it is **not** the
   changelog of record.

2. **`CHANGELOG.md` `## [Unreleased]` is the durable changelog** (Keep a Changelog format).
   **Every user-visible change adds a bullet under the right `### Added`/`### Changed`/`### Fixed`
   subsection in the same change**, in plain player-facing English. Entries are **not**
   reconstructed from commits at release time (squash-merges lose individual commits): the
   `/release` skill simply *promotes* `## [Unreleased]` to a stamped heading. If `## [Unreleased]`
   is empty at release, the player changelog is silently lost — treat the entry as part of
   "done". Purely internal changes a player could never notice (a CI tweak, a test-only
   refactor) may be omitted (mark the PR with the `skip-changelog` label instead).

3. **Versioning.** The app version lives in `ProjectSettings/ProjectSettings.asset`
   (`bundleVersion`), surfaced in-game by `GameManager` via `Application.version`. The **first**
   tagged release is **`1.0.0`** (the launch cut-off); subsequent releases bump from the last
   `v*` tag per SemVer. `/release` is the only thing that edits `bundleVersion`.

4. **CI runs at *release* time, not per-PR** (saves Actions minutes). The only
   workflow that runs on a PR to `main` is **`changelog-preview.yml`** — the **PR gate**: it lints
   every commit subject as a Conventional Commit (**blocking**), posts a sticky preview comment of
   the `## [Unreleased]` entries the PR would add, and **fails if the PR doesn't touch
   `CHANGELOG.md`** (bypass with the `skip-changelog` label). The **`security.yml`** scanners
   (gitleaks / semgrep / trivy / checkov) are **`workflow_dispatch`-only** and run at release time,
   dispatched by `/release` against `main`. The **Unity tests** (`tests-run`, EditMode/PlayMode) run
   **locally via MCP** during `/release` — there is no Unity build/test in CI (no Unity license in
   CI; Unity is the build system). `main` is configured **squash-only** and **branch-protected** to
   require the `preview` check, so it stays one parseable Conventional Commit per PR.

5. **Activity → PR lifecycle.** `/perform-activity [true|false] | <prompt>` takes a trusted,
   locally-typed instruction (parameters first, then a `|` separator, then the free-text prompt),
   analyses it, confirms guardrail compliance, implements honouring every guardrail here, and
   visually verifies any UI/gameplay change. With the **default `true`** flag it branches from
   `origin/main`, commits + opens a structured PR (title = a Conventional-Commit subject), waits
   for the `preview` check, then — **after you confirm** — squash-merges to `main`. With **`false`**
   it works in the current tree and stops without committing (for review). **No GitHub issue or
   comment is ever read or written** (beyond the optional PR it authors) — there are no issue
   labels and nothing for `/release` to reconcile. The harness blocks an agent from self-merging
   its own PR, so the merge step **requires explicit human confirmation**.

6. **`/release`** runs the quality gates (dispatch `security.yml` against `main`; run the Unity
   tests locally), computes the SemVer bump, promotes `## [Unreleased]`, bumps `bundleVersion`,
   **builds both targets locally through MCP** (Windows Standalone x86_64 + WebGL → two ZIPs under
   `Builds/`), commits + tags `v<X.Y.Z>`, pushes, and creates the **GitHub Release** with both ZIPs
   attached and notes from the promoted CHANGELOG section. It never deploys to a server (the WebGL
   ZIP is downloaded from the release and embedded on a web page of the maintainer's choice manually).

## Architecture

The entire game lives in **`Assets/Scripts/`** (7 files, namespace `MorBreaker`) plus one scene, `Assets/Scenes/SampleScene.unity`. Components are wired together in the scene; coordination between them is done with **static C# events**, not direct references — this keeps the pieces decoupled.

The event flow:

- `BrickController` (per brick) raises `Destroyed(int points)`, `Hit(BrickController)`, and `Damaged(BrickController)` (a hit the brick *survived* — drives ball acceleration). Only objects carrying a `BallController` damage it. `Init(hits)` sets durability at spawn; colour reflects remaining HP.
- `BrickSpawner` builds the brick grid **at runtime** (not baked into the scene) from a `LevelDefinition` via `Build(def)`, listens for `Destroyed` to count survivors, and raises `LevelCleared` when the last brick dies.
- `LevelTable` is pure static design data (no MonoBehaviour): the 10 `LevelDefinition`s (columns + per-row hit points, tougher rows on top) and `AccelPerHit(level)` (the per-bounce speed-ramp %, rising 0.40%→1.75% across the 10 levels). `Count` = 10; clearing the last level wins.
- `BallController` raises `Launched` (off the paddle) and `Lost` (fell past the bottom). Speed **ramps up** on each wall bounce and each survived-brick hit (`OnCollisionEnter2D` walls + subscribing to `BrickController.Damaged`), capped at `maxSpeedMultiplier`× the level base, and **resets to base whenever the ball is reset** (lost / new life / new level). `ConfigureLevel(baseSpeed, perHitAccel)` sets the per-level ramp; `Arm()`/`Hold()`/`ResetToPaddle()` are its public controls. The paddle bounce never accelerates.
- `Leaderboard` is the optional **local** high-score store (no networking). `Submit(name,score,level,completed,cb)` adds an entry, sorts desc, and caps at `maxStored`; `Fetch(top,cb)` returns the top N; `SanitizeName` clamps to ≤10 safe chars; the entry carries name/score/level/date/completed. Persistence is `localStorage` in WebGL (`#if UNITY_WEBGL && !UNITY_EDITOR`, via the `MorBreakerLSGet/Set/Free` jslib bridge), in-memory otherwise. Both `Submit`/`Fetch` keep their callback signatures (storage is synchronous, so callbacks fire immediately). See constraint #3 + README "High scores".
- `GameManager` is the hub: subscribes to `BrickController.Destroyed`, `BrickSpawner.LevelCleared`, `BallController.Launched`, `BallController.Lost`; owns the score/lives/level state machine (`Ready`/`Playing`/`Ended`, with an `EndPhase` of `NameEntry`/`Results`); drives `StartLevel()` (which calls `ball.ConfigureLevel()` + `spawner.Build(LevelTable.Get(level))`), the win bonus (`1000 + 500×remaining lives` on clearing level 10), the end-of-game name prompt → leaderboard submit → top-scores display, and the `F1` toggle for the scores panel. Sets the bottom-right version label from `Application.version`. Updates the legacy uGUI HUD and orchestrates spawning itself (`spawner.spawnOnStart = false`).
- `PaddleController` is standalone (no events): keyboard + pointer movement, clamped to the playfield.

When adding gameplay systems, follow this pattern: raise a static event rather than reaching across to `GameManager`, and have `GameManager` subscribe.

## Critical gotchas (these have bitten before)

- **Input System only.** Project `activeInputHandler` = New Input System; legacy `UnityEngine.Input` throws at runtime. All input must use `UnityEngine.InputSystem` (`Pointer.current`, `Keyboard.current`).
- **Paddle/walls must be Kinematic** Rigidbody2D with `gravityScale 0`. A Dynamic body under gravity (or edit-mode `Physics2D.Simulate`) falls and the bad position can get baked into the scene.
- **Sprite import mode.** The default texture import preset sets `spriteImportMode = Multiple`, so `LoadAssetAtPath<Sprite>` returns null on new PNGs. Force `Single` + reimport before loading the Sprite sub-asset.
- **Auto-fit colliders bake to size 0** if added while the SpriteRenderer's sprite is still null. Set `box.size` explicitly (e.g. `Vector2.one`) and `box.offset = Vector2.zero`.
- **Ball containment is a deliberate safety net.** `BallController.ContainWithinPlayfield()` does position-based death (`deathY`) + hard x/ceiling reflection because trigger zones (DeathZone) get no continuous-collision protection and can be tunnelled. DeathZone is matched by **name**, not `CompareTag` (the tag may be undefined and would throw).
- **Headless play-mode appears frozen.** With the Editor unfocused and "Run In Background" off, the game loop pauses (`Time.fixedTime` frozen) — not a bug. Set `Application.runInBackground = true` to observe motion while driving via MCP.

## Scene layout reference

Orthographic camera, `orthographicSize 6`, portrait 9:16 (world x ≈ [-3.375, 3.375], y ∈ [-6, 6]). `Playfield` holds Wall_Left/Right (x ±3.3), Wall_Top (y 5.85), DeathZone (y -6.6, trigger). Paddle (tag `Player`) at y -4.8. Brick grid spawns at runtime into x ≈ [-2.98, 2.98] from `topY 4.6` downward (5–7 rows depending on level). HUD is a `ScaleWithScreenSize` Canvas (ref 1080×1920) named `HUD`, holding Score/Lives/Message legacy `Text` plus the (initially hidden) `NameEntryPanel` (a legacy uGUI `InputField` + `SubmitButton`) and `LeaderboardPanel` (a `LeaderboardText`). A root `EventSystem` (with **`InputSystemUIInputModule`**, not the legacy module — see Input gotcha) drives the UI; the `Leaderboard` component lives on the `GameManager` object. These UI pieces and the EventSystem were built/wired by an editor script and are referenced by `GameManager`'s serialized fields.
