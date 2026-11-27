<!-- AIDLC intake: verbatim copy. Do not edit. id=LT-003 source=requirements/LT-003.md fetched=2026-11-27T10:57:28Z -->
---
id: LT-003
title: Employee screen for requesting leave
depends_on: [LT-002]
priority: medium
owner: product
---
# LT-003 Employee screen for requesting leave

## Who
Employees.

## Problem
Employees need a simple page where they can ask for leave and see what happened to their requests,
without emailing their manager.

## What we want
- When someone opens the app, they see the employee page.
- At the top they pick who they are from a list of employees.
  The app remembers their choice the next time they open it.
- A form to ask for leave: first day, last day, type of leave, optional reason, and a button to send it.
- After sending, they see a confirmation and the new request shows up in their list straight away.
- A "My requests" list showing the dates, type, number of days, status and the manager's comment (if any).
- If a request can't be accepted, the reason is shown clearly next to the form.
- Managers see a link to their manager page (LT-004). Other employees don't.

## Not included
- A phone layout. It would be nice, but it isn't required.

## Good to know
- Keep it simple and clean.
- Some colleagues use only the keyboard, and some are colour-blind. Everything must work
  with the keyboard, and the status must be readable as text, not shown only as a colour.
