# LT-004 Manager screen for deciding on leave: Plan

**Precondition (design "Context", spec Q8):** before T1, the branch `aidlc/LT-004` must contain LT-003 (`wwwroot/js/identity.js`
exists). If it doesn't, IMPLEMENT stops and asks the human to fast-forward the branch to `main`. Nothing is duplicated.

## Tasks
- **T1**: Serve the manager page. Add `wwwroot/manager.html` with:
  - the same header and picker as the employee page, plus an "Employee page" link
  - the `#load-error`, `#pick-first` and `#not-manager` messages
  - the hidden `#team-section` with the count heading, `#page-status` / `#page-error` regions and the team table (Employee, First day,
    Last day, Type, Days, Reason, Decide)
  - the `<template id="decision-template">` panel, with a labelled textarea, error slot, Confirm and Cancel

  Append the manager styles to `css/site.css` (`.nav-link`, `.button-secondary`, `.decision-panel`, `.button-row`). Add `ManagerPageTests`
  for: the page served at the LT-003 link, the back link, the shared picker, the pick-first hint and hidden team section, the hidden
  not-manager message, table columns and count heading, the labelled decision panel, the status and alert regions, every input labelled,
  and decide buttons as words. (covers AC1, AC3, AC4, AC5, AC6, AC8, AC11, AC12)
- **T2**: Manager page behaviour. Add `wwwroot/js/manager.js`, reusing `api.js`, `identity.js` and `format.js` unchanged.
  - **Picking a person:** it remembers the shared choice, then shows nobody-picked, not-a-manager or the team section, and loads the
    team's pending list using a list token.
  - **Rows:** each row is built with `textContent`, with Approve / Reject buttons labelled with the request.
  - **The inline decision panel:** only one is open at a time. Focus goes to the textarea. Cancel or Escape closes it and returns focus.
  - **Confirm:** the button is disabled while sending ("Sending…"). It POSTs approve `{comment}` or reject `{reason}`.
    - On success: remove the row, update the count, show a confirmation and move focus to the next row.
    - On 400: show the field error beside the textarea.
    - On 409, 403 or 404: show the message and reload.
    - On a network failure: show the error in the panel and keep it open.

  Extend `ManagerPageTests` with:
  - the script served as JavaScript, wired to the shared identity and the expected routes, with no external URLs and no `innerHTML`
  - the API flow tests "approving removes the request from the team list" and "rejecting needs a reason and then removes the request"

  (covers AC2, AC7, AC8, AC9, AC10, AC11, AC13)

`allowed_files` lists exactly the design's "Affected files". No other repository file is touched.

## Plan data
```aidlc-plan
{
  "tasks": [
    {"id": "T1", "title": "Serve the manager page: HTML structure, styles and page tests", "covers": ["AC1", "AC3", "AC4", "AC5", "AC6", "AC8", "AC11", "AC12"]},
    {"id": "T2", "title": "Manager page behaviour: list pending requests, approve and reject, switch person", "covers": ["AC2", "AC7", "AC8", "AC9", "AC10", "AC11", "AC13"]}
  ],
  "allowed_files": [
    "app/src/LeaveTracker.Api/wwwroot/manager.html",
    "app/src/LeaveTracker.Api/wwwroot/js/manager.js",
    "app/src/LeaveTracker.Api/wwwroot/css/site.css",
    "app/tests/LeaveTracker.Tests/ManagerPageTests.cs"
  ],
  "test_command": "dotnet test app/LeaveTracker.sln"
}
```
