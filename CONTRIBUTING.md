# Contributing to morBreaker

Thanks for your interest! morBreaker is a **personal, spare-time, non-commercial** 2D
brick-breaker built in Unity 6. Issues, suggestions, and small pull requests are welcome —
please read the rules below first, because they are non-negotiable constraints that govern
everything in this repo.

## Ground rules (please respect these)

1. **Free resources only.** No paid tools, packages, assets, fonts, or services may be
   introduced. Everything here must stay free-to-use.
2. **No copyright or trademark violations.** Use only original assets, names, and code — no
   Arkanoid (or other) sprites, sounds, level layouts, or trademarked names.
3. **No data collection, ever.** No analytics, no telemetry, no tracking, and nothing is
   sent to any server or third party. Every gameplay script's doc comment asserts *"Stores
   no data of any kind"* — preserve that when editing those scripts. The **one** allowed
   exception is the high-score list, which is stored **only on the player's own device**
   (browser `localStorage` on WebGL, in memory otherwise) and never transmitted.
4. **Security first.** Watch for and flag anything that could leak data, embed a secret, or
   be exploited. Do not commit keys, tokens, or credentials.
5. **MIT-licensed.** By contributing you agree your contribution is licensed under the
   project's [MIT License](LICENSE).

## Platform scope & extending it

The **canonical** build targets are **Windows (Standalone, x86_64)** and **WebGL** — that is
what the maintainer ships, and what the `/release` workflow builds. But this is an open,
MIT-licensed project, so if you want to take it further — a **mobile** (or other platform)
build, new level packs, audio, particle effects, accessibility options, anything else — **why
not? Go for it.** Contributions of essentially any kind are welcome; you're free to build on it
however you see fit.

A few things to keep in mind when you extend beyond the two default targets:

- **The ground rules above are still non-negotiable** — free resources only, no
  copyrighted/trademarked material, no data collection/telemetry, security first, MIT. *Legal
  compliance and respect for others come before any feature.*
- **Mind the tooling.** The `/release` skill only builds Windows + WebGL today. If you add a
  platform you want the project to ship **officially**, update that workflow too (see *Keeping
  the tooling current* below); otherwise, build and distribute your variant from your own fork.
- **Avoid scope creep in the core build.** Things like multiplayer/online, server backends,
  accounts, ads, or monetization run against the project's spirit — discuss in an issue first
  rather than adding them to the default Windows/WebGL build.
- **Open an issue first** for anything sizable, so we can agree on scope before you invest time.

## Environment

- **Unity `6000.4.11f1`** (see [`ProjectSettings/ProjectVersion.txt`](ProjectSettings/ProjectVersion.txt)) with URP.
- Install the **Windows Build Support (IL2CPP)** and/or **WebGL Build Support** module(s).
- See the README [Local setup](README.md#local-setup) for the (optional, editor-only) Unity
  MCP tooling and the restorable files that are not committed.

## Coding conventions

- All gameplay code lives in [`Assets/Scripts/`](Assets/Scripts/) under the `MorBreaker`
  namespace.
- Components are **decoupled via static C# events**, not direct references — raise an event
  rather than reaching across to another component; `GameManager` is the hub that subscribes.
  See [`CLAUDE.md`](CLAUDE.md) for the architecture and the gotchas that have bitten before
  (Input System only, kinematic walls/paddle, sprite import mode, etc.).
- **Input System only** — use `UnityEngine.InputSystem` (`Keyboard.current`, `Pointer.current`).
  Legacy `UnityEngine.Input` throws at runtime.
- Match the style, naming, and comment density of the surrounding code. Keep it small and
  readable; this is a tiny codebase on purpose.
- Keep [`README.md`](README.md), [`CLAUDE.md`](CLAUDE.md), **and the
  [`.claude/skills/`](.claude/skills/) workflow skills** (`/release`, `/work-issue`,
  `/list-issues`) up to date with any change that affects them — the skills are documentation
  of the actual process, so a process change that leaves them stale is an incomplete change.

## Submitting changes

1. Open an issue first for anything beyond a trivial fix, so we can agree on the approach.
2. Keep pull requests **small and focused**; one concern per PR.
3. Make sure the project **compiles with no errors or new warnings**, and that the game still
   plays from `Assets/Scenes/SampleScene.unity`. If you touch logic covered by tests, run the
   Unity test runner (EditMode/PlayMode) and keep it green.
4. Describe what you changed and why, and confirm it respects the ground rules above.

## Commit & PR conventions

Commits follow [Conventional Commits](https://www.conventionalcommits.org/):

```
type(scope): imperative description (~50 chars, no trailing period)

Body explaining WHAT changed and WHY (bullets for multiple items).
Flag guardrail-relevant touches: new dependencies, any privacy/data-flow
change, gameplay doc-comment edits, README/CLAUDE doc updates.

Closes #<issue>

Co-Authored-By: <name> <email>
```

- `type` ∈ `feat` / `fix` / `docs` / `style` / `refactor` / `perf` / `test` / `build` / `ci` / `chore`.
- `scope` (optional) names the area: `ball`, `paddle`, `brick`, `spawner`, `gamemanager`,
  `leaderboard`, `level`, `ui`, `build`, …
- A breaking change uses `type(scope)!:` and/or a `BREAKING CHANGE:` footer.

The conventional-commit `type` drives the SemVer bump and a per-PR changelog **preview** comment,
but the **durable changelog record is the hand-maintained `CHANGELOG.md` `## [Unreleased]`** section
— **every user-visible change must add its entry there in the same change** (plain, player-facing
English). A per-PR CI check (`changelog-preview`) **fails on any non-conventional commit** and on a
PR that doesn't update `CHANGELOG.md`; truly invisible changes (CI tweaks, test-only refactors) may
carry a `skip-changelog` label instead.

**PR bodies** are structured:

```
## Summary    — what & why
## Changes    — bullet list of areas/decisions
## Testing    — what was verified (build/play/tests)

Closes #<issue>
```

## How the workflow runs

This repo is GitHub-driven. To conserve CI minutes, the full test + security suites run **at
release time**, not per-PR — the only PR-time check is the `changelog-preview` gate above.

- **`main` is squash-only and branch-protected** (the `changelog-preview` check is required), so
  `main` stays one parseable Conventional Commit per PR.
- A **release** is a GitHub Release with two attached build ZIPs — the **Windows** standalone and
  the **WebGL** build — produced locally from the Unity Editor (there is no Unity build in CI).
- The maintainer drives issues/branches/releases with three Claude Code skills:
  **`/work-issue <n>`** (branch → implement → PR → confirm-merge), **`/list-issues`** (backlog
  overview), and **`/release`** (gates → version bump → build both targets → GitHub Release). See
  [`CLAUDE.md`](CLAUDE.md) → *Commits, changelog & release*.

### Keeping the tooling current

The `/release` skill **does not deploy anywhere**. It only runs the quality gates, builds the
two players locally from the Unity Editor, and publishes a **GitHub Release with the Windows and
WebGL ZIPs attached** — that's it. There is no server, host, or store push; hosting the WebGL
build on a web page (or installing the Windows build) is a separate, manual choice left to
whoever downloads the assets.

Because of that, please **keep the skill files in [`.claude/skills/`](.claude/skills/) in sync
with reality**, exactly as you would `README.md`/`CLAUDE.md`. If you change the release process,
the build targets, or the branch/PR flow, update the matching skill in the same change. An
accurate `/release` skill is what keeps releases reproducible for the next contributor.

## Reporting bugs

Open an issue with: what you did, what you expected, what happened, your platform
(Windows / WebGL + browser), and the Unity/Editor version. Screenshots or a short clip help.

## Be kind

Be respectful and constructive. This is a hobby project maintained in spare time — reviews
and replies may take a while. Thanks for understanding, and for contributing!
