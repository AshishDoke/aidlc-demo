# LT-004 Manager screen for deciding on leave: Design

## Context
- **The API from LT-002 already does everything this page needs.** No API change is required:
  - `GET /api/employees` (anonymous): `id`, `name`, `isManager`, …
  - `GET /api/team/leave-requests/pending` (with `X-Employee-Id`) → `LeaveRequestResponse[]` of the caller's direct reports, earliest
    first day first, each with `employeeName`, `firstDay`, `lastDay`, `daysCovered`, `type`, `reason`. A non-manager gets 403 with
    "… is not a manager, so they cannot decide on leave requests."
  - `POST /api/leave-requests/{id}/approve` with body `{ comment? }` and `POST /api/leave-requests/{id}/reject` with body `{ reason }` →
    200 `LeaveRequestResponse`. Refusals: 400 `ValidationProblem` with `errors.reason` ("A rejection needs a reason. …") or
    `errors.comment` / `errors.reason` (over 500 characters); 403 (not a manager, not your team); 404 (no such request);
    409 ("This request was already approved by Maya Manager; only pending requests can be decided.").
- **LT-003 (merged on the remote `main`, `eae01c5`)** added the static front end: `Program.cs` serves `wwwroot` (`UseDefaultFiles`,
  `UseStaticFiles`), `index.html` with a hidden `<a id="manager-link" href="manager.html">` shown for managers, `css/site.css`, and
  the reusable modules `js/api.js` (`api()`, `ApiError {status, message, fieldErrors}`, `NETWORK_MESSAGE`), `js/identity.js`
  (`loadPicker`, `pickedEmployee`, `rememberEmployeeId`, storage key `leaveTracker.employeeId`) and `js/format.js` (`formatDay`,
  `typeWord`, `daysText`, `commentText`). `manager.html` gives a 404 today.
- **Precondition (spec Q8):** this run's branch `aidlc/LT-004` currently starts at `749ba49`, before LT-003. The design below
  **builds on LT-003's files**. Before IMPLEMENT, the branch must be fast-forwarded to the remote `main` (`eae01c5` or later). The
  human does this (`git switch main && git pull && git switch aidlc/LT-004 && git merge --ff-only main`). The branch has no commits
  yet, so this is a plain fast-forward. If it has not happened when IMPLEMENT starts, I will stop and say so rather than duplicate LT-003.
- Tests: 114 xUnit tests (WebApplicationFactory + SQLite in-memory), including LT-003's structural `EmployeePageTests`. No JS
  tooling (project.md).

## Approach
1. **A second static page, `wwwroot/manager.html`.** It needs no server change, because `UseStaticFiles` already serves it, which makes
   the LT-003 link work (AC1). It has the same header as the employee page (app name, the same labelled `<select id="who">`
   "Who are you?" with "— Pick who you are —", and the demo note) plus an always-visible link **"Employee page"** (`href="index.html"`).
   The main content, in order:
   - `#load-error` (`role="alert"`, hidden): the employee list or the team list could not be loaded.
   - `#pick-first` hint: "Pick who you are to see the requests waiting for you." (AC3).
   - `#not-manager` (hidden): "This page is for managers. *Name* is not a manager. Go to the **employee page** to ask for leave."
     The name is set with `textContent` and the link goes to `index.html` (AC4, spec Q3).
   - `#team-section` (hidden unless a manager is picked):
     - An `<h2 id="waiting-count" tabindex="-1">` with the count in words: "1 request waiting for your decision",
       "3 requests waiting for your decision", or "Nothing is waiting for your decision." (AC5). It is a heading, not a live region,
       so it is not announced twice. The confirmation says it.
     - `#page-status` (`role="status" aria-live="polite"`): the confirmation. `#page-error` (`role="alert"`, `tabindex="-1"`): refusals
       that aren't about the comment or reason (AC9).
     - A `<table id="team-table">` with a visually hidden caption and the columns **Employee, First day, Last day, Type, Days, Reason,
       Decide** (AC6). Every row is built with `createElement` and `textContent`. A missing reason shows "—" (`commentText`). The
       rows keep the API's order (earliest first day first, spec Q4).
   - A `<template id="decision-template">` holding the one decision panel (see 3). It lives in the HTML so its label and error slot
     can be checked by a test.
2. **The Decide cell has two buttons, "Approve" and "Reject",** as real `<button>`s with visible words. Each has an `aria-label`
   naming the request ("Approve Alex Turner, 14 Dec 2026 – 16 Dec 2026"), so a screen reader knows which row it acts on. Approve is the
   filled blue button style from LT-003. Reject is an outlined button with a dark-red border and text. The word is the difference, and
   colour only adds to it (AC12).
3. **The decision panel opens inline (spec Q2).** Choosing Approve or Reject inserts a `<tr class="decision-row">` right under that
   request, with one `<td colspan="7">` holding a clone of the template. Only one panel is open at a time: opening another closes the first.
   The panel has:
   - A heading line: "Approve Alex Turner's annual leave, 14 Dec 2026 – 16 Dec 2026 (3 days)" or the same with "Reject".
   - `<label for="decision-text">`: "Comment (optional)" for Approve, or "Reason for rejecting (required)" for Reject. A
     `<textarea id="decision-text" maxlength="500" aria-describedby="decision-hint decision-error">` (`aria-required="true"` for
     Reject only), the hint "Up to 500 characters.", and `<p id="decision-error" class="field-error">`.
   - The buttons **"Confirm approval"** / **"Confirm rejection"** (submit) and **"Cancel"**. Escape inside the panel also cancels (AC10, AC11).
   - Focus moves to the textarea when the panel opens. Cancel or Escape removes the panel and returns focus to the button that opened it.
4. **Sending a decision:** on Confirm, the confirm button is disabled and reads "Sending…", and a second click or Enter is ignored while
   a request is in flight (AC10). The page POSTs `/api/leave-requests/{id}/approve` with `{ comment }` or `/reject` with `{ reason }`, the
   text trimmed and an empty one sent as `null`. The page does **not** check the reason itself, so the system's message is always the
   one shown (spec Q2, as in LT-003).
   - **200:** the panel and row are removed, and the count and caption are updated from the remaining rows. `#page-status` says "Approved:
     Alex Turner, annual, 14 Dec 2026 – 16 Dec 2026. 0 requests left." (or "Rejected: …"). Focus moves to the next row's Approve button,
     otherwise the previous row's, otherwise the count heading. Focus is never lost to the top of the page (AC7, AC8, AC11).
   - **400 with `errors.reason` / `errors.comment`:** the message shows in `#decision-error`, the textarea gets `aria-invalid="true"` and
     focus, the text is kept, and the row stays (AC8).
   - **409, 403 or 404** (already decided, not your team, gone): the panel is closed, the system's message shows in `#page-error` (focused),
     and the list and count are reloaded so they are up to date (AC9).
   - **Network failure or anything else:** the message (`NETWORK_MESSAGE` or the API's) shows in `#decision-error`, and the panel and
     row stay so the manager can try again (AC9).
5. **Switching person (`js/manager.js`):** on change, the page remembers the choice with the shared `identity.js`, so the employee page
   picks the same person (AC2). It clears messages, closes any open panel, and shows one of nobody picked / not a manager / the team
   section. For a manager it loads `GET /api/team/leave-requests/pending` (AC13). A `listToken` (as in LT-003) ignores stale
   responses after a quick switch. If the pending call returns 403 even though `isManager` was true (data changed), the not-manager
   message is shown.
6. **Styling: small additions to `css/site.css`.** These are `.nav-link`, `.button-secondary` (the outlined Reject and Cancel style,
   with WCAG AA contrast), `.decision-row` / `.decision-panel` (a light tinted panel with a left border and padding), and
   `.button-row` (gap between buttons). The existing `:focus-visible` outline (3px) applies to everything. No existing rule changes, so
   the employee page looks the same.
7. **Shared modules are reused unchanged:** `api.js`, `identity.js` and `format.js`. `manager.js` is the only new script.

**Alternatives considered**
- *A modal dialog (`<dialog>`) for the comment or reason:* rejected. Inline panels keep the request's details in view while typing, and
  avoid focus-trap and `showModal` differences between browsers. Escape-to-cancel and returning focus give the same keyboard comfort.
- *Approve straight away, without a confirm step, with an optional comment field always visible on each row:* rejected. That is seven
  textareas on a long list, a higher chance of approving by accident, and the requirement asks for a reason prompt on Reject anyway.
  One small panel for both keeps them consistent.
- *Copying the picker and API code into `manager.js` to avoid depending on LT-003:* rejected. It would duplicate code and could make the
  two pages remember different people, which breaks AC2. Fast-forwarding the branch is simpler (see Context).
- *Showing the count from a separate count endpoint:* rejected. The count is the number of listed rows (spec Q5), so no API change is needed.

## Affected files
| Path | Change |
|---|---|
| app/src/LeaveTracker.Api/wwwroot/manager.html | create |
| app/src/LeaveTracker.Api/wwwroot/js/manager.js | create |
| app/src/LeaveTracker.Api/wwwroot/css/site.css | modify (append manager page styles only) |
| app/tests/LeaveTracker.Tests/ManagerPageTests.cs | create |

`index.html`, `api.js`, `identity.js`, `format.js`, `employee.js` and `Program.cs` are **not** changed.

## Data
None. There is no schema, entity or seed change. The only browser state is LT-003's existing `localStorage` key, now shared by both pages.

## API
No new or changed endpoints. The page uses:

| Method | Route | Request | Response | Codes |
|---|---|---|---|---|
| GET | /manager.html | — | the manager page (text/html) | 200 |
| GET | /js/manager.js | — | static file (text/javascript) | 200 |
| GET | /api/employees | — | employee list (existing) | 200 |
| GET | /api/team/leave-requests/pending | `X-Employee-Id` | `LeaveRequestResponse[]` (existing) | 200, 400, 403 |
| POST | /api/leave-requests/{id}/approve | `X-Employee-Id` + `{ comment? }` | `LeaveRequestResponse` (existing) | 200, 400, 403, 404, 409 |
| POST | /api/leave-requests/{id}/reject | `X-Employee-Id` + `{ reason }` | `LeaveRequestResponse` (existing) | 200, 400, 403, 404, 409 |

## Risks
| Risk | Mitigation |
|---|---|
| **The branch base lacks LT-003** (spec Q8) | IMPLEMENT starts by checking that `wwwroot/js/identity.js` exists. If it doesn't, I stop and ask the human to fast-forward rather than duplicate the code. |
| An employee's reason, or a name, containing HTML or script (XSS) on the manager's screen | All dynamic text uses `textContent` / `createElement`. A test asserts `manager.js` has no `innerHTML`, `outerHTML`, `insertAdjacentHTML` or `document.write`. |
| Anyone can pick a manager (no login) | This is the known demo limitation, and the demo note is on the page. The API still enforces manager-only and team-only on every call. The page hides the list for non-managers only for convenience. |
| A double click sends the same decision twice | The confirm button is disabled while sending, with an in-flight flag. Even if a second request did get through, the API answers 409 and the page shows that message and refreshes (AC9). |
| Two managers or two tabs deciding at once | The 409, 403 or 404 → message + reload path (AC9). |
| Focus lost after a row is removed (keyboard and screen-reader users) | Focus moves to the next row's Approve, then the previous row's, then the count heading. This is in the manual checklist. |
| Changing the shared `site.css` alters the employee page | Rules are only appended, with new class names. LT-003's `EmployeePageTests` (focus style, status styles) keep running. |
| The UI is not covered by automated browser tests | The structural tests (below) check the served markup and scripts, the API behaviour is covered by LT-002's `DecisionApiTests` / `MyAndTeamRequestsApiTests` plus one end-to-end flow test here, and there is a short manual checklist for G3. |
| New dependency | None. No NuGet, npm, CDN or fonts. |

## Test strategy
`ManagerPageTests` (new, WebApplicationFactory) checks the served page and script structurally, as `EmployeePageTests` does, and adds one
API flow test for the page's main path. Interactive behaviour is a manual check (project.md rules out JS test tooling).

| AC | Test type | Test |
|---|---|---|
| AC1 | Integration | `ManagerPageTests.Manager_page_is_served_where_the_employee_page_links` (GET `/manager.html` → 200 text/html; the `href` of `index.html`'s `#manager-link` resolves to it); `Manager_page_links_back_to_the_employee_page` |
| AC2 | Integration + manual | `ManagerPageTests.Manager_page_has_the_shared_picker` (same `<select id="who">` and placeholder; `manager.js` imports `./identity.js` and calls `rememberEmployeeId`). Manual M2: pick Maya on the employee page, open the manager page → Maya is picked, and the reverse. |
| AC3 | Integration + manual | `Manager_page_shows_pick_first_and_hides_team_section_by_default` (`#pick-first` visible, `#team-section` hidden). Manual M3. |
| AC4 | Integration + manual | `Not_manager_message_is_present_and_hidden_by_default` (`#not-manager` hidden, links to `index.html`). Manual M4: pick Alex → the message, and no count, list or buttons. |
| AC5 | Integration + manual | `Team_table_has_all_columns` (count heading present). Manual M5: Maya shows "1 request waiting for your decision". After deciding it, "Nothing is waiting for your decision." |
| AC6 | Integration + manual | `Team_table_has_all_columns` (Employee, First day, Last day, Type, Days, Reason, Decide). API: `MyAndTeamRequestsApiTests` (team only, pending only, earliest first). Manual M6. |
| AC7 | Integration + manual | `ManagerPageTests.Approving_removes_the_request_from_the_team_list` (as Maya: pending has Alex's request → approve with a comment → 200 approved → pending list no longer has it, and Alex's own list shows it approved with the comment). Manual M7. |
| AC8 | Integration + manual | `ManagerPageTests.Rejecting_needs_a_reason_and_then_removes_the_request` (reject with spaces → 400 `errors.reason`, still pending → reject with "Release week" → gone from pending). `Decision_panel_has_labelled_text_and_error_slot` (template: `<label for="decision-text">`, `aria-describedby` → `decision-error`). Manual M8. |
| AC9 | Integration + manual | `Page_script_uses_the_expected_api_routes` (pending, approve, reject routes; `fieldErrors.reason` / `fieldErrors.comment` handled; reload on 409, 403, 404). API: `DecisionApiTests` (409 already decided, 403 not your team). Manual M9: decide in two tabs. |
| AC10 | Manual | Manual M10: open a panel → Cancel and Escape decide nothing. Double-click Confirm → one decision only (the button shows "Sending…"). |
| AC11 | Integration + manual | `Every_input_has_a_label`; `Page_has_status_and_alert_regions` (`#page-status` `role="status"`, `#page-error` `role="alert"`); the LT-003 focus-style test still applies to the shared CSS. Manual M11: the whole flow with the keyboard only. |
| AC12 | Integration + manual | `Decide_buttons_are_words` (the "Approve", "Reject", "Confirm approval", "Confirm rejection" and "Cancel" texts present). Manual M12: achromatopsia emulation. |
| AC13 | Manual | Manual M13: switch Maya → Alex → Maya while a panel or message is open. Everything changes straight away. |
| Safety | Integration | `Manager_page_uses_no_external_urls_or_innerHTML` (manager.html and manager.js); `Manager_script_is_served_as_javascript` |
| Regression | Integration | All existing tests, including `EmployeePageTests`, still pass. |

The manual checklist (M1–M13, with M1 = the picker lists the 4 demo people) will be written out in the test report, and takes about 4 minutes.
