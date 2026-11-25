---
name: aidlc-run
description: Run or resume the AIDLC (AI-Driven Life Cycle) workflow for a requirement ID such as LT-001, taking it from requirement to merge request with human approval gates. Use when the user says "/aidlc-run <ID>", "run AIDLC", "continue LT-00x", or "resume the requirement".
---

# AIDLC run (Claude Code adapter)

Your client id is `claude-code`. Pass `--client claude-code` on every engine command.

1. Read `.aidlc/skills/orchestrator.md` and follow it exactly. It is the single source of truth for the loop.
2. The requirement ID is the argument the user gave (for example `LT-001`). If none was given, ask for it.
3. The engine is `python tools/aidlc/aidlc.py`. If `python` is not found, try `py` (Windows) or `python3`.

Reminders specific to Claude Code:
- A PreToolUse hook (`aidlc.py guard`) blocks edits outside the current stage's allowed paths. If a write
  is blocked, do not try another tool or path to get around it. Re-read the `next` output and stay in scope.
- A PostToolUse hook logs every edit and command to the run's audit log automatically.
- When the engine says STOP for an approval, end your turn. Tell the user the exact `approve` / `reject`
  command to run in **their own terminal**. You cannot and must not run it.
