---
agent: agent
description: Show AIDLC run status and pending human approvals
---

Run `python tools/aidlc/aidlc.py status --json`, then show a compact table (ID, title, stage, status, approved gates).
For each run that is `AWAITING_APPROVAL`, give the artifact to review and the exact command for the human to run
in their own terminal: `python tools/aidlc/aidlc.py approve <ID> <GATE>` or `reject <ID> <GATE> --comment "..."`.
For each run that is `BLOCKED`, say that a human must run `python tools/aidlc/aidlc.py unblock <ID>`.
Do not modify any files.
