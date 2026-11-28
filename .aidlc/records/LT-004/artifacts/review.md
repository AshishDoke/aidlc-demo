# LT-004 Manager screen for deciding on leave: AI review

## Summary
- **The page:** `/manager.html`, where the LT-003 "Manager page" link already pointed. It has the shared "Who are you?" picker (same
  remembered choice as the employee page) and an "Employee page" link back.
- **Who sees what:** nobody picked → a hint. A non-manager → "This page is for managers. *Name* is not a manager." with a link.
  A manager → the waiting count in words, and a 7-column table of the team's pending requests (employee, dates, type, days, reason, decide).
- **Deciding:** Approve or Reject opens one inline panel under the row (optional comment, or a required reason) with Confirm and Cancel.
  Escape also cancels.
  - On success, the row is removed, the count updates, a confirmation is announced, and focus moves to the next row.
  - A refusal shows the system's message. On 403, 404 or 409 the list also reloads.
- **What changed:** only `manager.html`, `js/manager.js`, rules appended to `site.css`, and 18 tests. LT-003's `api.js`, `identity.js`
  and `format.js` are reused unchanged. There are no API, schema, package or `Program.cs` changes.

## Acceptance criteria verification
| AC | Met? | Evidence |
|---|---|---|
| AC1 | Yes | `manager.html`, `#employee-link`. `ManagerPageTests.Manager_page_is_served_where_the_employee_page_links`, `Manager_page_links_back_to_the_employee_page` |
| AC2 | Yes by design (manual M1–M2 pending) | `manager.js` uses `loadPicker` / `rememberEmployeeId` from `identity.js` (one storage key). `Manager_page_has_the_shared_picker`, `Manager_script_shares_the_remembered_choice` |
| AC3 | Yes (markup automated; manual M3) | `#pick-first` visible and `#team-section` hidden by default. `applyPerson(null)`. `Manager_page_shows_pick_first_and_hides_team_section_by_default` |
| AC4 | Yes (markup automated; manual M4) | `#not-manager` with the name via `textContent`, plus a link. The API also refuses (403). `Not_manager_message_is_present_and_hidden_by_default`, `MyAndTeamRequestsApiTests.Non_manager_cannot_see_team_requests` |
| AC5 | Yes by design (manual M5) | `countText` / `updateCount` computes the count from the rows on screen. `Team_table_has_all_columns`, `Decide_buttons_are_words` |
| AC6 | Yes | `renderRows` (reason "—" when empty, API order). `Team_table_has_all_columns`, `Approving_removes_the_request_from_the_team_list`, `MyAndTeamRequestsApiTests.Team_pending_shows_only_own_direct_reports` |
| AC7 | Yes, **with finding 1** (manual M7) | `onConfirm` success path. `Approving_removes_the_request_from_the_team_list` |
| AC8 | Yes (manual M8) | `fieldErrors.reason` → panel error slot + `aria-invalid`. `Rejecting_needs_a_reason_and_then_removes_the_request`, `Decision_panel_has_labelled_text_and_error_slot` |
| AC9 | Yes (manual M9) | 403/404/409 → `#page-error` (focused) + `loadTeam()`. `Deciding_twice_gives_the_already_decided_message`, `Page_script_uses_the_expected_api_routes` |
| AC10 | **Partly: finding 1** (manual M10) | Cancel and Escape work. Confirm is disabled while sending, so the same decision can't be sent twice. But opening another row's panel while sending makes the page drop the first result (finding 1). |
| AC11 | Yes by design (manual M11) | Native buttons and textarea, labels, `role=status`/`alert`, focus to the textarea, back to the opener on cancel, then to the next row or the count heading. `Every_input_has_a_label`, `Page_has_status_and_alert_regions`, `Stylesheet_has_manager_styles_and_visible_focus` |
| AC12 | Yes (manual M12) | Approve, Reject, Confirm approval, Confirm rejection and Cancel are all words. Reject is outlined; colour is extra. `Decide_buttons_are_words` |
| AC13 | Yes by design (manual M13) | `applyPerson` closes the panel, clears messages and reloads. `listToken` ignores stale lists. |

**Important:** AC2–AC5 and AC7–AC13 depend on browser behaviour that **no automated test runs** (project.md allows no JS or browser
tooling, and my guardrails block driving a browser or calling the running app). They were checked by reading the code and with `node --check`.
**Please run the 13-step manual checklist in `test-report.md` before G3.**

## Code quality
1. **Defect (minor, real): a decision's result can be lost if another row is opened while it is being sent.** `openDecision` calls
   `closePanel(false)`, which also resets `sending`. The in-flight `onConfirm` then sees `openPanel !== panel` and returns.
   - **What happens:** the server has recorded the decision, but the row stays on screen, the count is not updated, and no confirmation
     is shown. If the manager then decides that row again, they get the 409 "already approved" message and the list reloads, so the
     data is never wrong. The screen is just misleading for a moment.
   - **How likely:** it needs a click on another row's Approve or Reject inside the ~100 ms a local request takes.
   - **The fix:** add `if (sending) return;` at the start of `openDecision`, or keep the result and remove the row when the request
     completes. About one line of code plus a structural test assertion.
   - **Why it isn't fixed here:** REVIEW may only write `review.md`.
2. **Structure:** `manager.js` follows `employee.js`'s pattern (an `el`, `show` and `setText` helper set, `listToken`, `textContent` only).
   The decision panel markup lives in a `<template>` in the HTML, so its accessibility attributes can be tested. The small helpers `show`,
   `setText` and `el` are now duplicated between `employee.js` and `manager.js` (about 10 lines). Moving them into a shared module would
   have meant changing LT-003 files outside this plan, so it was left as a possible tidy-up.
3. **Error handling:** every API outcome has a path: 200; 400 with a field (panel); 400 without a field or a network failure (panel,
   "Nothing was decided. …"); 403, 404 or 409 (alert + reload); the team list returning 403 → the not-manager message. Messages are the
   system's own words.
4. **Tests are structural and partly tied to exact code text** (for example `if (!panel || sending || !current)`). This is the same
   trade-off as LT-003: without a DOM library they catch wiring regressions, but a harmless refactor may need test updates. The three
   API flow tests are behavioural.
5. **CSS:** only appended rules with new class names. LT-003's `EmployeePageTests` still pass. The outlined Reject uses `--error-text` on
   white (about 8:1 contrast).
6. **Process note:** the run was created from a stale local `main` (`749ba49`, without LT-003). The human fast-forwarded the branch,
   and then by hand changed the run's recorded `base_sha` to `eae01c5` so the scan counts only this run's changes. The audit log still
   shows the original base at run creation. Spec Q8 and the decision notes describe this. Also, `requirements/LT-004.md` front matter
   still lists only LT-002 as a dependency, although the page relies on LT-003 (spec "Dependencies").

## Security
- **No injection from user data:** employee names and reasons are inserted with `textContent` / `createElement` only. A test fails if
  `manager.js` uses `innerHTML`, `outerHTML`, `insertAdjacentHTML` or `document.write`.
- **No third-party code:** there are no external URLs in `manager.html` or `manager.js` (tested), and no CDN or fonts.
- **Identity is still not authentication.** Anyone can pick Maya and approve leave. This is the documented demo limitation, and the
  page shows "Demo: there is no login." The API from LT-002 enforces manager-only, team-only and pending-only on every decision. The page
  hides the list for non-managers only for convenience.
- **Stored in the browser:** only LT-003's employee id key. `manager.js` doesn't touch `localStorage` itself (tested).
- **No API surface added**, and no personal data is logged.

## Guardrail report
- Files changed: 4 · Lines changed: 817 · Violations: 0
- Tests: 132 passed, 0 failed, 0 skipped (re-run by the engine in TEST).

## Recommendation
**APPROVE, with finding 1 as an accepted known issue** (the human's decision after the rework attempt; see below). Apart from finding 1,
everything is in place: the guardrail scan is clean, there are no new dependencies or API changes, and every AC has evidence. Finding 1
never makes the data wrong (the API stays the source of truth, and the next action corrects the screen), but it does weaken AC10 until it
is fixed. **The interactive ACs have only been verified by code reading, so please run the manual checklist (M1–M13 in `test-report.md`)
before approving G3.**

## Changes after review
- **G3 feedback (2026-11-28, Ashish Doke):** "Fix review finding 1".
- **What happened:** the rework returned to REVIEW, where the guard only allows writing `review.md`. The engine grants code paths only in
  IMPLEMENT and TEST. The one-line edit to `manager.js` was blocked and was not worked around (noted in the audit log). The human
  chose to **accept finding 1 as a known issue** and fix it in a follow-up, instead of reopening the run through G2.
- **The follow-up fix**, to be carried into LT-005 or a small new requirement: at the start of `openDecision` in
  `wwwroot/js/manager.js`, add `if (sending) { return; }`, so another row's panel cannot open while a decision is in flight. Add a
  matching assertion to `ManagerPageTests.Page_script_uses_the_expected_api_routes`.
- **Engine gap for the AIDLC maintainers:** rejecting G3 sends the run to REVIEW, where code cannot be changed, although the review skill
  says fixes are allowed there. Either REVIEW should get the plan's `allowed_files`, or a G3 rejection should be able to return to IMPLEMENT.
