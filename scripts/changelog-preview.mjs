#!/usr/bin/env node
// ─────────────────────────────────────────────────────────────────────────────
// changelog-preview.mjs — Conventional-Commits → Keep-a-Changelog preview + lint.
//
// Reads the commits a PR adds (BASE_SHA..HEAD_SHA), parses them as Conventional
// Commits, groups them into Keep-a-Changelog sections, and writes a Markdown
// preview (changelog-preview.md) of what the PR would add under "## [Unreleased]".
// It also lints every commit subject and reports how many fail the convention.
//
// Dependency-free: uses only Node built-ins + the `git` already on the runner.
// No third-party actions, no npm install (keeps the free-resources guardrail clean).
//
// Outputs (GitHub Actions): `violations=<n>` on $GITHUB_OUTPUT.
// Always exits 0 — the workflow decides whether to fail the check on violations,
// so the preview comment is posted either way.
// ─────────────────────────────────────────────────────────────────────────────
import { execFileSync } from "node:child_process";
import { writeFileSync, appendFileSync } from "node:fs";

const US = "\x1f", RS = "\x1e"; // unit / record separators (safe field delimiters)

const base = process.env.BASE_SHA || "origin/main";
const head = process.env.HEAD_SHA || "HEAD";

// Pull the PR's own commits (exclude merge commits — they aren't change records).
let raw = "";
try {
  raw = execFileSync(
    "git",
    ["log", "--no-merges", `--format=%H${US}%s${US}%b${RS}`, `${base}..${head}`],
    { encoding: "utf8", maxBuffer: 64 * 1024 * 1024 }
  );
} catch (e) {
  console.error("git log failed:", e.message);
}

const records = raw.split(RS).map((r) => r.trim()).filter(Boolean);

// type → Keep-a-Changelog section. Types not listed are "internal" (shown, but
// flagged as not user-facing so they can be filtered out of the product changelog).
const SECTION = {
  feat: "Added",
  fix: "Fixed",
  perf: "Changed",
  refactor: "Changed",
  revert: "Changed",
  build: "Changed",
};
const INTERNAL = new Set(["docs", "style", "test", "ci", "chore"]);
const TYPES = [...Object.keys(SECTION), ...INTERNAL];

const RE = new RegExp(
  `^(?<type>${TYPES.join("|")})(?<scope>\\([a-z0-9 ,_./-]+\\))?(?<bang>!)?: (?<desc>.+)$`
);

const sections = { Breaking: [], Added: [], Changed: [], Fixed: [], Internal: [] };
const violations = [];

for (const rec of records) {
  const [sha = "", subject = "", body = ""] = rec.split(US);
  const short = sha.slice(0, 7);
  const m = RE.exec(subject.trim());
  if (!m) {
    violations.push({ short, subject: subject.trim() });
    continue;
  }
  const { type, scope, bang, desc } = m.groups;
  const breaking = Boolean(bang) || /^BREAKING[ -]CHANGE:/m.test(body);
  const scopeTxt = scope ? ` ${scope}` : "";
  const line = `- **${type}${scopeTxt}:** ${desc} (\`${short}\`)`;
  if (breaking) sections.Breaking.push(line);
  if (INTERNAL.has(type)) sections.Internal.push(line);
  else sections[SECTION[type]].push(line);
}

// ── Render the preview Markdown ──────────────────────────────────────────────
const MARKER = "<!-- changelog-preview -->";
const out = [MARKER, "## 📓 Changelog preview", ""];

const total = records.length;
if (total === 0) {
  out.push("_No commits found for this PR yet._");
} else {
  out.push(
    `These are the **${total}** commit(s) this PR adds, grouped into the entries ` +
      "they would contribute to `## [Unreleased]` in `CHANGELOG.md`:",
    ""
  );
  const order = ["Breaking", "Added", "Changed", "Fixed", "Internal"];
  const labels = {
    Breaking: "⚠️ Breaking (forces a **major** bump)",
    Added: "### Added",
    Changed: "### Changed",
    Fixed: "### Fixed",
    Internal: "<details><summary>Internal (not user-facing)</summary>",
  };
  let any = false;
  for (const key of order) {
    const items = sections[key];
    if (!items.length) continue;
    any = true;
    out.push(labels[key], "", ...items, "");
    if (key === "Internal") out.push("</details>", "");
  }
  if (!any) out.push("_No conventional entries parsed._", "");
}

if (violations.length) {
  out.push(
    "---",
    "",
    `### ❌ ${violations.length} commit(s) do not follow Conventional Commits`,
    "",
    "Fix these subjects (e.g. `feat(scope): …`, `fix: …`) — this check is **blocking**:",
    "",
    ...violations.map((v) => `- \`${v.short}\` — ${v.subject || "(empty subject)"}`),
    ""
  );
}

out.push(
  "---",
  "",
  "<sub>Auto-generated from the PR's commits. Edits land in `## [Unreleased]`; " +
    "the version is stamped at release time.</sub>"
);

writeFileSync("changelog-preview.md", out.join("\n") + "\n", "utf8");

// ── Report violation count to the workflow ───────────────────────────────────
const n = String(violations.length);
if (process.env.GITHUB_OUTPUT) appendFileSync(process.env.GITHUB_OUTPUT, `violations=${n}\n`);
console.log(`Parsed ${total} commit(s); ${n} violation(s). Wrote changelog-preview.md`);
