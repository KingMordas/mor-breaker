---
name: work-issue
description: Fetch a GitHub issue by number and work it end-to-end on morBreaker — optionally branch from origin/main, analyse the issue (attachments/comments), confirm CLAUDE.md guardrail compliance, implement via the Unity MCP skills, visually verify any UI/gameplay change, and (only when branching) commit/push, open a PR, wait for the changelog-preview check, then (after the user confirms) squash-merge it to main. The test + security gates run at /release time, not per-PR. Use when the user invokes "/work-issue <issue#>" (defaults to new branch + PR + confirm-then-merge) or "/work-issue <issue#> false" (work in place, no commit).
---

# Work issue

Drive a single GitHub issue from the current repository through to either a working-tree implementation (no branch) or a pushed branch + PR that — **once the user confirms the merge** — is **squash-merged to `main`** (new branch). Ask the user whenever there is genuine doubt, and keep the issue updated as the activity progresses.

> **CI model (read this).** To conserve Actions minutes, the **security** (`security.yml`) workflow and the **Unity tests** run **at `/release` time**, not on PRs. The **only** workflow that runs on a PR to `main` is **`changelog-preview.yml`** (Conventional-Commit lint + CHANGELOG-update gate — the `preview` check). So this skill waits for that single check, then **asks the user to confirm** and **squash-merges** the PR (delete branch). `main` is configured **squash-only** and **branch-protected** to require the `preview` check.
>
> **Merge requires explicit human confirmation (load-bearing).** The harness auto-mode classifier **blocks an agent from self-merging a PR it authored** without specific human approval, so the merge step (7.5) **must ask the user to confirm before running `gh pr merge`** — do not attempt an unattended merge.

## Inputs

Invocation: `/work-issue <issue#> [true|false]` — e.g. `/work-issue #1` (branch + PR, the default) or `/work-issue #1 false` (work in place, no commit).

- `$1` — the **issue number**. May arrive as `#1` or `1`; strip a leading `#`. If it is missing or not a positive integer, stop and ask the user for a valid issue number.
- `$2` — the **new-branch flag**, a bool. Parse case-insensitively: `true`/`false` (treat `1`/`yes` as true, `0`/`no` as false). **If missing or unparseable, default to `true`** (create a new branch + PR) — do not ask.

Throughout, call this `ISSUE` and `NEW_BRANCH`.

**Repo context (resolve dynamically — never hardcode the owner/repo).** This skill operates on **the GitHub repository this working tree's `origin` remote points to**, so a fork needs no edits. Resolve it once with the `gh` CLI and reuse it; pass `--repo "$REPO"` to **every** `gh` call so operations can only ever touch this repo (never a fork's `upstream`/parent or any other repo on the account). The default branch is `main`. Use `@me` wherever the current GitHub user is needed (e.g. PR assignee) — do not hardcode a handle:

```powershell
$originUrl = git remote get-url origin
$REPO = gh repo view $originUrl --json nameWithOwner --jq .nameWithOwner   # e.g. "owner/name"
$REPO_URL = gh repo view $originUrl --json url --jq .url                   # e.g. "https://github.com/owner/name"
```

Use the `gh` CLI for all GitHub operations. If `gh` is not authenticated (or `origin` is missing), report that and stop.

## Steps

### 1. Fetch the issue

```powershell
gh issue view <ISSUE> --repo "$REPO" --json number,title,state,body,labels,assignees,comments,url
```

- If the issue does not exist, **inform the user that issue #<ISSUE> was not found and stop** — take no further action (no branch, no comment, nothing).
- Otherwise continue. Fetch the full comment thread and note any attachment URLs (images, files); read them so the analysis in step 4 is complete.

### 2. Decide the branch (driven by `NEW_BRANCH`)

**If `NEW_BRANCH` is false** — use the **current** active branch (`git branch --show-current`). Do not switch or create anything.

**If `NEW_BRANCH` is true** — create a new branch from `origin/main`:

1. **Check for uncommitted changes first** (`git status --porcelain`). If the working tree is **dirty**, **stop and ask the user how to proceed** before touching branches. Do not move their work without an answer. (Note: a Unity Editor session may leave `.meta`/`Library` churn — confirm what is genuinely uncommitted.)
2. Once clean (or directed), fetch and create:
   ```powershell
   git fetch origin
   git checkout -b <branch-name> origin/main
   ```
3. **You choose the branch name** from the issue — short, kebab-case, ASCII, prefixed with the issue number, e.g. `issue-<ISSUE>-<short-slug>`.

### 3. Record the branch on the issue + mark it in progress

**In both flows:**

1. **Post a branch breadcrumb comment** — e.g. *"Working this issue on branch `<branch-name>` (newly created from `origin/main`)"* or *"…on the existing branch `<branch-name>`"*. This comment **is** the branch↔issue association record (GitHub's formal Development-panel link can't link a pre-existing branch).
2. **Stamp the `in-progress` label** (both flows). `awaiting-release` is **not** set here — only at the very end (step 7).

```powershell
gh issue comment <ISSUE> --repo "$REPO" --body "<branch info>"
gh label create in-progress --repo "$REPO" --description "Actively being worked via /work-issue; cleared when queued for release" --color 0E8A16 2>$null
gh issue edit <ISSUE> --repo "$REPO" --add-label in-progress
```

### 4. Analyse & clarify

Analyse the issue thoroughly: body, **all comments**, **all attachments**. Determine the concrete work and which scripts/scene/assets it touches (the game lives in `Assets/Scripts/`, namespace `MorBreaker`, plus `Assets/Scenes/SampleScene.unity` — see `CLAUDE.md` → *Architecture*).

- If anything is **ambiguous**, ask the user clarifying questions (AskUserQuestion; surface trade-offs + a recommendation).
- After they answer, post **one single comment** to the issue summarising the Q&A. Do not spam one comment per question. If there were no questions, skip this comment.

### 5. Guardrail compliance check (CLAUDE.md)

Before implementing, verify the requested work is **compliant with `CLAUDE.md`** — its project constraints and gotchas:

- **Free resources only** — no paid dependency/asset/service.
- **No copyright/trademark** — only original assets, names, code (no Arkanoid sprites/sounds/level layouts or trademarked names).
- **No data collection** — no analytics/telemetry/network; all game state in memory; every *gameplay* script's doc comment asserts *"Stores no data of any kind"* (preserve it). The **only** persisted data is the device-local `Leaderboard` (localStorage on WebGL, in-memory otherwise) — never networked.
- **Both build targets** — the change must build & run on **Windows (Standalone x86_64)** and **WebGL**.
- **Input System only**; **kinematic walls/paddle**; the **sprite-import / auto-fit collider / ball-containment / runInBackground** gotchas (see `CLAUDE.md` → *Critical gotchas*).
- **Decoupled via static C# events** — raise an event rather than reaching across to `GameManager`.

If there is a **discrepancy** between the issue and CLAUDE.md, **ask the user how to proceed**, then post a comment stating the discrepancy and the decision. Only then continue.

### 6. Implement

Do the work through the **Unity MCP skills** (preferred over hand-editing `.cs`/`.unity`/`.asset`/`.prefab` — they go through the live Editor, keep the AssetDatabase consistent, and validate C# via Roslyn): `script-read` / `script-update-or-create` / `script-delete` for code; `scene-open` / `scene-get-data` / `gameobject-*` for the scene; `assets-*` for assets; `assets-refresh` to force recompilation. Follow `CLAUDE.md` to the letter, and update `README.md` / `CLAUDE.md` when the change warrants it.

**For any user-visible change, add an entry under `## [Unreleased]` in `CHANGELOG.md` in this same change** — plain, player-facing English under the right `### Added` / `### Changed` / `### Fixed` subsection. The per-PR `changelog-preview` check enforces this. **Decide now** whether the change is **changelog-exempt** — *purely* internal with no symptom a player could ever notice (a CI tweak, a test-only refactor): if exempt, **don't** add a CHANGELOG entry and instead create the PR **with** the `skip-changelog` label (see the timing trap below); if user-visible, add the entry and do **not** use the label.

> **Visual verification (mandatory when the change adds or alters anything the player sees or how the game plays).** Following the gotchas is necessary but not sufficient — **actually look at the result** before wrapping up. Use the Unity MCP run/observe skills: ensure no compile errors (`console-get-logs`), save the scene, **enter play mode** (`editor-application-set-state`; set `Application.runInBackground = true` so the headless loop isn't frozen — see the gotcha), drive the relevant flow, and capture **`screenshot-game-view`** (and `console-get-logs`) to confirm it renders/behaves correctly. If a screenshot or log reveals a problem, **fix it and re-capture until it's right** before committing. Exit play mode when done. Skip this only for purely non-visual, non-gameplay work (docs, CI, pure-data refactors); when in doubt, run it. Note what you verified (scenes/flows checked) — it feeds the PR's `## Testing` section.

If the change touches logic covered by tests, run the **relevant** tests via `tests-run` (EditMode/PlayMode) — **precondition: all open scenes must be saved** or the run aborts. Add a test where the change introduces intricate logic worth guarding; favour quality over quantity. The full suite is the `/release` gate's job, not yours.

> **`skip-changelog` timing trap (load-bearing).** The CI gate reads the label from the **`pull_request` event payload captured when the PR is *opened***. Adding the label *after* `gh pr create` does **not** re-evaluate the check, so it stays red. An exempt PR **must be created *with* the label already on it** — pass `--label skip-changelog` to `gh pr create` (step 7), never add it as a follow-up.

### 7. Wrap up — branches diverge here

**Mark it for release (both flows, only now).** The work is complete and verified, so flip the labels: **add `awaiting-release`** (the machine-readable marker `/release` uses to reconcile + close issues) and **remove `in-progress`**. Apply this **at the very end** — for the `true` flow, **only after the PR has been merged to `main`** (the user confirmed and the squash-merge succeeded); for the `false` flow, after posting the final comment:

```powershell
gh label create awaiting-release --repo "$REPO" --description "Worked and queued for the next release; reconciled+closed by /release" --color FBCA04 2>$null
gh issue edit <ISSUE> --repo "$REPO" --add-label awaiting-release --remove-label in-progress
```

**If `NEW_BRANCH` is false** — **do NOT commit, push, or open a PR.** Leave the changes in the working tree. Then:
- Inform the user the work is complete and that nothing was committed/pushed (per the input flag).
- Post a final comment summarising what was done (files/scenes changed, key decisions, that the changes sit uncommitted on branch `<branch-name>`).
- Flip the labels (`+awaiting-release` / `-in-progress`).

**If `NEW_BRANCH` is true** — commit, push, open a PR, and (after the user confirms) squash-merge it to `main`:

1. **Commit** with a structured, meaningful **Conventional Commits** message — never a bare one-liner. Follow the format in `CLAUDE.md` → *Commits, changelog & release*:
   - **Subject** = `type(scope): description` — imperative, ~50 chars, no trailing period (e.g. `feat(ball): cap per-level speed ramp`). `type` ∈ `feat`/`fix`/`docs`/`style`/`refactor`/`perf`/`test`/`build`/`ci`/`chore`; `scope` names the area (`ball`, `paddle`, `brick`, `spawner`, `gamemanager`, `leaderboard`, `level`, `ui`, `build`, …). Breaking → `type(scope)!:` and/or a `BREAKING CHANGE:` footer.
   - **Blank line**, then a **body** explaining *what* changed **and why** (bullets for several items), flagging guardrail-relevant touches (new dependency, privacy/data-flow change, gameplay doc-comment edit, doc updates).
   - **Blank line**, then `Closes #<ISSUE>`, then the co-author trailer:
   ```
   <type>(<scope>): <imperative description, ~50 chars>

   <body: what changed and why; bullets for multiple items;
   note any data-flow / dependency / doc / gameplay-doc-comment touches>

   Closes #<ISSUE>

   Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>
   ```
   Note: a Unity change may touch generated `.meta` files and `ProjectSettings` — stage the genuinely-relevant files; don't sweep in `Library/`/`Temp/` churn (the `.gitignore` already excludes them).
2. **Push**: `git push -u origin <branch-name>`.
3. **Open a PR to `main`**, assigning the current user (`@me`) and linking the issue so merging auto-closes it. **The PR title must itself be a Conventional Commit subject** (the *same* subject as the commit in 7.1) — the squash-merge in 7.5 uses it as the commit landed on `main`, and **`/release` parses those `main` subjects to compute the SemVer bump**, so a non-conventional title would silently break the bump. **If you decided the change is changelog-exempt, create the PR *with* the `skip-changelog` label** (`--label skip-changelog`); never add it afterwards (timing trap). Omit `--label` when the change is user-visible (you added a CHANGELOG entry instead):
   ```powershell
   gh label create skip-changelog --repo "$REPO" --description "Change has no player-facing effect; exempt from the CHANGELOG gate" --color ededed 2>$null  # only when exempt
   gh pr create --repo "$REPO" --base main --head <branch-name> `
     --title "<type(scope): imperative description — SAME subject as the commit>" `
     --body "$body" `
     --assignee @me `
     --label skip-changelog   # ← include ONLY for a changelog-exempt change; omit otherwise
   ```
   Compose `$body` (here-string) with: **`## Summary`** (what & why), **`## Changes`** (bullets of changed scripts/scene/assets + decisions, mirroring guardrail touches), **`## Testing`** (what was verified — compile clean, play-mode screenshot/flow, any tests run, or an explicit note it wasn't runtime-verified and why), then a final **`Closes #<ISSUE>`**.
4. Post a comment on the issue with the PR link and a summary.
5. **Wait for the changelog-preview check, then confirm + merge.** The **only** check on this PR is **`changelog-preview`** (the `preview` check). Watch it:
   ```powershell
   gh pr checks <PR#> --repo "$REPO" --watch --interval 20
   ```
   - **If it passes** — **ask the user to confirm the merge** (AskUserQuestion: "Merge PR #<PR#> to `main`?"). This is the deliberate human gate; the harness blocks self-merge without approval. **Do not run `gh pr merge` before the user confirms.** Once approved, squash-merge, delete the branch, with an explicit Conventional-Commit subject:
     ```powershell
     gh pr merge <PR#> --repo "$REPO" --squash --delete-branch --subject "<type(scope): description — same subject as the commit/PR title>"
     ```
     If the user **declines**, stop and leave the PR open (do **not** flip labels — the work isn't on `main` yet); report that it awaits their merge.
   - **If it fails** — `changelog-preview` opens **no** bug issue; it fails only on a **non-conventional commit** or a **missing `## [Unreleased]` CHANGELOG entry** (or a mis-set `skip-changelog` label). Fix the cause on **this same branch**, `git push --force-with-lease` if you amended, and **re-watch**. **Bound to ~3 attempts**; if it still fails, **stop and ask the user** rather than looping or merging red.
6. **Flip the original issue's labels** (`+awaiting-release` / `-in-progress`) — **only now, with the PR merged**.
7. **Return the local checkout to `main` and prune the merged branch:**
   ```powershell
   git checkout main
   git pull origin main
   git branch -D <branch-name>
   ```
   If any step fails (e.g. an unexpected dirty tree from the Editor), report it rather than forcing it — the work is already merged.
8. Inform the user the issue's PR was **merged to `main`** (squashed, branch deleted) and that the change ships with the next **`/release`** (which runs the security + test gates and builds the artifacts).

## Notes

- All GitHub writes go through `gh`. The co-author trailer follows the repo's git conventions.
- Honour the privacy guardrails when writing commit messages, comments, and logs — never include secrets or personal data.
- Prefer the Unity MCP skills over hand-editing Unity YAML / C#. After any external `.cs` change, `assets-refresh` to force recompilation and check `console-get-logs` for errors before considering the work done.
- If `gh` is not authenticated, report that and stop rather than failing mid-way.
