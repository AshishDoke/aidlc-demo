# LT-004 Manager screen for deciding on leave: Implementation notes

## Summary
This adds the manager page (`wwwroot/manager.html` + `js/manager.js`) at the address the LT-003 "Manager page" link already pointed to.
- **Who are you:** the same picker as the employee page, sharing the remembered choice.
- **Who sees what:** a friendly message for non-managers. For managers, the waiting count at the top and the team's pending requests
  (employee, dates, type, days and reason).
- **Deciding:** Approve or Reject on each request opens an inline panel (optional comment, or required reason) with Confirm and Cancel.

The page reuses LT-003's `api.js`, `identity.js` and `format.js` unchanged. `site.css` only gained new rules at the end.
There are no API, schema, `Program.cs` or package changes. 132 tests pass: 114 earlier plus 18 new `ManagerPageTests`. `manager.js` was
also syntax-checked locally with `node --check` (a one-off check; no Node tooling was added).

Before T1, the precondition from the design was checked. The branch was fast-forwarded by the human to `eae01c5`, so it includes LT-003.

## Tasks
### T1: Serve the manager page: HTML structure, styles and page tests
- Commit: 54d79e7
- Changes:
  - `wwwroot/manager.html`:
    - the header with the shared picker and an "Employee page" link
    - the load-error, pick-first and not-manager messages
    - the hidden team section with the count `<h2 tabindex="-1">`, status and alert regions, and the 7-column table
    - a `<template id="decision-template">` holding the decision panel: a labelled textarea, hint and error slot, Confirm and Cancel
  - `css/site.css`: appended `.page-name`, `.nav-link`, `.message-info`, `.button-secondary`, `.decision-cancel`, `.button-row`,
    `.decision-row` and `.decision-panel`.
  - `ManagerPageTests` (10 structural tests).
- Notes:
  - The panel markup lives in a `<template>` in the HTML rather than being built in JS, so the label, `aria-describedby` and
    `maxlength` can be checked by a test (AC8/AC11).
  - Reject is the outlined dark-red `.button-secondary`, and Approve keeps LT-003's filled blue button. Both are words.

### T2: Manager page behaviour: list pending requests, approve and reject, switch person
- Commit: a4e596e
- Changes: `js/manager.js` and 8 more `ManagerPageTests`. Of those, 5 are structural (script type, shared identity, routes and
  error handling, words, no external URLs or `innerHTML`) and 3 are API flow tests (approve removes from the pending list and Alex
  sees the comment; reject needs a reason and then removes; deciding twice gives "already approved by Maya Manager").
- How the page behaves:
  - **Picking a person:** nobody picked → hint. Not a manager → "This page is for managers. *Name* is not a manager." with a link.
    Manager → loads `GET /api/team/leave-requests/pending`. A `listToken` ignores stale responses after a quick switch.
  - **Rows:** built with `textContent` only. Each Approve or Reject button has an `aria-label` naming the employee and dates.
  - **The panel:** only one is open at a time. It opens under its row, and focus goes to the textarea. Reject relabels it "Reason for
    rejecting (required)" and sets `aria-required`. Cancel or Escape closes it and returns focus to the opening button.
  - **Confirm:** an in-flight flag plus a disabled "Sending…" button stop double sends (AC10).
    - Success: the row is removed, the count is recalculated from the rows on screen, "Approved: Alex Turner, annual, 14 Dec 2026 –
      16 Dec 2026. 0 requests left." is shown, and focus moves to the next row's Approve (else the previous row's, else the count heading).
    - 400 with `errors.reason`/`errors.comment`: the message goes in the panel's error slot and the textarea is marked `aria-invalid`.
    - 403, 404 or 409: the panel closes, the message shows in the alert (focused), and the list reloads (AC9).
    - Network or other errors: "Nothing was decided. …" in the panel, which stays open.
- Notes:
  - For a non-field error, the first draft moved focus to the Confirm button. That button is still disabled at that moment, so focus
    would have been lost. Focus now always goes to the textarea, which is linked to the error slot by `aria-describedby`.
  - If the team list call returns 403 for someone the picker said was a manager (data changed), the not-manager message is shown.
