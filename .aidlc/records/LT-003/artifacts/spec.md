# LT-003 Employee screen for requesting leave: Specification

## Overview
Employees currently email their manager to ask for leave. LT-003 gives them a simple employee page, the first screen of
the app. On it they pick who they are, ask for leave with a short form, and follow what happened to their requests. It
uses the behaviour built in LT-002 (asking for leave, own requests, the employee list) and adds no new business rules. The page
must be fully usable with the keyboard, and status must be readable as text, because some colleagues use only the keyboard
and some are colour-blind.

## Requirement
1. Opening the app (its start address) shows the employee page.
2. At the top of the page the person picks who they are from a list of all employees. The page remembers the choice in
   that browser, so the same person is already picked the next time the page is opened there.
3. A form asks for leave with: first day, last day, type of leave (annual, sick, unpaid), an optional reason, and a send
   button. It always asks for the picked person.
4. After a request is sent and accepted, the page shows a confirmation, and the new request appears in "My requests" straight
   away, without reloading the page.
5. A "My requests" list shows each of the picked person's requests with: first and last day, type, number of days, status, and
   the manager's comment if there is one (for a rejection, that is the reason).
6. If a request cannot be accepted, the reason is shown clearly next to the form, in the words the system gives.
7. If the picked person is a manager, the page shows a link to the manager page (LT-004). Otherwise it does not.
8. Everything works with the keyboard alone, and status is always readable as text, never shown only as a colour.
9. The page is kept simple and clean. A phone layout is not required.

## Glossary
- **Employee page**: the page shown at the app's start address (`/`). It is the main screen for employees.
- **Pick who you are**: choosing your own name from the employee list. This is the demo identity from LT-002 (no login). The page
  sends the choice with every request that depends on who you are.
- **Remembers the choice**: the choice is kept in the browser on that computer. It is not stored on the server and does not follow
  the person to another browser or computer.
- **Number of days**: the "days covered" from LT-002, meaning calendar days from first to last day with both included.
- **Status**: pending, approved or rejected, shown as words.
- **Manager's comment**: the comment or rejection reason the manager gave when deciding (LT-002). It is empty for pending requests
  and for approvals without a comment.
- **Confirmation**: a short message on the page saying the request was sent and is now pending.
- **Manager page**: the page for managers delivered by LT-004. This requirement only adds the link to it.

## Scope
- Showing the employee page at the app's start address.
- A "who are you" picker filled from the employee list, with the choice remembered in the browser.
- A leave request form (first day, last day, type, optional reason, send button) that sends for the picked person.
- A confirmation after a successful send, and immediate refresh of "My requests".
- A "My requests" list with dates, type, number of days, status as text and the manager's comment.
- Showing the system's refusal messages next to the form, per field where the system names a field.
- A manager-page link that is visible only when the picked person is a manager.
- Keyboard-only operation, labelled inputs, and status as text (engineering defaults on accessibility).
- Clear messages when nobody is picked yet or when the employee list or requests cannot be loaded.

## Acceptance criteria
- AC1 (stated): Given the app is running, when someone opens its start address, then the employee page is shown, with the
  "who are you" picker at the top.
- AC2 (stated): Given the employee page is open, when the picker is opened, then it lists every employee by name. Given
  nobody has been picked yet, then the form cannot be sent and the page says to pick who you are first.
- AC3 (stated): Given Alex picks himself, when the page is closed and opened again in the same browser, then Alex is already
  picked and his requests are shown.
- AC4 (derived): Given the remembered person no longer exists (for example, the demo data was reset), when the page opens, then
  nobody is picked and the page asks the person to pick who they are. It shows no error from the system.
- AC5 (stated): Given Alex is picked, when he fills in first day 2026-12-14, last day 2026-12-16, type annual, reason "Trip",
  and sends, then the page shows a confirmation that the request was sent and is pending, the form is cleared, and the new
  request appears at the top of "My requests" straight away with 14 Dec 2026 – 16 Dec 2026, annual, 3 days, status "Pending".
- AC6 (stated): Given Alex is picked, when he sends a request whose last day is before its first day, then the system's message
  ("The last day … is before the first day …") is shown next to the form, beside the last-day field. Nothing is added to
  "My requests", and what he typed stays in the form so he can correct it.
- AC7 (stated): Given any other refusal from the system (missing dates, missing type, reason too long, or something
  unexpected such as the system not being reachable), when he sends, then a clear message saying why is shown next to the form.
- AC8 (stated): Given Sam is picked, when "My requests" is shown, then it lists all of Sam's requests, newest first, each with
  first and last day, type, number of days, status as a word, and the manager's comment when there is one. Given Sam has no
  requests, then the list says so in words.
- AC9 (stated): Given Maya (a manager) is picked, then a link to the manager page is visible. When Alex (not a manager)
  is picked, then no such link is shown. Switching between them updates this straight away.
- AC10 (stated): Given a keyboard-only user, when they use the page, then they can pick who they are, fill in every field,
  send the form, and reach the manager link, using only Tab, Shift+Tab, the arrow keys, Enter and Space. The focused element is
  always visibly highlighted, and confirmation and error messages are announced to screen readers.
- AC11 (stated): Given a colour-blind user, when they look at "My requests", then each status is written as a word (Pending,
  Approved, Rejected). Any colour used is extra, never the only sign, and the text has enough contrast to read.
- AC12 (derived): Given the picked person changes, when another person is picked, then "My requests", the manager link and
  the form all switch to the new person straight away, and the form's earlier messages are cleared.

## Out of scope
- A phone or small-screen layout (stated). The page should not break on a narrow window, but it is not designed for one.
- The manager page itself (LT-004). Until LT-004 is built, the link leads to a page that does not exist yet (see Q1).
- Cancelling or editing a request (not in LT-002 either).
- New business rules on dates, overlaps, weekends or length (LT-005).
- Logging in. Picking who you are stays the demo identity.
- Showing other people's requests on this page.
- Translations, or date formats other than the one chosen for the page.

## Dependencies
- LT-002: employee list, ask for leave (self only), own requests newest first, days covered, plain-words refusal messages,
  and the picked-person identity. Matches front matter: yes.
- LT-001 (via LT-002): employees, managers and the demo team used to try the page.
- LT-004 is the target of the manager link, but it is not a dependency. LT-003 only links to it.

## Open questions
- Q1: The manager page does not exist until LT-004. What should the link do before then? **Assumption:** the link is shown for
  managers and points to the manager page's address. Until LT-004 is merged it leads to a "not found" page, which is acceptable
  because LT-004 follows.
- Q2: How should dates be shown in "My requests"? **Assumption:** as day, short month name and year (for example "14 Dec 2026"), in
  English, so the order of day and month can't be misread. The date inputs use the browser's own date picker.
- Q3: Should the form check obvious mistakes, like the last day before the first, before sending? **Assumption:** the page may
  do a quick check for convenience, but the system's own check and message are always the deciding ones (project.md). The
  message shown is the system's.
- Q4: Should the form default the type to "annual"? **Assumption:** no. The type must be chosen, so nobody asks for the wrong
  type by accident. "Choose a type" is shown first in the list.
- Q5: Should a pending request show anything in the comment column? **Assumption:** it stays empty (shown as "—").
- Q6: Is "remembers the choice" per browser, or should it follow the person? **Assumption:** per browser on that computer only (no
  login exists to follow a person).
- Q7: The LT-002 review suggested a plain message when the request body is empty or not valid JSON. Is that part of LT-003?
  **Assumption:** no. The page always sends a proper request, so a user cannot trigger it from this screen, and it stays a known API follow-up.
- Q8: Should the confirmation stay visible, or disappear after a few seconds? **Assumption:** it stays until the next send
  or until another person is picked, so keyboard and screen-reader users don't miss it.
- Q9: Should the list refresh by itself when a manager decides on a request? **Assumption:** no live updates. The list refreshes
  when the page opens, after a send, and when the person is picked again.
