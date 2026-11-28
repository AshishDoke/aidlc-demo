<!-- AIDLC intake: verbatim copy. Do not edit. id=LT-004 source=requirements/LT-004.md fetched=2026-11-28T13:58:23Z -->
---
id: LT-004
title: Manager screen for deciding on leave
depends_on: [LT-002]
priority: medium
owner: product
---
# LT-004 Manager screen for deciding on leave

## Who
Managers.

## Problem
Managers need one place to see what's waiting for them and decide quickly.

## What we want
- A manager page in the app.
- The same "who are you" choice as the employee page. If the person picked isn't a manager,
  show a friendly message instead of the list.
- The number of requests waiting for a decision, shown at the top.
- A list of the team's pending requests showing the employee's name, dates, type, number of days and reason.
- Approve and Reject on every request. Rejecting asks for a reason (required). Approving allows an optional comment.
- After a decision, the request disappears from the list and the manager gets a short confirmation.

## Not included
- Changing a decision after it's made.

## Still to decide
- Should managers also see the requests they decided recently? It would be useful, but we're not sure it's needed now.

## Good to know
- The same keyboard and colour-blind needs as the employee screen (LT-003).
