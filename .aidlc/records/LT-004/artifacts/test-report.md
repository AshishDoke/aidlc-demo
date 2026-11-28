# LT-004 Manager screen for deciding on leave: Test report

## Summary
132 automated tests, all passing (0 failed, 0 skipped), run with `dotnet test app/LeaveTracker.sln`. That is 114 regression tests from
LT-001 to LT-003 (including `EmployeePageTests`, which still passes after the `site.css` additions) plus 18 new `ManagerPageTests`.
- **Structural (15):** the page and script are served, the markup and scripts carry what the ACs need, and there are no external URLs
  and no `innerHTML`.
- **API flow (3):** the main paths the page relies on: approve removes the request from the pending list, reject needs a reason,
  and deciding twice gives the "already decided" message.

No new tests were needed in this stage. Every AC already has automated evidence from these tests and LT-002's API tests.

The project allows no JavaScript or browser test tooling (project.md), so **interactive behaviour is covered by the manual checklist
below. The AI could not run it**, because the guardrails block browser and HTTP tools against the running app. The human should run it
before G3 (about 4 minutes). As a local extra, `manager.js` passed `node --check`.

## Acceptance criteria evidence
| AC | Evidence (test or manual check) | Result |
|---|---|---|
| AC1 | LeaveTracker.Tests.ManagerPageTests.Manager_page_is_served_where_the_employee_page_links; LeaveTracker.Tests.ManagerPageTests.Manager_page_links_back_to_the_employee_page | pass |
| AC2 | LeaveTracker.Tests.ManagerPageTests.Manager_page_has_the_shared_picker; LeaveTracker.Tests.ManagerPageTests.Manager_script_shares_the_remembered_choice; manual M1, M2 | pass (manual: pending human) |
| AC3 | LeaveTracker.Tests.ManagerPageTests.Manager_page_shows_pick_first_and_hides_team_section_by_default; manual M3 | pass (manual: pending human) |
| AC4 | LeaveTracker.Tests.ManagerPageTests.Not_manager_message_is_present_and_hidden_by_default; API: LeaveTracker.Tests.MyAndTeamRequestsApiTests.Non_manager_cannot_see_team_requests; manual M4 | pass (manual: pending human) |
| AC5 | LeaveTracker.Tests.ManagerPageTests.Team_table_has_all_columns (count heading above the table); LeaveTracker.Tests.ManagerPageTests.Decide_buttons_are_words (count wording); manual M5 | pass (manual: pending human) |
| AC6 | LeaveTracker.Tests.ManagerPageTests.Team_table_has_all_columns; LeaveTracker.Tests.ManagerPageTests.Approving_removes_the_request_from_the_team_list (name, dates, type, 3 days, reason "Trip"); API: LeaveTracker.Tests.MyAndTeamRequestsApiTests.Team_pending_shows_only_own_direct_reports; manual M6 | pass (manual: pending human) |
| AC7 | LeaveTracker.Tests.ManagerPageTests.Approving_removes_the_request_from_the_team_list; LeaveTracker.Tests.DecisionApiTests.Manager_approves_with_and_without_comment; manual M7 | pass (manual: pending human) |
| AC8 | LeaveTracker.Tests.ManagerPageTests.Rejecting_needs_a_reason_and_then_removes_the_request; LeaveTracker.Tests.ManagerPageTests.Decision_panel_has_labelled_text_and_error_slot; LeaveTracker.Tests.DecisionApiTests.Reject_without_reason_is_refused; manual M8 | pass (manual: pending human) |
| AC9 | LeaveTracker.Tests.ManagerPageTests.Deciding_twice_gives_the_already_decided_message; LeaveTracker.Tests.ManagerPageTests.Page_script_uses_the_expected_api_routes (403/404/409 → message and reload; field errors mapped); LeaveTracker.Tests.DecisionApiTests.Already_decided_request_cannot_be_decided_again; manual M9 | pass (manual: pending human) |
| AC10 | LeaveTracker.Tests.ManagerPageTests.Page_script_uses_the_expected_api_routes (in-flight guard, disabled confirm, Escape cancels); manual M10 | pass (manual: pending human) |
| AC11 | LeaveTracker.Tests.ManagerPageTests.Every_input_has_a_label; LeaveTracker.Tests.ManagerPageTests.Page_has_status_and_alert_regions; LeaveTracker.Tests.ManagerPageTests.Stylesheet_has_manager_styles_and_visible_focus; manual M11 | pass (manual: pending human) |
| AC12 | LeaveTracker.Tests.ManagerPageTests.Decide_buttons_are_words; manual M12 | pass (manual: pending human) |
| AC13 | manual M13 | manual: pending human |
| Safety | LeaveTracker.Tests.ManagerPageTests.Manager_page_uses_no_external_urls_or_innerHTML; LeaveTracker.Tests.ManagerPageTests.Manager_script_is_served_as_javascript | pass |

### Manual checklist (about 4 minutes)
Start the app with `dotnet run --project app/src/LeaveTracker.Api` and open the URL it prints. For a clean start, delete
`app/src/LeaveTracker.Api/leave.db` first. The demo data has one pending request (Alex Turner, "Family holiday").
- **M1 (AC2):** Open `/manager.html`. The picker lists Alex Turner, Maya Manager (manager), Priya Shah and Sam Lee.
- **M2 (AC1, AC2):**
  1. On the employee page (`/`) pick Maya Manager, then follow "Manager page". Maya is already picked.
  2. Pick Sam Lee on the manager page, then follow "Employee page". Sam is picked there.
- **M3 (AC3):** Choose "— Pick who you are —". The page says "Pick who you are to see the requests waiting for you." No list is shown.
- **M4 (AC4):** Pick Alex Turner. The page says "This page is for managers. Alex Turner is not a manager. Go to the employee page …",
  with a working link. There is no count, list or buttons.
- **M5 (AC5):** Pick Maya. The heading reads "1 request waiting for your decision".
- **M6 (AC6):**
  1. As Alex on the employee page, send 14/12/2026 → 16/12/2026, Annual, "Trip".
  2. Back as Maya, the list has a row "Alex Turner | 14 Dec 2026 | 16 Dec 2026 | Annual | 3 | Trip", and the count says 2.
- **M7 (AC7):**
  1. Choose Approve on the "Trip" row, type "Enjoy", and Confirm approval.
  2. The row disappears, "Approved: Alex Turner, annual, 14 Dec 2026 – 16 Dec 2026. 1 request left." shows, and the count says 1.
  3. As Alex on the employee page, the request is Approved with "Enjoy".
- **M8 (AC8):**
  1. On the remaining row choose Reject, leave the reason empty, and Confirm rejection.
  2. "Error: A rejection needs a reason. …" appears under the reason box, and the row stays.
  3. Type "Release week" and confirm. The row disappears, and the heading says "Nothing is waiting for your decision."
- **M9 (AC9):**
  1. As Alex, send another request, then open the manager page as Maya in **two tabs**.
  2. Approve it in tab 1.
  3. In tab 2 choose Reject, type a reason, and confirm. The message "This request was already approved by Maya Manager; only pending
     requests can be decided." shows, and the list refreshes without the row.
- **M10 (AC10):**
  1. Open Approve on a row, then press Cancel. Do the same with Escape. Nothing is decided, and focus returns to the button.
  2. Double-click Confirm approval quickly. Only one decision is made: the button shows "Sending…", and there is no "already approved"
     error.
- **M11 (AC11):** Repeat M5, M7 and M8 with the keyboard only (Tab / Shift+Tab / Enter / Space / Escape). The orange focus outline is
  always visible. After a decision, focus lands on the next row's Approve button, or on the count heading when the list is empty.
- **M12 (AC12):** In DevTools → Rendering → "Emulate vision deficiencies: Achromatopsia", Approve, Reject, the count, the confirmation
  and the errors are all readable as words.
- **M13 (AC13):** With a Reject panel or an error message open as Maya, switch to Alex, then back to Maya. The panel and messages are
  gone, Alex gets the "for managers" message, and Maya's list reloads straight away.

## Test results
```
Passed!  - Failed:     0, Passed:   132, Skipped:     0, Total:   132, Duration: 3 s - LeaveTracker.Tests.dll (net8.0)
```
