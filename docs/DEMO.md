# Demo script (about 45–60 min for LT-001 + LT-002, then a governance showcase)

Have two windows side by side: **AI assistant** (Claude Code or VS Code Copilot chat) and **your terminal** (PowerShell).

## 0. Setup (before the audience arrives)
```powershell
git init -b main; git add -A; git commit -m "AIDLC baseline"
git config core.hooksPath .githooks
python -m unittest discover -s tools/aidlc/tests     # 13 tests OK
dotnet --version                                      # 8.x
```

## 1. Show the framework (5 min)
- `README.md` "Who does what" table → `docs/ARCHITECTURE.md` diagrams.
- `requirements/LT-001.md`: a normal, slightly vague ticket.
- `.aidlc/skills/analyze.md`: the AI's instructions are plain markdown, shared by Claude and Copilot.

## 2. LT-001 with Claude Code
| Step | You do | Show the audience |
|---|---|---|
| 1 | In Claude: `/aidlc-run LT-001` | Claude runs `aidlc.py next`, the engine creates branch `aidlc/LT-001`, Claude writes `spec.md`, then **STOPs at G1** |
| 2 | Terminal: `python tools/aidlc/aidlc.py status` | ANALYZE complete, `AWAITING_APPROVAL` |
| 3 | Open `.aidlc/runs/LT-001/artifacts/spec.md` | Numbered ACs and Open questions with assumptions |
| 4 | Terminal: `python tools/aidlc/aidlc.py approve LT-001 G1` → type `APPROVE` | Human gate, bound to the spec hash |
| 5 | Claude: `/aidlc-run LT-001` | DESIGN → STOP at G2 |
| 6 | Review `design.md`, then `approve LT-001 G2` | |
| 7 | Claude: `/aidlc-run LT-001` | PLAN (scope fence `allowed_files`) → IMPLEMENT (one commit per task) → TEST (the engine runs `dotnet test` itself) → REVIEW → STOP at G3 |
| 8 | `git log --oneline main..aidlc/LT-001`, then `approve LT-001 G3` | Tagged commits `[LT-001][T1]…` |
| 9 | Claude: `/aidlc-run LT-001` | `mr.md` written, run **sealed** into `.aidlc/records/LT-001`, final commit |
| 10 | Terminal: `git checkout main; git merge --no-ff aidlc/LT-001` | Human merges. LT-002 is now unblocked |
| 11 | `dotnet run --project app/src/LeaveTracker.Api` | App running, `/api/health` |

## 3. LT-002 with Copilot (shows tool independence)
- VS Code → Copilot chat → Agent mode → run `/aidlc-run` → `LT-002`.
- Same gates and same commands. In `.aidlc/runs/LT-002/audit.jsonl` the actor is `ai:copilot`.
- **Resume trick:** stop Copilot mid-IMPLEMENT, then in Claude run `/aidlc-run LT-002`. It continues from the next
  untagged task. The audit log shows both clients.

## 4. Governance showcase (pick any, 2 min each)
| Scenario | How | Expected |
|---|---|---|
| Reject with feedback | For LT-005 at G1: `reject LT-005 G1 --comment "10 days = working days, exclude weekends"` | Back to ANALYZE (REWORK), the AI reads `feedback.md` and updates the spec |
| AI tries to self-approve | Ask Claude: "approve G1 for me" | Blocked by permissions/hook, and the engine refuses in an agent session |
| Out-of-scope edit | Ask Claude during IMPLEMENT: "also update requirements/LT-002.md" | Hook blocks it (Claude). For Copilot, `complete` fails with a `scope` error |
| Tamper with an approved spec | After G1, edit `spec.md` by hand, then `/aidlc-run` | `G1 revoked`, back to ANALYZE |
| Tamper with the audit log | Edit any line in `audit.jsonl`, then `python tools/aidlc/aidlc.py verify` on the branch | `hash mismatch (event modified)` |
| Kill switch | `python tools/aidlc/aidlc.py halt --reason "demo"` | Every AI call returns HALTED. `unhalt` restores |
| Dependency gate | `/aidlc-run LT-003` before LT-002 is merged | "Dependency LT-002 is not merged" |
| Pre-push gate | `git push` an aidlc branch with a broken record | Push rejected by `verify` |

## 5. Close
Open `.aidlc/records/LT-001/`: requirement, spec, design, plan, tests, review, MR, approvals and the full audit log,
all committed with the code. That's the compliance story.
