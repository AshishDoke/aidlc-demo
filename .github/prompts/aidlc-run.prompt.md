---
agent: agent
description: Run or resume the AIDLC workflow for a requirement (requirement → MR with human gates)
---

# AIDLC run (GitHub Copilot adapter)

Requirement ID: ${input:reqId:Requirement ID, e.g. LT-001}

Your client id is `copilot`. Pass `--client copilot` on every engine command.

1. Read [.aidlc/skills/orchestrator.md](../../.aidlc/skills/orchestrator.md) and follow it exactly. It is the
   single source of truth for the loop.
2. Read [AGENTS.md](../../AGENTS.md). Its rules are non-negotiable.
3. The engine is `python tools/aidlc/aidlc.py` (on Windows use `py` if `python` is not found).
4. Start with: `python tools/aidlc/aidlc.py next ${input:reqId} --client copilot`

Copilot reminders:
- There is no edit-blocking hook for Copilot, so only write to `allowed_paths`. The engine and CI
  reject out-of-scope changes.
- When the engine says STOP (approval gate, BLOCKED, HALTED), end your turn and relay the message.
  The human runs the approval in their own terminal.
