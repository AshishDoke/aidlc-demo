---
name: aidlc-status
description: Show the status of AIDLC runs (stage, status, approved gates) and what the human needs to do next. Use when the user asks "AIDLC status", "where is LT-00x", or "what's pending approval".
---

# AIDLC status

1. Run `python tools/aidlc/aidlc.py status --json` (or `status <ID> --json` for one run).
2. Present a compact table: ID, title, stage, status, approved gates.
3. For every run with status `AWAITING_APPROVAL`, tell the user the artifact to review and the command to run
   in their own terminal: `python tools/aidlc/aidlc.py approve <ID> <GATE>` (or `reject ... --comment "..."`).
4. For `BLOCKED` runs, show the last errors (`.aidlc/runs/<ID>/audit.jsonl`, last `stage.check_failed` event)
   and say that a human must run `aidlc.py unblock <ID>`.
5. Do not modify anything.
