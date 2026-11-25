# Copilot instructions

All agent rules for this repository are in **AGENTS.md** at the repository root. Read it first and follow it.

When asked to build, continue or resume a requirement (for example "LT-002"), use the AIDLC workflow:
follow `.aidlc/skills/orchestrator.md` exactly, and pass `--client copilot` to every
`python tools/aidlc/aidlc.py` command.

Copilot-specific notes:
- Unlike Claude Code, Copilot has no pre-edit hook here, so **you** must respect `allowed_paths` from the
  engine's `next` output. The engine re-checks the git diff at every `complete`, and CI checks it again.
  Out-of-scope edits fail the stage.
- Never run `aidlc.py approve` / `reject` / `unblock` / `unlock` / `halt` / `unhalt`. When the engine says
  STOP, end your turn and tell the user the exact command to run in their own terminal.
- Run every terminal command visibly. Do not chain commands to hide what you run.
