import * as sandcastle from "@ai-hero/sandcastle";
import { docker } from "@ai-hero/sandcastle/sandboxes/docker";
import { execFileSync, spawnSync } from "node:child_process";

// Per-issue loop: implement, review, then run the Windows gate (when it has
// steps) before merging into the current branch.
// Run this from the WSL clone whose origin is the Windows repo, with:
//   npx tsx .sandcastle/main.mts [maxIssues] [issueId]
//   maxIssues  how many issues to work through (default 3)
//   issueId    work on this issue only, e.g. my-feature/18

const MAX_ISSUES = Number(process.argv[2] ?? 3);
const ONLY_ISSUE = process.argv[3] ?? "any";
const MODEL = "claude-opus-5-5";

// Commands run once in each new sandbox before the agents start.
const SANDBOX_READY: { command: string }[] = [
  {
    "command": "dotnet restore Tests/XisfPreview.Tests.csproj"
  }
];

// Steps run on Windows, in the gate worktree, after the reviewer and before the
// merge. Each must exit 0 within its timeout. With no steps there is no gate.
const GATE_STEPS: { label: string; command: string; args: string[]; timeoutMs: number }[] = [
  {
    "label": "key",
    "command": "pwsh.exe",
    "args": [
      "-NoProfile",
      "-Command",
      "Copy-Item 'C:\\work\\personal\\xisf-preview\\key.snk' ."
    ],
    "timeoutMs": 30000
  },
  {
    "label": "build",
    "command": "dotnet.exe",
    "args": [
      "build",
      "Tests/XisfPreview.Tests.csproj",
      "-p:SkipKillSurrogates=true",
      "--nologo",
      "-v",
      "q",
      "-nodeReuse:false"
    ],
    "timeoutMs": 300000
  },
  {
    "label": "tests",
    "command": "pwsh.exe",
    "args": [
      "-NoProfile",
      "-File",
      "Tests/run-gate-tests.ps1",
      "-TimeoutSeconds",
      "540"
    ],
    "timeoutMs": 600000
  }
];

const hooks = {
  sandbox: {
    onSandboxReady: SANDBOX_READY,
  },
};

function run(command: string, args: string[]): string {
  return execFileSync(command, args, { encoding: "utf8" }).trim();
}

function git(...args: string[]): string {
  return run("git", args);
}

// Runs one gate step through WSL interop and reports whether it passed.
function step(label: string, command: string, args: string[], cwd: string, timeoutMs: number): boolean {
  console.log(`\n--- Windows gate: ${label}`);
  const result = spawnSync(command, args, { cwd, stdio: "inherit", timeout: timeoutMs });
  if (result.status === 0) return true;
  console.log(`--- Windows gate: ${label} failed (exit ${result.status ?? result.signal})`);
  return false;
}

// The container cannot run Windows-only checks, so the branch is fetched into
// a temporary worktree of the Windows repo (origin) and the gate steps run
// there with the Windows tools. Windows git trusts the \\wsl$ form of this
// clone's path, not \\wsl.localhost.
function windowsGate(branch: string): boolean {
  if (!GATE_STEPS.length) return true;
  const windowsRepo = run("wslpath", ["-w", git("remote", "get-url", "origin")]);
  const clone = run("wslpath", ["-w", process.cwd()]).replace(/^\\\\wsl\.localhost\\/i, "\\\\wsl$\\");
  const gate = `${windowsRepo}\\.sandcastle\\worktrees\\gate`;
  const gateLinux = run("wslpath", ["-u", gate]);
  spawnSync("git.exe", ["-C", windowsRepo, "worktree", "remove", "--force", gate]);
  try {
    return (
      step("fetch", "git.exe", ["-C", windowsRepo, "fetch", "--quiet", clone, branch], process.cwd(), 120_000) &&
      step("checkout", "git.exe", ["-C", windowsRepo, "worktree", "add", "--quiet", "--detach", gate, "FETCH_HEAD"], process.cwd(), 120_000) &&
      GATE_STEPS.every((s) => step(s.label, s.command, s.args, gateLinux, s.timeoutMs))
    );
  } finally {
    spawnSync("git.exe", ["-C", windowsRepo, "worktree", "remove", "--force", gate]);
  }
}

for (let issue = 1; issue <= MAX_ISSUES; issue++) {
  console.log(`\n=== Issue ${issue}/${MAX_ISSUES} ===\n`);

  const base = git("rev-parse", "HEAD");
  const branch = `sandcastle/${Date.now()}`;
  const sandbox = await sandcastle.createSandbox({ branch, sandbox: docker(), hooks });

  let passed = false;
  try {
    const implement = await sandbox.run({
      name: "implementer",
      agent: sandcastle.claudeCode(MODEL),
      promptFile: "./.sandcastle/prompt.md",
      promptArgs: { ONLY_ISSUE },
    });
    if (!implement.commits.length) {
      // No commits: the backlog is empty or every remaining issue is blocked.
      console.log("The implementer made no commits. Stopping.");
      break;
    }

    await sandbox.run({
      name: "reviewer",
      agent: sandcastle.claudeCode(MODEL),
      promptFile: "./.sandcastle/review-prompt.md",
      promptArgs: { BASE: base },
    });

    passed = windowsGate(branch);
  } finally {
    await sandbox.close();
  }

  if (!passed) {
    console.log(`\nBranch ${branch} failed on Windows and is not merged. Stopping.`);
    break;
  }
  git("merge", "--ff-only", branch);
  git("branch", "-d", branch);
  console.log(`\nMerged ${branch}.`);
  if (ONLY_ISSUE !== "any") break;
}

console.log("\nAll done.");
