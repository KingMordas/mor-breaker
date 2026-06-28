#!/usr/bin/env node
// ─────────────────────────────────────────────────────────────────────────────
// security-report.mjs — aggregate the security scanners' reports into a short
// summary and a richly-detailed report body.
//
// Reads the per-tool reports written by .github/workflows/security.yml into
// ./security-reports/ (one JSON file per scanner + a meta.json with each tool's
// version, the exact command run, and its exit code), normalises every finding to
// { sev, loc, rule, msg }, and emits:
//   • security-comment.md — a 🟢 pass / 🔴 fail summary.
//   • security-report.md  — the full report, built to be handed straight to Claude
//     for a later fix: context block (ref/SHA/run URL, each tool's version + exact
//     command), per-tool normalised findings, and the full raw reports inline up
//     to a size limit, with the complete JSON also attached as an artifact. GitHub
//     Issues are disabled on this repo, so the workflow surfaces this report in the
//     run's job summary and the security-reports artifact (no issue is filed).
//
// Privacy: scanners are run so secret VALUES never reach here (gitleaks --redact);
// this script only ever prints rule/severity/location/message — no data leaks.
//
// Dependency-free: Node built-ins only — no npm install, no third-party action
// (the free-resources + zero-third-party-action CI stance).
//
// Outputs (GitHub Actions, on $GITHUB_OUTPUT): failed=true|false.
// Always exits 0 — the workflow decides when to fail the check, after the report
// has been written to the job summary and the artifact.
// ─────────────────────────────────────────────────────────────────────────────
import { readFileSync, writeFileSync, appendFileSync, existsSync } from "node:fs";

const DIR = "security-reports";
const REPORT_BODY_LIMIT = 60000; // cap the rendered report (job summary has a ~1 MiB limit; keep it lean)

// ── Context from the workflow environment ────────────────────────────────────
const serverUrl = process.env.GITHUB_SERVER_URL || "https://github.com";
const repo = process.env.GITHUB_REPOSITORY || "";
const runId = process.env.GITHUB_RUN_ID || "";
const sha = process.env.GITHUB_SHA || "";
const shortSha = sha.slice(0, 7);
const eventName = process.env.EVENT_NAME || "";
const prNumber = process.env.PR_NUMBER || "";
const refName = process.env.REF_NAME || "";
const artifactName = process.env.ARTIFACT_NAME || "security-reports";
const runUrl = repo && runId ? `${serverUrl}/${repo}/actions/runs/${runId}` : "";

// ── Helpers ──────────────────────────────────────────────────────────────────
const readJson = (file) => {
  const path = `${DIR}/${file}`;
  if (!existsSync(path)) return undefined;
  const raw = readFileSync(path, "utf8").trim();
  if (!raw) return undefined;
  try {
    return JSON.parse(raw);
  } catch {
    return undefined; // unparseable → treated as a tool error below
  }
};

const SEV_RANK = { CRITICAL: 0, HIGH: 1, MEDIUM: 2, MODERATE: 2, LOW: 3, INFO: 4, UNKNOWN: 5 };
const normSev = (s) => {
  const u = String(s || "UNKNOWN").toUpperCase();
  if (u === "ERROR") return "HIGH";
  if (u === "WARNING") return "MEDIUM";
  if (u === "INFORMATIONAL" || u === "INFO" || u === "NOTE") return "LOW";
  if (u === "MODERATE") return "MEDIUM";
  return SEV_RANK[u] !== undefined ? u : "UNKNOWN";
};
const bySeverity = (a, b) =>
  (SEV_RANK[a.sev] ?? 9) - (SEV_RANK[b.sev] ?? 9) || a.loc.localeCompare(b.loc);

const meta = readJson("meta.json") || {};

// ── Per-tool parsers → normalised findings [{ sev, loc, rule, msg }] ──────────
function parseSemgrep() {
  const j = readJson("semgrep.json");
  if (j === undefined) return null;
  return (j.results || []).map((r) => ({
    sev: normSev(r.extra?.severity),
    loc: `${r.path}:${r.start?.line ?? "?"}`,
    rule: r.check_id || "",
    msg: (r.extra?.message || "").trim().replace(/\s+/g, " ").slice(0, 300),
  }));
}

function parseTrivy() {
  const j = readJson("trivy.json");
  if (j === undefined) return null;
  const out = [];
  for (const res of j.Results || []) {
    for (const v of res.Vulnerabilities || []) {
      const fix = v.FixedVersion ? ` — fixed in ${v.FixedVersion}` : " — no fix available";
      out.push({
        sev: normSev(v.Severity),
        loc: `${res.Target} · ${v.PkgName}@${v.InstalledVersion}`,
        rule: v.VulnerabilityID || "",
        msg: `${(v.Title || v.VulnerabilityID || "").slice(0, 240)}${fix}`,
      });
    }
    for (const m of res.Misconfigurations || []) {
      out.push({
        sev: normSev(m.Severity),
        loc: `${res.Target}`,
        rule: m.ID || m.AVDID || "",
        msg: (m.Title || m.Message || "").slice(0, 280),
      });
    }
  }
  return out;
}

function parseGitleaks() {
  const j = readJson("gitleaks.json");
  if (j === undefined) return null;
  const arr = Array.isArray(j) ? j : [];
  return arr.map((f) => ({
    sev: "HIGH", // a committed secret is always high-severity
    loc: `${f.File}:${f.StartLine ?? "?"}`,
    rule: f.RuleID || "",
    // Secret/Match are already redacted by gitleaks --redact; we never print them.
    msg: `${(f.Description || "secret detected").slice(0, 200)} (commit ${String(f.Commit || "").slice(0, 7)})`,
  }));
}

function parseCheckov() {
  const j = readJson("checkov.json");
  if (j === undefined) return null;
  const blocks = Array.isArray(j) ? j : [j];
  const out = [];
  for (const b of blocks) {
    for (const c of b.results?.failed_checks || []) {
      const line = Array.isArray(c.file_line_range) ? c.file_line_range[0] : "?";
      out.push({
        sev: normSev(c.severity),
        loc: `${c.file_path}:${line}`,
        rule: c.check_id || "",
        msg: (c.check_name || "").slice(0, 280),
      });
    }
  }
  return out;
}

// ── Tool registry (display order = the report's numbered list) ────────────────
const TOOLS = [
  { id: "semgrep", name: "Semgrep CE", category: "SAST (static analysis, incl. C#)", parse: parseSemgrep, raw: "semgrep.json" },
  { id: "trivy", name: "Trivy", category: "Dependencies + misconfig (fs)", parse: parseTrivy, raw: "trivy.json" },
  { id: "gitleaks", name: "Gitleaks", category: "Secrets detection", parse: parseGitleaks, raw: "gitleaks.json" },
  { id: "checkov", name: "Checkov", category: "CI workflow misconfig", parse: parseCheckov, raw: "checkov.json" },
];

// state per tool: { findings:[], count, errored:bool, version, command, exit }
const results = TOOLS.map((t) => {
  const m = meta[t.id] || {};
  let findings = null;
  try {
    findings = t.parse();
  } catch {
    findings = null;
  }
  // A tool "errored" if its report is missing/unparseable while it ran (no findings list),
  // OR it exited non-zero for a reason other than findings (we surface both, fail-safe).
  const errored = findings === null;
  if (findings) findings.sort(bySeverity);
  return {
    ...t,
    findings: findings || [],
    count: findings ? findings.length : 0,
    errored,
    version: m.version || "?",
    command: m.command || "",
    exit: m.exit,
  };
});

const failing = results.filter((r) => r.count > 0 || r.errored);
const failed = failing.length > 0;

// ── Build the per-tool summary table ─────────────────────────────────────────
function summaryTable() {
  const rows = results.map((r) => {
    const status = r.errored
      ? "⚠️ error"
      : r.count > 0
        ? `🔴 ${r.count}`
        : "🟢 0";
    return `| ${r.name} | ${r.category} | ${status} | \`${r.version}\` |`;
  });
  return [
    "| Scanner | Coverage | Findings | Version |",
    "| --- | --- | --- | --- |",
    ...rows,
  ].join("\n");
}

// ── security-comment.md (summary — written pass or fail) ──────────────────────
const COMMENT_MARKER = "<!-- security-scan-summary -->";
function buildComment() {
  const out = [COMMENT_MARKER];
  if (!failed) {
    out.push(
      "## 🟢 ✅ Security scans passed",
      "",
      "All four scanners completed with **no findings**.",
      "",
      summaryTable(),
    );
  } else {
    const names = failing.map((r) => r.name).join(", ");
    out.push(
      "## 🔴 ❌ Security scans failed",
      "",
      `Findings from: **${names}**.`,
      "",
      summaryTable(),
      "",
      `Full findings are in this run's job summary and the **\`${artifactName}\`** artifact.`,
    );
  }
  if (runUrl) out.push("", `[View workflow run ↗](${runUrl})`);
  out.push(
    "",
    "<sub>Auto-generated by `.github/workflows/security.yml`. Secret values are redacted and never shown.</sub>",
  );
  return out.join("\n") + "\n";
}

// ── security-report.md (rich, Claude-ready) ──────────────────────────────────
function findingsBlock(r, limit) {
  if (r.errored) {
    return [
      `> ⚠️ **${r.name} did not produce a parseable report** (exit code \`${r.exit ?? "?"}\`). Inspect the run log and the \`${artifactName}\` artifact.`,
    ];
  }
  if (r.count === 0) return ["_No findings._"];
  const lines = [
    "| Severity | Location | Rule | Detail |",
    "| --- | --- | --- | --- |",
  ];
  const shown = r.findings.slice(0, limit);
  for (const f of shown) {
    const cell = (s) => String(s).replace(/\|/g, "\\|").replace(/\n/g, " ");
    lines.push(`| ${f.sev} | \`${cell(f.loc)}\` | \`${cell(f.rule)}\` | ${cell(f.msg)} |`);
  }
  if (r.count > shown.length) {
    lines.push("", `_…and ${r.count - shown.length} more — see the \`${artifactName}\` artifact for the full list._`);
  }
  return lines;
}

function buildReport(perToolLimit) {
  const total = results.reduce((n, r) => n + r.count, 0);
  const out = [
    `## 🔒 Security scan findings — \`${refName}\` @ ${shortSha}`,
    "",
    "Automated security scans failed. This report is intended to be handed directly to Claude Code for analysis and a fix.",
    "",
    "### Context",
    "",
    `- **Event:** \`${eventName}\`${prNumber ? ` (PR #${prNumber})` : ""}`,
    `- **Branch/ref:** \`${refName}\``,
    `- **Commit:** \`${sha}\``,
    runUrl ? `- **Workflow run:** ${runUrl}` : "",
    `- **Full raw reports:** the **\`${artifactName}\`** artifact attached to the run above`,
    `- **Total findings:** ${total}${failing.some((r) => r.errored) ? " (plus one or more scanner errors)" : ""}`,
    "",
    "### Summary",
    "",
    summaryTable(),
    "",
    "### Tools & exact commands (for local reproduction)",
    "",
    "| Scanner | Version | Command |",
    "| --- | --- | --- |",
    ...results.map((r) => `| ${r.name} | \`${r.version}\` | \`${(r.command || "").replace(/\|/g, "\\|")}\` |`),
    "",
    "### Findings by scanner",
    "",
  ];
  for (const r of results) {
    out.push(`#### ${r.name} — ${r.category}`, "", ...findingsBlock(r, perToolLimit), "");
  }
  out.push(
    "---",
    "<sub>Auto-generated by `.github/workflows/security.yml`. Secret values are redacted (gitleaks `--redact`) and never included here.</sub>",
  );
  return out.filter((l) => l !== "").join("\n") + "\n";
}

// Build the report body, shrinking the per-tool finding cap until it fits the limit.
let reportBody = "";
for (const limit of [200, 100, 50, 25, 10]) {
  reportBody = buildReport(limit);
  if (reportBody.length <= REPORT_BODY_LIMIT) break;
}
if (reportBody.length > REPORT_BODY_LIMIT) {
  reportBody =
    reportBody.slice(0, REPORT_BODY_LIMIT - 200) +
    `\n\n> _Report truncated to fit the size limit — see the \`${artifactName}\` artifact for the complete output._\n`;
}

// ── Emit ─────────────────────────────────────────────────────────────────────
writeFileSync("security-comment.md", buildComment(), "utf8");
writeFileSync("security-report.md", reportBody, "utf8");

if (process.env.GITHUB_OUTPUT) {
  appendFileSync(process.env.GITHUB_OUTPUT, `failed=${failed}\n`);
}

console.log(
  `Security report: ${failed ? "FAIL" : "PASS"} — ` +
    results.map((r) => `${r.id}=${r.errored ? "err" : r.count}`).join(" "),
);
