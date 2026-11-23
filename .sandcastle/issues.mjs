#!/usr/bin/env node
// Local-markdown issue tracker for Sandcastle (see docs/agents/issue-tracker.md).
//
// Issues live in .scratch/<feature>/issues/NN-<slug>.md. Each has a "Status:" line
// (bold or plain) near the top, an optional "Blocked by:" line, and comments under
// a "## Comments" heading. An issue id is "<feature>/<NN>", e.g. "my-feature/06".
//
//   node .sandcastle/issues.mjs list               JSON array of ready-for-agent issues
//   node .sandcastle/issues.mjs view <id>          print one issue
//   node .sandcastle/issues.mjs close <id> <text>  set Status to done (needs-verification unless <text>
//                                                  says "Needs manual verification: none"), append <text>
//   node .sandcastle/issues.mjs comment <id> <text> append <text> as a comment
//
// <id> may also be a path to the issue file, or a bare number if only one feature has it.

import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const SCRATCH = path.join(ROOT, ".scratch");
const READY = "ready-for-agent";
const DONE = "done";
const NEEDS_VERIFICATION = "needs-verification";
// Statuses whose code is committed, so they no longer block other issues.
const RESOLVED = new Set([DONE, NEEDS_VERIFICATION, "wontfix"]);
const STATUS_RE = /^([ \t]*(?:\*\*)?Status:(?:\*\*)?[ \t]*)(.*?)[ \t]*$/m;
const BLOCKED_RE = /^[ \t]*(?:\*\*)?Blocked by:(?:\*\*)?[ \t]*(.*?)[ \t]*$/m;

function fail(message) {
  console.error(message);
  process.exit(1);
}

function loadIssues() {
  if (!fs.existsSync(SCRATCH)) return [];
  const issues = [];
  for (const feature of fs.readdirSync(SCRATCH).sort()) {
    const dir = path.join(SCRATCH, feature, "issues");
    if (!fs.existsSync(dir) || !fs.statSync(dir).isDirectory()) continue;
    for (const file of fs.readdirSync(dir).sort()) {
      const match = /^(\d+)-.*\.md$/.exec(file);
      if (!match) continue;
      const fullPath = path.join(dir, file);
      const body = fs.readFileSync(fullPath, "utf8");
      const title = (/^#\s+(.*)$/m.exec(body)?.[1] ?? file).trim();
      const status = (STATUS_RE.exec(body)?.[2] ?? "").trim();
      const blockedText = BLOCKED_RE.exec(body)?.[1] ?? "";
      const blockedBy = /^none\b/i.test(blockedText) ? [] : blockedText.match(/\d+/g) ?? [];
      const specFile = ["spec.md", "PRD.md"]
        .map((name) => path.join(SCRATCH, feature, name))
        .find((p) => fs.existsSync(p));
      issues.push({
        id: `${feature}/${match[1]}`,
        number: match[1],
        feature,
        title,
        status,
        blockedBy,
        path: path.relative(ROOT, fullPath),
        spec: specFile ? path.relative(ROOT, specFile) : null,
        body,
      });
    }
  }
  return issues;
}

function findIssue(issues, ref) {
  if (!ref) fail("Missing issue id. Use <feature>/<NN>, e.g. my-feature/06.");
  const asPath = path.resolve(ROOT, ref);
  const byPath = issues.find((i) => path.resolve(ROOT, i.path) === asPath);
  if (byPath) return byPath;
  const [feature, number] = ref.includes("/") ? ref.split("/") : [null, ref];
  const n = Number(number);
  const matches = issues.filter(
    (i) => Number(i.number) === n && (feature === null || i.feature === feature),
  );
  if (matches.length === 1) return matches[0];
  if (matches.length > 1) fail(`Ambiguous id "${ref}": ${matches.map((i) => i.id).join(", ")}`);
  fail(`No issue "${ref}".`);
}

function today() {
  const d = new Date();
  const pad = (x) => String(x).padStart(2, "0");
  return `${pad(d.getDate())}/${pad(d.getMonth() + 1)}/${d.getFullYear()}`;
}

function appendComment(body, label, text) {
  const comment = `${today()}, ${label}:\n${text.trim()}\n`;
  const trimmed = body.replace(/\s*$/, "\n");
  if (/^## Comments\s*$/m.test(trimmed)) return `${trimmed}\n${comment}`;
  return `${trimmed}\n## Comments\n\n${comment}`;
}

function writeIssue(issue, body) {
  fs.writeFileSync(path.join(ROOT, issue.path), body);
}

const [command, ref, ...rest] = process.argv.slice(2);
const text = rest.join(" ");
const issues = loadIssues();

switch (command) {
  case "list": {
    const statusOf = new Map(issues.map((i) => [i.id, i.status]));
    const open = issues
      .filter((i) => i.status === READY)
      .map((i) => {
        const openBlockers = i.blockedBy
          .map((n) => `${i.feature}/${n.padStart(2, "0")}`)
          .filter((id) => !RESOLVED.has(statusOf.get(id)));
        return { ...i, openBlockers, blocked: openBlockers.length > 0 };
      });
    console.log(JSON.stringify(open, null, 2));
    break;
  }
  case "view": {
    const issue = findIssue(issues, ref);
    console.log(`id: ${issue.id}\npath: ${issue.path}\nspec: ${issue.spec ?? "none"}\n`);
    console.log(issue.body);
    break;
  }
  case "close": {
    const issue = findIssue(issues, ref);
    if (!text.trim()) fail("close needs a comment explaining what was done.");
    if (!STATUS_RE.test(issue.body)) fail(`${issue.path} has no Status: line.`);
    const status = /Needs manual verification\W*none\b/i.test(text) ? DONE : NEEDS_VERIFICATION;
    let body = issue.body.replace(STATUS_RE, `$1${status}`);
    body = appendComment(body, "closed", text);
    writeIssue(issue, body);
    console.log(`Closed ${issue.id} as ${status} (${issue.path}).`);
    break;
  }
  case "comment": {
    const issue = findIssue(issues, ref);
    if (!text.trim()) fail("comment needs some text.");
    writeIssue(issue, appendComment(issue.body, "comment", text));
    console.log(`Commented on ${issue.id} (${issue.path}).`);
    break;
  }
  default:
    fail("Usage: issues.mjs list | view <id> | close <id> <text> | comment <id> <text>");
}
