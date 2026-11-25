# AIDLC: AI-Driven Life Cycle (enterprise demo kit)

Requirement → spec → design → plan → code → tests → review → merge request. **AI does the work, humans approve the gates,
and a deterministic engine enforces the rules.** It works with **Claude Code** and **GitHub Copilot** (agent mode).

Demo product: **Team Leave Tracker** (.NET 8 minimal API + SQLite + vanilla JS), built requirement by requirement
from [`requirements/`](requirements/README.md).

```
You ──/aidlc-run LT-001──► AI assistant (Claude or Copilot) ──next / complete──► aidlc.py engine ──► .aidlc/runs/LT-001/
 ▲                              │ does stage work                                  │ state, gates,         (state, audit log,
 └──── approve in YOUR terminal ┴────────────── STOP at G1 / G2 / G3 ◄─────────────┘ checks, audit          approvals, artifacts)
```

## Who does what
| Actor | Responsibility |
|---|---|
| **Human** | Starts runs, reviews artifacts, approves or rejects gates G1/G2/G3, pushes the branch, merges the MR |
| **AI assistant** | Writes the spec, design, plan, code, tests, review and MR text. Asks the engine what to do next |
| **`tools/aidlc/aidlc.py`** | State machine, stage exit checks, hash-bound approvals, hash-chained audit log, scope fence, locks, budgets, kill switch, sealing |
| **Hooks + CI** | Claude PreToolUse/PostToolUse hooks, git pre-push, and GitLab CI `verify`: the backstop for every client |

Details: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) · [docs/GUARDRAILS.md](docs/GUARDRAILS.md) ·
[docs/DEMO.md](docs/DEMO.md) (step-by-step demo script) · [docs/REVIEW-NOTES.md](docs/REVIEW-NOTES.md) (known limitations)

## Lifecycle
```
INTAKE → ANALYZE ─[G1 spec]→ DESIGN ─[G2 design]→ PLAN → IMPLEMENT → TEST → REVIEW ─[G3 raise MR]→ MR → DONE (sealed)
```
- INTAKE is done by the engine: a verbatim copy of `requirements/<ID>.md`, or a Jira issue if enabled.
- Each approval is bound to the SHA-256 of the approved artifact. G3 is also bound to the commit. Any later change revokes it.
- REJECT sends the run back with your comment in `feedback.md`. The AI must address it.

## Prerequisites
- Git, Python 3.9+ (`python` or `py` on PATH), .NET 8 SDK
- Claude Code, and/or VS Code with GitHub Copilot (agent mode, prompt files enabled)

## Setup (once)
```powershell
# in the unzipped folder
git init -b main
git add -A
git commit -m "AIDLC baseline"
git config core.hooksPath .githooks          # enables the pre-push verify gate
python -m unittest discover -s tools/aidlc/tests -v   # engine self-test (13 tests)
```
Optional: create a GitLab project, push `main`, and the pipeline in `.gitlab-ci.yml` enforces `verify` on every MR.

## Run a requirement
| Client | Command |
|---|---|
| Claude Code | `/aidlc-run LT-001` |
| Copilot (agent mode) | Run the prompt `/aidlc-run` and enter `LT-001` |

When the AI stops at a gate, review the artifact it names, then **in your own terminal** (PowerShell or Windows
Terminal; in Git Bash use `winpty python ...`):
```powershell
python tools/aidlc/aidlc.py approve LT-001 G1
python tools/aidlc/aidlc.py reject  LT-001 G1 --comment "AC3 must say calendar days"
```
Then run `/aidlc-run LT-001` again to resume. Resume works from any client, at any time.

After DONE, merge (human):
```powershell
git checkout main
git merge --no-ff aidlc/LT-001      # or push the branch and merge the MR in GitLab
```
LT-002 can only start once LT-001's sealed record is on `main`.

## Human-only commands
| Command | Purpose |
|---|---|
| `aidlc.py approve <ID> <G1/G2/G3>` | Approve a gate (interactive confirmation) |
| `aidlc.py reject <ID> <gate> --comment "..."` | Reject and send feedback to the AI |
| `aidlc.py unblock <ID>` | Clear BLOCKED after repeated failed checks |
| `aidlc.py unlock <ID>` | Release a run lock held by another client |
| `aidlc.py halt --reason "..."` / `unhalt` | Kill switch for all AI runs |
| `aidlc.py status` | Show all runs |

## Repository map
```
.aidlc/config.json            lifecycle config (gates, sections, limits, test command, Jira)
.aidlc/policies/guardrails.json  protected paths, denied commands, secret/test-skip/redaction patterns
.aidlc/skills/*.md            tool-neutral stage instructions (single source of truth)
.aidlc/templates/*.md         artifact templates
.aidlc/context/project.md     stack and conventions every stage reads
.aidlc/runs/<ID>/             live run state (git-ignored, local)
.aidlc/records/<ID>/          sealed evidence, committed with the MR
tools/aidlc/aidlc.py          the engine (stdlib only) + tests/
.claude/                      Claude Code adapter: skills, hooks, permissions
.github/                      Copilot adapter: prompt files, instructions
AGENTS.md / CLAUDE.md         shared agent rules
.githooks/pre-push, .gitlab-ci.yml, .gitlab/CODEOWNERS   enforcement
requirements/                 LT-001 … LT-005 demo requirements
app/                          the Leave Tracker app (created by the AIDLC runs)
```
