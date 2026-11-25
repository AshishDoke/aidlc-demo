# AIDLC project context (attach to new chats)

Owner: Ash · Status: v1.0.0 kit built, engine tested (13 tests). The real run with Claude Code / Copilot on Windows hasn't happened yet.

## What it is
**AIDLC (AI-Driven Life Cycle)**: an enterprise-style framework that takes a requirement all the way to a merge request
using AI coding assistants (**Claude Code** and **GitHub Copilot agent mode**), with human approval gates,
resumable workflow state, a tamper-evident audit log and guardrails. Built as a demo, to be reviewed by Codex and Copilot.

**Core principle:** judgement goes to the AI, rules go to deterministic code. No rule relies on a prompt alone.

## Who does what
| Actor | Role |
|---|---|
| Human | Starts runs (`/aidlc-run <ID>`), reviews artifacts, approves/rejects gates in their own terminal, pushes, merges |
| AI assistant (loop driver) | Calls `aidlc.py next`, does the stage work (spec/design/plan/code/tests/review/MR text), calls `complete` |
| `tools/aidlc/aidlc.py` (brain) | Python stdlib-only engine: state machine, stage checks, hash-bound approvals, hash-chained audit log, scope guard, locks, budgets, kill switch, seal, verify. Never calls an LLM |
| Hooks + CI | Claude PreToolUse `guard` / PostToolUse `hook-log`, git pre-push `verify`, GitLab CI `verify --ci`, CODEOWNERS |

There is no separate orchestrator service. Orchestrator = the skill loop (driver) + the engine (brain). It runs only while an
assistant session is open, and resumes from `state.json`.

## Lifecycle
`INTAKE → ANALYZE →[G1 spec]→ DESIGN →[G2 design]→ PLAN → IMPLEMENT → TEST → REVIEW →[G3 raise MR]→ MR → DONE (sealed)`
- INTAKE: the engine copies `requirements/<ID>.md` verbatim (Jira REST optional, off by default) into `requirement.md`.
- Approvals are bound to the artifact's SHA-256 (G3 also to the HEAD commit). Any later change revokes them automatically.
- Reject → back to the producing stage (REWORK), with the comment in `feedback.md`.
- IMPLEMENT: one commit per task tagged `[<ID>][T<n>]`. Resume derives progress from those tags.
- TEST: the engine runs `test_command` itself. The AI's report is not trusted on its own.
- MR: the run is sealed into `.aidlc/records/<ID>/` and committed. The AI never merges.
- Each requirement is its own run. `depends_on` blocks a start until dependencies are merged into `main`.

## Key files
```
tools/aidlc/aidlc.py            engine (~1300 lines) + tests/test_engine.py
.aidlc/config.json              stages' required sections, limits, budgets, test_command, jira, mr
.aidlc/policies/guardrails.json protected paths, denied bash patterns, secret/test-skip/redaction regexes
.aidlc/skills/*.md              orchestrator + 7 stage skills (tool-neutral, single source of truth)
.aidlc/templates/*.md           artifact templates
.aidlc/context/project.md       demo app stack and conventions
.aidlc/runs/<ID>/               live state (git-ignored): state.json, approvals.json, audit.jsonl, artifacts/
.aidlc/records/<ID>/            sealed evidence committed with the MR
.claude/ (settings.json hooks+permissions, skills/aidlc-run, aidlc-status) · CLAUDE.md
.github/ (copilot-instructions.md, prompts/aidlc-run.prompt.md, aidlc-status.prompt.md) · .vscode/settings.json
AGENTS.md (shared rules) · .githooks/pre-push · .gitlab-ci.yml · .gitlab/CODEOWNERS
docs/ ARCHITECTURE.md, GUARDRAILS.md, DEMO.md, REVIEW-NOTES.md · requirements/ LT-001..LT-005
```

## Engine commands
- AI (JSON out): `next <ID>`, `complete <ID> --stage X`, `check <ID>`, `log <ID> --note`, `status [--json]`. Always pass `--client claude-code|copilot`.
- Human only (TTY, refused when `CLAUDECODE` is set): `approve <ID> G1|G2|G3`, `reject <ID> <gate> --comment`, `unblock`, `unlock`, `halt`, `unhalt`.
- Hooks/CI: `guard`, `hook-log`, `verify [--ci --branch --base]`.

## Guardrails (summary)
AI can't approve, edit state/records/governance files, or leave the plan's `allowed_files` scope.
Also blocked or checked: protected paths, max files/lines/tasks, retry budget → BLOCKED, secret scan, test-integrity scan
(no Skip/Ignore/Assert.True(true)/deleted tests), redaction in logs, no merge/rebase/amend/force/config/--no-verify/push to main,
no curl/wget, per-run lock, HALT kill switch, dependency gate, requirement-drift reset, hash-chained audit verified in CI.

## Demo app: Team Leave Tracker
.NET 8 minimal API + EF Core SQLite + plain HTML/JS in wwwroot + xUnit. No auth (`X-Employee-Id` header).
- LT-001 DB, model and solution skeleton
- LT-002 REST API (depends on 001)
- LT-003 employee UI (002)
- LT-004 manager UI (002)
- LT-005 business rules (003, 004). Planted ambiguity, "10 days": working or calendar days? Good for showing a G1 rejection.

## Decisions made
Python engine (stdlib only) · Markdown requirements for the demo · local approvals for the demo tier · GitLab (MR, not PR) ·
live state git-ignored, sealed records committed · `mr.ai_may_push_feature_branch: false`.

## Known limitations / next steps
1. Local approvals deter but don't prove (same OS user). Next: source approvals from GitLab MR approvals or Jira transitions.
2. The hash chain is tamper-evident, not tamper-proof. Next: anchor the last hash externally at each gate.
3. Copilot has no pre-edit hook. Scope is enforced at `complete` and in CI only.
4. To verify on Windows: Claude hook command (`python` vs `py`), Copilot prompt front matter (`agent:` vs `mode:`),
   `.vscode` auto-approve keys, approving from Git Bash (needs `winpty`).
5. No token/cost budgets. No requester ≠ approver check. Jira intake untested against a live instance.

## How to help me
Very concise technical answers. Don't re-explain the architecture. Reference files and functions by name.
For changes, give exact edits to the files above, and keep engine tests passing.
