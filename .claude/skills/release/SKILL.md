---
name: release
description: Cut a new SemVer release of morBreaker — first run the security + Unity-test gates (driving any failure to green), then compute the next version from Conventional Commits, promote the CHANGELOG, bump bundleVersion, build BOTH targets locally through the Unity MCP bridge (Windows Standalone x86_64 + WebGL → two ZIPs), commit, tag v<X.Y.Z>, push, and create a GitHub Release with both ZIPs attached. Never deploys to a server. Use when the user invokes "/release" (optionally "/release major|minor|patch").
---

# Release

Cut a release of `KingMordas/mor-breaker`: **run the quality gates** (security scans in CI + the
Unity tests locally), turn the accumulated `## [Unreleased]` work into a stamped `CHANGELOG.md`
entry, bump the app version, **build the Windows and WebGL players locally** through the Unity MCP
bridge, push a `v<X.Y.Z>` tag, and publish a **GitHub Release carrying both build ZIPs**.

> **A release is a GitHub Release with two assets — there is no server deploy.** morBreaker ships
> as a downloadable Windows ZIP and a WebGL ZIP (the latter is downloaded from the release and
> embedded on a web page of the maintainer's choice manually). The builds are produced **locally in the running Unity
> Editor** via `script-execute` — there is no Unity build in CI (no Unity license in CI; Unity is
> the build system).

> **`/release` is the single quality gate.** `security.yml` is `workflow_dispatch`-only and the
> Unity tests don't run in CI, so this skill is the one place the full security + test suites run:
> it **dispatches `security.yml` against `main`**, **runs the Unity tests via MCP**, **drives any
> failure to green** (committing fixes directly to `main`), and only then bumps + builds + tags +
> releases. Never tag while a gate is red.

## Inputs

Invocation: `/release` or `/release <level>` where `<level>` ∈ `major` / `minor` / `patch`.

- **No argument** — compute the bump automatically from the Conventional Commits since the last tag.
- **`<level>`** — force that bump level, overriding the computed one.

Repo facts: remote `origin` → `https://github.com/KingMordas/mor-breaker.git`, default branch `main`,
tag prefix `v`. The version lives in `ProjectSettings/ProjectSettings.asset` (`bundleVersion`),
surfaced in-game by `GameManager` via `Application.version`. Use `git` / `gh` and the Unity MCP skills.

## Preconditions (stop and ask if any fails)

1. **`gh` is authenticated** — if not, report and stop.
2. **On `main` and clean** — `git branch --show-current` is `main` and `git status --porcelain` is empty (ignore Editor `.meta`/`Library` churn already covered by `.gitignore`; if there is genuine uncommitted work, **stop and ask**).
3. **Up to date with `origin/main`** — `git fetch origin` then confirm `main` is not behind. If behind, ask the user to pull/rebase first.
4. **Unity Editor reachable + clean** — the MCP bridge responds (`ping`), there are **no compile errors** (`console-get-logs`), and the active scene is **saved** (`scene-save`). The Editor must have the **Windows (IL2CPP)** and **WebGL** build-support modules installed — without them the build in step 6 fails; surface that and stop.

## Steps

### 1. Run the release quality gates (security + tests)

Do not touch any files until **both** gates are green.

1. **Dispatch the security scan against `main`** and read back the run id:
   ```powershell
   gh workflow run security.yml --ref main
   Start-Sleep -Seconds 5   # let the run register before reading it back
   $secRun = gh run list --workflow security.yml --branch main --limit 1 --json databaseId,url | ConvertFrom-Json
   gh run watch $secRun.databaseId --exit-status
   ```
2. **Run the Unity tests locally** via the `tests-run` MCP skill (EditMode, then PlayMode). **Precondition: all open scenes are saved** (dirty scenes abort the run). If the project has **no tests yet**, that is a **pass**, not an error — note it and continue.
3. **If both succeed** — continue to step 2.
4. **If either fails** — drive it to green on `main`, then re-run *that* gate, repeating until both pass:
   - The **security** workflow opens/updates one `bug` issue (titled `Security scan failures — main @ <sha>`) assigned to KingMordas. **Work it like a release-time fix**: stamp `in-progress`, analyse, implement the fix honouring every `CLAUDE.md` guardrail (via the Unity MCP skills), `git commit` (Conventional Commits, body, `Closes #<issue>`) and `git push origin main`, post a resolution comment, then flip the issue `+awaiting-release` / `-in-progress` so step 7 reconciles it. Re-dispatch `security.yml` and re-watch.
   - **Test failures** surface in the `tests-run` results (no auto-issue) — diagnose from the failing tests + `console-get-logs`, fix on `main`, commit + push, re-run the tests.
   - **Bound the loop:** after ~3 genuine fix attempts on the same failure (or a failure the skill can't fix — flaky infra, a missing build module), **stop and ask the user**.

Only once **both gates are green** does the release proceed.

### 2. Find the last release

```powershell
git fetch origin --tags
git describe --tags --abbrev=0 --match "v*"   # e.g. v1.2.0 ; none yet ⇒ first release
```

### 3. Collect the commits since the last tag and compute the bump

```powershell
git log --no-merges --format="%H%x1f%s%x1f%b%x1e" <lastTag>..HEAD
```

Parse each subject as a Conventional Commit (`type(scope)!: desc`). Determine the bump:

- **First release:** if there are **no `v*` tags yet**, the version to cut is **`1.0.0`** (the launch cut-off). Skip the rest of this step.
- Otherwise, from the last tag: any **breaking** change (`!` or a `BREAKING CHANGE:` footer) ⇒ **major**; else any `feat` ⇒ **minor**; else (`fix`/`perf`/`refactor`/…) ⇒ **patch**.

A `<level>` argument overrides the computed bump. Apply it to the last released version to get `<X.Y.Z>`.

### 4. Promote the CHANGELOG `## [Unreleased]` section

`## [Unreleased]` is the **already-curated** changelog (every change adds its entry as it's made), so this step **promotes** it — it does **not** regenerate entries from commits:

- Read the current `## [Unreleased]` bullets. If it is **empty**, **stop and warn the user** — the player changelog would be lost (something was merged without honouring the CHANGELOG guardrail); offer to reconstruct from the commit log only as a fallback.
- Rename `## [Unreleased]` to `## [<X.Y.Z>] - <YYYY-MM-DD>` (today, ISO), keeping its bullets; drop any empty `### Added` / `### Changed` / `### Fixed` subsections.
- Insert a fresh empty `## [Unreleased]` block (with the three subsection headers) above it.

### 5. Bump `bundleVersion`

Set the app version through the Editor (this writes `ProjectSettings/ProjectSettings.asset` cleanly and keeps the AssetDatabase consistent) — run via the `script-execute` MCP skill:

```csharp
UnityEditor.PlayerSettings.bundleVersion = "<X.Y.Z>";
UnityEditor.AssetDatabase.SaveAssets();
```

Confirm `bundleVersion: <X.Y.Z>` in `ProjectSettings/ProjectSettings.asset` afterwards.

### 6. Build both targets locally + zip

Build into the git-ignored `Builds/` folder. **Build Windows first (fast), then WebGL (slow).** Run each build via `script-execute`; `BuildPipeline.BuildPlayer` blocks the Editor's main thread, so the call returns the `BuildReport` when the build finishes. Use this helper (adjust the scene list if `EditorBuildSettings` is empty — morBreaker's single scene is `Assets/Scenes/SampleScene.unity`):

```csharp
// Parameter `target`: "windows" or "webgl". Returns the result + output path.
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

string which = "windows"; // ← set per call
var scenes = new[] { "Assets/Scenes/SampleScene.unity" };
string root = Path.Combine(Directory.GetCurrentDirectory(), "Builds");
Directory.CreateDirectory(root);

BuildTarget target; BuildTargetGroup group; string outPath; string locationPath;
if (which == "windows") {
    target = BuildTarget.StandaloneWindows64; group = BuildTargetGroup.Standalone;
    outPath = Path.Combine(root, "windows"); Directory.CreateDirectory(outPath);
    locationPath = Path.Combine(outPath, "morBreaker.exe");
} else {
    target = BuildTarget.WebGL; group = BuildTargetGroup.WebGL;
    outPath = Path.Combine(root, "webgl"); locationPath = outPath;
}

if (EditorUserBuildSettings.activeBuildTarget != target)
    EditorUserBuildSettings.SwitchActiveBuildTarget(group, target);

var opts = new BuildPlayerOptions {
    scenes = scenes, locationPathName = locationPath, target = target, options = BuildOptions.None
};
BuildReport report = BuildPipeline.BuildPlayer(opts);
Debug.Log($"BUILD {which}: {report.summary.result} → {locationPath}");
```

- **Verify each build succeeded** (`report.summary.result == Succeeded`, surfaced in `console-get-logs` / the `script-execute` result). If a build **fails**, stop and surface the reason — nothing has been tagged or pushed yet.
- **WebGL build time vs the MCP call window:** the WebGL build can take several minutes. If the `script-execute` call returns before the build is done (timeout) while the build continues, **poll** until the output appears — check `console-get-logs` for the `BUILD webgl: Succeeded` line and that `Builds/webgl/index.html` exists — before proceeding. Do **not** re-trigger a second build on top of a running one.
- **Switching back:** after both builds, optionally switch the active target back to what it was before (a convenience; not required).
- **Zip each output** (PowerShell `Compress-Archive`; the Windows ZIP is the full player folder, the WebGL ZIP is the build folder contents so it can be dropped straight onto a web page):
  ```powershell
  $v = "<X.Y.Z>"
  Compress-Archive -Path Builds/windows/* -DestinationPath "Builds/morBreaker-v$v-windows.zip" -Force
  Compress-Archive -Path Builds/webgl/*   -DestinationPath "Builds/morBreaker-v$v-webgl.zip"   -Force
  ```

### 7. Reconcile the release's issues (`awaiting-release`)

Every issue worked through **`work-issue`** is stamped **`awaiting-release`** (both flows). The label (not commit text) is the source of truth.

1. **Collect the set:**
   ```powershell
   gh issue list --label awaiting-release --state all --json number,title,state,url
   ```
   If empty, note "no issues to reconcile" and continue.
2. **For each issue:** if still `OPEN`, close it with a comment referencing this release; then remove the marker label:
   ```powershell
   gh issue close <n> --comment "Released in v<X.Y.Z>."
   gh issue edit  <n> --remove-label awaiting-release
   ```
   (Already-`CLOSED` issues — those auto-closed by their own squash-merge — get only the label removal.)
3. **Print a summary table** in the release report: each issue → was it open? → action taken. Surface any `gh` failures rather than swallowing them.

### 8. Commit, tag, push

```powershell
git add CHANGELOG.md ProjectSettings/ProjectSettings.asset
git commit -m "chore(release): v<X.Y.Z>" -m "<one-line summary of what's in this release>" -m "Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
git tag -a "v<X.Y.Z>" -m "v<X.Y.Z>"
git push origin main
git push origin "v<X.Y.Z>"
```

(The `Builds/` ZIPs are git-ignored — they are not committed; they are uploaded as release assets in step 9.)

### 9. Create the GitHub Release with both ZIPs

Extract the just-promoted CHANGELOG section as the release notes, then create the release with both artifacts attached:

```powershell
$v = "<X.Y.Z>"
# Pull the "## [X.Y.Z]" section body out of CHANGELOG.md into notes.md
$notes = @()
$inSection = $false
foreach ($line in Get-Content CHANGELOG.md) {
  if ($line -match "^## \[$([regex]::Escape($v))\]") { $inSection = $true; continue }
  elseif ($inSection -and $line -match "^## \[") { break }
  elseif ($inSection) { $notes += $line }
}
($notes -join "`n").Trim() | Set-Content notes.md -Encoding utf8

gh release create "v$v" `
  --title "v$v" `
  --notes-file notes.md `
  "Builds/morBreaker-v$v-windows.zip" `
  "Builds/morBreaker-v$v-webgl.zip"
Remove-Item notes.md
```

If `notes.md` is empty (shouldn't happen — step 4 guards an empty `## [Unreleased]`), fall back to `--generate-notes`.

### 10. Verify & report

- Confirm the release and its two assets exist:
  ```powershell
  gh release view "v<X.Y.Z>" --json tagName,assets --jq '{tag:.tagName, assets:[.assets[].name]}'
  ```
  Expect both `morBreaker-v<X.Y.Z>-windows.zip` and `morBreaker-v<X.Y.Z>-webgl.zip`.
- **Report** the version, the tag, the release URL, the two attached assets, and the issue-reconciliation summary from step 7. Remind the user that embedding the WebGL build on a web page is a separate manual step (download the WebGL ZIP from the release).

## Notes

- **Never deploy to a server from this skill** — it versions, builds locally, tags, and publishes a GitHub Release. No FTP, no API, no third party (the project's no-network constraint).
- Honour the privacy guardrails in the commit/tag/CHANGELOG/release text — no secrets, no personal data.
- The version-bump + CHANGELOG commit doesn't change gameplay, so the green gate result from step 1 stays valid for the tagged tree.
- If a build module is missing or a build fails, stop and report — do not tag a release whose artifacts couldn't be produced.
