---
name: list-issues
description: List all open GitHub issues of morBreaker in a readable overview table — ordered by open date ascending, with bug-labelled issues first as high priority. Use when the user invokes "/list-issues" or asks for an overview of the open issues.
---

# List issues

Produce a single, readable overview table of **all open** GitHub issues in the current repository, so the user can see the whole backlog at a glance.

**Repo context (resolve dynamically — never hardcode the owner/repo).** This skill operates on **the GitHub repository this working tree's `origin` remote points to**, so a fork needs no edits. Resolve it once with the `gh` CLI and reuse it; pass `--repo "$REPO"` to **every** `gh` call so operations can only ever touch this repo (never a fork's `upstream`/parent or any other repo on the account):

```powershell
$originUrl = git remote get-url origin
$REPO = gh repo view $originUrl --json nameWithOwner --jq .nameWithOwner   # e.g. "owner/name"
$REPO_URL = gh repo view $originUrl --json url --jq .url                   # e.g. "https://github.com/owner/name"
```

Use the `gh` CLI for all GitHub operations. If `gh` is not authenticated (or `origin` is missing), report that and stop.

## Steps

### 1. Fetch the open issues

Run:

```powershell
gh issue list --repo "$REPO" --state open --limit 1000 --json number,title,labels,assignees,createdAt,url
```

- If `gh` is not authenticated, report that and stop rather than failing mid-way.
- If there are **no open issues**, tell the user plainly ("No open issues in `$REPO`.") and stop — do not render an empty table.

### 2. Order the issues

Sort the issues with this two-level ordering:

1. **Priority first** — issues carrying the **`bug`** label (case-insensitive match in their `labels`) come **before** all non-bug issues. Treat bug-labelled issues as high priority.
2. **Within each group**, order by **open date ascending** (`createdAt` oldest → newest).

So the result is: all bugs (oldest → newest), then all non-bugs (oldest → newest).

### 3. Render the overview table

Output a GitHub-flavored markdown table with these columns, in order:

| Column | Content |
| --- | --- |
| **#** | The issue number as a clickable markdown link to its `url` (the `url` field returned by `gh`, e.g. `[#12]($REPO_URL/issues/12)`). |
| **Priority** | `🐛 Bug` for bug-labelled issues, otherwise `—`. |
| **Title** | The issue title. |
| **Labels** | All labels, comma-separated (or `—` if none). |
| **Assignees** | Assignee logins, comma-separated (or `Unassigned` if none). |
| **Opened** | The `createdAt` date rendered as `yyyy-MM-dd`, plus a relative age in parentheses (e.g. `2026-05-01 (49 days ago)`). |

- Keep the table compact and readable; do not include the body.
- Above the table, print a one-line summary: the total open count and how many are bugs (e.g. *"7 open issues — 2 bugs (high priority), 5 other."*).
- Below the table, if any bugs exist, add a brief note that bug issues are listed first as high priority.

## Notes

- This skill is **read-only** — it never creates branches, comments, or modifies anything on GitHub or in the working tree.
- Honour the privacy guardrails: surface only what is already public on the issues; never add secrets or personal data.
