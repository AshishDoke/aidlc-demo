# LT-003 Employee screen for requesting leave: Test report

## Summary
114 automated tests, all passing (0 failed, 0 skipped), with `dotnet test app/LeaveTracker.sln`. That is 97 regression tests from
LT-001/LT-002 plus 17 new `EmployeePageTests`. The automated tests check that the page and its assets are served and that the markup
and scripts have what the acceptance criteria need. The API behaviour the page relies on is covered by LT-002's API tests. The project
allows no JavaScript or browser test tooling (project.md), so **interactive behaviour is covered by the manual checklist below. The AI
could not run it**, because the guardrails block browser and HTTP tools. The human should run it before G3 (about 3 minutes). As a local
extra, the JS modules passed `node --check`, and `format.js` was smoke-tested ("14 Dec 2026", "Rejected", "1 day", "—").

## Acceptance criteria evidence
| AC | Evidence (test or manual check) | Result |
|---|---|---|
| AC1 | LeaveTracker.Tests.EmployeePageTests.Start_address_serves_the_employee_page; LeaveTracker.Tests.EmployeePageTests.Api_routes_still_work_alongside_static_files | pass |
| AC2 | LeaveTracker.Tests.EmployeePageTests.Page_has_picker_and_form_disabled_until_picked; manual M1 | pass (manual: pending human) |
| AC3 | LeaveTracker.Tests.EmployeePageTests.Identity_is_remembered_in_local_storage_safely; manual M2 | pass (manual: pending human) |
| AC4 | LeaveTracker.Tests.EmployeePageTests.Identity_is_remembered_in_local_storage_safely (unknown id forgotten); manual M3 | pass (manual: pending human) |
| AC5 | LeaveTracker.Tests.EmployeePageTests.Page_script_uses_the_expected_api_routes; API: LeaveTracker.Tests.AskForLeaveApiTests.Employee_asks_for_leave_and_it_is_pending; manual M4 | pass (manual: pending human) |
| AC6 | LeaveTracker.Tests.EmployeePageTests.Page_has_error_and_status_regions; LeaveTracker.Tests.EmployeePageTests.Page_script_uses_the_expected_api_routes (field → error slot mapping); API: LeaveTracker.Tests.AskForLeaveApiTests.Last_day_before_first_day_is_refused; manual M5 | pass (manual: pending human) |
| AC7 | LeaveTracker.Tests.EmployeePageTests.Page_has_error_and_status_regions; API: LeaveTracker.Tests.AskForLeaveApiTests.Bad_or_missing_dates_are_refused, Bad_type_is_refused; manual M6 | pass (manual: pending human) |
| AC8 | LeaveTracker.Tests.EmployeePageTests.My_requests_table_has_all_columns; API: LeaveTracker.Tests.MyAndTeamRequestsApiTests.Own_requests_are_all_states_newest_first; manual M7 | pass (manual: pending human) |
| AC9 | LeaveTracker.Tests.EmployeePageTests.Manager_link_is_hidden_by_default; LeaveTracker.Tests.EmployeePageTests.Page_script_uses_the_expected_api_routes (`person.isManager`); manual M8 | pass (manual: pending human) |
| AC10 | LeaveTracker.Tests.EmployeePageTests.Every_input_has_a_label; LeaveTracker.Tests.EmployeePageTests.Stylesheet_has_visible_focus_style; LeaveTracker.Tests.EmployeePageTests.Page_has_error_and_status_regions; manual M9 | pass (manual: pending human) |
| AC11 | LeaveTracker.Tests.EmployeePageTests.Status_is_rendered_as_text; manual M10 | pass (manual: pending human) |
| AC12 | manual M11 | manual: pending human |
| Safety | LeaveTracker.Tests.EmployeePageTests.Frontend_uses_no_external_urls_or_innerHTML; LeaveTracker.Tests.EmployeePageTests.Static_assets_are_served_with_correct_types (5) | pass |

### Manual checklist (about 3 minutes)
Start the app with `dotnet run --project app/src/LeaveTracker.Api` and open the URL it prints (for example `http://localhost:5000/`).
For a clean start, delete `app/src/LeaveTracker.Api/leave.db` first.
- **M1 (AC2):** The picker lists Alex Turner, Maya Manager (manager), Priya Shah and Sam Lee. With nobody picked, the form is greyed out and says "Pick who you are to ask for leave."
- **M2 (AC3):** Pick Alex Turner, then reload the page. Alex is still picked and his pending request is listed.
- **M3 (AC4):** In DevTools → Console run `localStorage.setItem('leaveTracker.employeeId','9999')`, then reload. Nobody is picked, the hint shows, and there is no error message.
- **M4 (AC5):** As Alex, enter 14/12/2026 → 16/12/2026, Annual, reason "Trip", and Send. The green message "Request sent: 14 Dec 2026 – 16 Dec 2026, annual, 3 days. It is pending …" shows, the form is cleared, and the top row reads 14 Dec 2026 | 16 Dec 2026 | Annual | 3 | Pending | —.
- **M5 (AC6):** Enter first day 16/12/2026, last day 14/12/2026 and Annual, then Send. "Error: The last day (2026-12-14) is before the first day (2026-12-16)." appears under Last day, focus moves to Last day, the values stay, and no new row is added.
- **M6 (AC7):** Send with no dates or type. Messages show under each field. Then stop the server and Send. The red message shows "… Could not reach the leave tracker. Check that it is running and try again."
- **M7 (AC8):** Pick Sam Lee. There are two rows, one Rejected with "Busy release week, please pick another day." and one Approved with "—". Pick Maya Manager, and the page says "You have no leave requests yet."
- **M8 (AC9):** With Maya Manager picked, a "Manager page" link is visible (it gives a 404 until LT-004). With Alex picked, the link is gone.
- **M9 (AC10):** Do M2, M4 and M8 with the keyboard only (Tab / Shift+Tab / arrows / Enter / Space). The orange focus outline is always visible.
- **M10 (AC11):** Statuses read as words. In DevTools → Rendering → "Emulate vision deficiencies: Achromatopsia", the three statuses are still distinguishable (word + dashed / solid / double border).
- **M11 (AC12):** Switch from Sam to Alex while an error message is showing. The list, the manager link and the messages change straight away.

## Test results
```
Passed!  - Failed:     0, Passed:   114, Skipped:     0, Total:   114, Duration: 8 s - LeaveTracker.Tests.dll (net8.0)
```
