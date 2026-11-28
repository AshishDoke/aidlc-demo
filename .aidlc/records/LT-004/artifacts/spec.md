# LT-004 Manager screen for deciding on leave: Specification

## Overview
Managers need one place to see which leave requests are waiting for them and decide on them quickly. LT-004 adds the
manager page, the second screen of the app. On it a manager picks who they are (the same way as on the employee page),
sees how many of their team's requests are waiting, and approves or rejects each one. It uses the behaviour built in
LT-002 (team pending requests, approve, reject) and adds no new business rules. Like the employee page (LT-003), it must
work with the keyboard alone and never rely on colour alone.

## Requirement
1. The app has a manager page. It is the page the employee page's "Manager page" link (LT-003) leads to.
2. At the top, the person picks who they are from the same "who are you" list as the employee page, and the choice is shared
   with the employee page in that browser (picking on one page means the same person is picked on the other).
3. If the picked person is not a manager, the page shows a friendly message instead of the list, and no decisions can be made.
4. For a manager, the page shows at the top how many of their team's requests are waiting for a decision.
5. Below it, the page lists the team's pending requests. Each shows the employee's name, first and last day, type of leave,
   number of days and the employee's reason (if they gave one).
6. Every request in the list has an Approve and a Reject action.
   - Rejecting asks for a reason, which is required.
   - Approving allows an optional comment.
7. After a decision is accepted, that request disappears from the list, the waiting count goes down, and the manager sees a
   short confirmation saying what was decided.
8. If a decision cannot be made, the reason is shown clearly in the words the system gives, and the request stays in the list
   when it is still waiting.
9. Everything works with the keyboard alone, and nothing is shown only as a colour (as on the employee page).

## Glossary
- **Manager page**: the page the employee page's "Manager page" link leads to. It is a separate page from the employee page.
- **Pick who you are**: the same demo identity as LT-002/LT-003 (no login). The choice is remembered in the browser and is the
  same one the employee page uses.
- **Manager**: an employee marked as a manager (LT-001).
- **Team**: the manager's direct reports (LT-002). A manager is never in their own team.
- **Waiting for a decision / pending**: a request from someone in the team that has not been approved or rejected yet.
  Only these are listed and counted.
- **Waiting count**: the number of pending requests from the manager's team at the moment the list was loaded or last changed.
- **Number of days**: the "days covered" from LT-002 (calendar days from first to last day, both included).
- **Reason (on a request)**: the optional reason the employee gave when asking. It is different from the manager's
  **rejection reason** (required when rejecting) and **approval comment** (optional when approving), which are kept as the
  decision comment (LT-002).
- **Confirmation**: a short message on the page saying which request was approved or rejected.

## Scope
- A manager page reachable from the employee page's manager link, and from the manager page back to the employee page.
- The shared "who are you" picker, remembered in the browser together with the employee page.
- A friendly message for a picked person who is not a manager, and a prompt when nobody is picked.
- The waiting count at the top.
- The list of the team's pending requests with name, dates, type, days and the employee's reason.
- Approve with an optional comment, and Reject with a required reason, on every request.
- Removing the decided request from the list, updating the count, and showing a confirmation.
- Showing the system's refusal messages in plain words, and a clear message when the list cannot be loaded.
- Keyboard-only operation, labelled inputs, announced messages, and no colour-only information.

## Acceptance criteria
- AC1 (stated): Given the app is running, when someone follows the "Manager page" link on the employee page, then the manager
  page is shown (no longer a "not found" page), with the "who are you" picker at the top and a way back to the employee page.
- AC2 (stated): Given the manager page is open, when the picker is opened, then it lists every employee the same way as the
  employee page. Given Maya was picked on the employee page in this browser, when the manager page opens, then Maya is already
  picked, and picking someone on the manager page means they are also picked the next time the employee page opens.
- AC3 (derived): Given nobody is picked (or the remembered person no longer exists), when the manager page opens, then no list
  is shown and the page asks the person to pick who they are. No error from the system is shown.
- AC4 (stated): Given Alex (not a manager) is picked, when the manager page is shown, then a friendly message says this page is
  for managers and points back to the employee page. No count, no list and no Approve or Reject actions are shown.
- AC5 (stated): Given Maya is picked and her team has pending requests, then the top of the page shows the number waiting in
  words (for example "1 request waiting for your decision"). Given her team has none, then it says nothing is waiting, in words,
  instead of an empty list.
- AC6 (stated): Given Maya is picked and Alex asked for annual leave from 14 Dec 2026 to 16 Dec 2026 with reason "Trip", then
  the list shows a row for that request with "Alex Turner", 14 Dec 2026, 16 Dec 2026, Annual, 3 days and "Trip". Requests with
  no reason show "—". Requests from other managers' teams, already decided requests and Maya's own requests are not listed.
  The earliest first day is listed first.
- AC7 (stated): Given Maya chooses Approve on Alex's request, when she confirms with or without a comment, then the request is
  approved, it disappears from the list, the count goes down by one, and a confirmation says it was approved (naming the
  employee and dates). Alex then sees it as Approved, with the comment if one was given, on the employee page.
- AC8 (stated): Given Maya chooses Reject on Alex's request, when she confirms with the reason "Release week", then the request is
  rejected, it disappears from the list, the count goes down by one, and a confirmation says it was rejected. When she confirms
  with no reason or only spaces, then the system's message ("a rejection needs a reason") is shown beside the reason field,
  nothing is decided, and the request stays in the list.
- AC9 (derived): Given another manager or another browser window already decided a request that is still in Maya's list, when Maya
  decides it, then the system's message (that it was already approved or rejected, and by whom) is shown, and the list is
  refreshed so the request and the count are up to date. Any other refusal (for example a comment that is too long, or the
  system not being reachable) is shown as a clear message, and the request stays in the list.
- AC10 (derived): Given Maya has started to approve or reject a request, when she changes her mind before confirming, then she can
  cancel and nothing is decided. Given she confirms once, then the same decision cannot be sent twice by clicking again while it is
  being sent.
- AC11 (stated): Given a keyboard-only user, when they use the manager page, then they can pick who they are, reach every
  Approve and Reject action, type a comment or reason, confirm or cancel, and go back to the employee page, using only Tab,
  Shift+Tab, the arrow keys, Enter, Space (and Escape to cancel). The focused element is always visibly highlighted. After a
  decision, focus moves to a sensible place (not lost at the top of the page). Confirmations and errors are announced to screen readers.
- AC12 (stated): Given a colour-blind user, then the count, the confirmation and error messages, and the difference between
  Approve and Reject are all readable as words. Any colour used is extra, never the only sign, and the text has enough contrast.
- AC13 (derived): Given the picked person changes on the manager page, when another person is picked, then the count, the list
  and the messages switch to the new person straight away (including showing the "for managers" message for a non-manager).

## Out of scope
- Changing a decision after it is made (stated).
- Showing requests the manager decided recently (requirement "still to decide"; see Q1). Only pending requests are shown.
- Seeing requests of indirect reports, other teams, or the manager's own requests (LT-002 rules).
- Live updates when a new request arrives while the page is open (see Q6).
- Logging in. Picking who you are stays the demo identity, so anyone can pick a manager. The API still enforces every rule.
- New business rules on dates, overlaps or weekends (LT-005).
- Decisions on several requests at once (bulk approve).
- A phone or small-screen layout (same as LT-003). The page should not break on a narrow window.
- Notifications to the employee (email etc.).

## Dependencies
- LT-002: the team pending list (with requester name and days covered), approve with optional comment, reject with required
  reason, refusals for not a manager, not your team, already decided, and the picked-person identity. Matches front matter: yes.
- LT-003: the employee page with the "Manager page" link that this page fulfils, and the shared page pieces (the "who are you"
  picker with its remembered choice, the way the page talks to the system, date and type wording, and the page styles).
  **This is not in the front matter** (`depends_on: [LT-002]`). LT-004 can be built without LT-003, but it would then
  duplicate those pieces and AC1/AC2 (link from, and shared choice with, the employee page) could not be met. A human should
  add LT-003 to `depends_on`. LT-003 is merged on the remote main, but **the local branch for this run was created from a
  local main that does not include LT-003 yet** (see Q8).
- LT-001 (via LT-002): the demo team (Maya and her three reports) used to try the page.

## Open questions
- Q1: Should managers also see the requests they decided recently? (Listed by the requirement as "still to decide".)
  **Assumption:** no, not in LT-004. Only pending requests are shown. It can be added later as its own requirement, and the
  LT-002 behaviour would need a "recently decided by me" list first.
- Q2: How is the rejection reason or approval comment asked for, and is there a confirm step? **Assumption:** choosing Approve
  or Reject on a row opens a small form for that request only (optional comment, or required reason), with a Confirm and a Cancel
  button. Nothing is decided until Confirm. The page may check that a reason is filled in for convenience, but the system's check
  and message are the deciding ones.
- Q3: What does "friendly message" for a non-manager say? **Assumption:** "This page is for managers. [Name] is not a manager.
  Go to the employee page to ask for leave." with a link to the employee page.
- Q4: In what order is the list shown? **Assumption:** as the system gives it (LT-002 Q6): earliest first day first, so the most
  urgent decisions come first.
- Q5: Should the count include only the requests shown, or all pending requests the manager could decide? **Assumption:** they are
  the same thing: the team's pending requests. The count is the number of rows in the list.
- Q6: Should the list refresh by itself when an employee sends a new request? **Assumption:** no live updates. The list loads when the
  page opens, when a person is picked, and again after a refused decision (AC9).
- Q7: How long does the confirmation stay? **Assumption:** until the next decision or until another person is picked (same as LT-003
  Q8), so keyboard and screen-reader users don't miss it.
- Q8: The branch for this run starts from a local main without LT-003, though LT-003 is merged on the remote. **Assumption:** before
  the code is written (after G1 and G2), the human brings this run's branch up to date with the remote main, so the manager page can
  reuse the LT-003 pieces. If that does not happen, the design must say so and the shared pieces would be duplicated.
- Q9: Should the employee page's manager link remain visible only for managers (LT-003)? **Assumption:** yes, unchanged. The manager
  page is still reachable directly by its address for anyone, which is why AC4 is needed.
- Q10: Should the earlier follow-ups from LT-003 be handled here (the decide button guarded against double clicks is part of AC10; the
  greyed-out form contrast; the plain message for an empty or malformed request body)? **Assumption:** only the double-click guard
  (AC10) is in LT-004. The other two remain separate follow-ups.
