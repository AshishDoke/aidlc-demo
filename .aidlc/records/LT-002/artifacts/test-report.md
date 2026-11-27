# LT-002 Ask for leave and decide on it: Test report

## Summary
97 tests, all passing (0 failed, 0 skipped), with `dotnet test app/LeaveTracker.sln`. That is 37 LT-001 regression tests
(two strengthened, with the `Z` and `daysCovered` assertions) plus 60 new ones. Every LT-002 acceptance criterion already had automated evidence
from IMPLEMENT, so no extra `[TEST]` commits were needed. All HTTP behaviour is tested through WebApplicationFactory
on SQLite in-memory. No running server is needed.

## Acceptance criteria evidence
| AC | Evidence (test or manual check) | Result |
|---|---|---|
| AC1 | LeaveTracker.Tests.IdentityApiTests.Employee_list_needs_no_picked_person | pass |
| AC2 | LeaveTracker.Tests.AskForLeaveApiTests.Employee_asks_for_leave_and_it_is_pending; LeaveTracker.Tests.LeaveRequestServiceTests.Asking_for_self_is_pending | pass |
| AC3 | LeaveTracker.Tests.IdentityApiTests.Actions_without_picked_person_are_refused (15 cases: no header / "abc" / unknown id × ask, approve, reject, own list, team list) | pass |
| AC4 | LeaveTracker.Tests.AskForLeaveApiTests.Last_day_before_first_day_is_refused; Bad_type_is_refused (3); Bad_or_missing_dates_are_refused (4); Reason_too_long_is_refused | pass |
| AC5 | LeaveTracker.Tests.AskForLeaveApiTests.Asking_for_someone_else_is_refused; LeaveTracker.Tests.LeaveRequestServiceTests.Asking_for_someone_else_is_forbidden | pass |
| AC6 | LeaveTracker.Tests.MyAndTeamRequestsApiTests.Own_requests_are_all_states_newest_first; Employee_with_no_requests_gets_an_empty_list | pass |
| AC7 | LeaveTracker.Tests.MyAndTeamRequestsApiTests.Team_pending_shows_only_own_direct_reports | pass |
| AC8 | LeaveTracker.Tests.MyAndTeamRequestsApiTests.Non_manager_cannot_see_team_requests; LeaveTracker.Tests.DecisionApiTests.Non_manager_cannot_decide (2) | pass |
| AC9 | LeaveTracker.Tests.DecisionApiTests.Manager_approves_with_and_without_comment (2); LeaveTracker.Tests.LeaveRequestServiceTests.Approval_without_comment_uses_clock_time | pass |
| AC10 | LeaveTracker.Tests.DecisionApiTests.Manager_rejects_with_reason; Reject_without_reason_is_refused (3); LeaveTracker.Tests.LeaveRequestServiceTests.Rejection_without_reason_is_refused (3) | pass |
| AC11 | LeaveTracker.Tests.DecisionApiTests.Cannot_decide_request_outside_team (other team + own request); LeaveTracker.Tests.LeaveRequestServiceTests.Request_outside_team_is_forbidden | pass |
| AC12 | LeaveTracker.Tests.DecisionApiTests.Already_decided_request_cannot_be_decided_again; Seeded_rejected_request_cannot_be_approved; LeaveTracker.Tests.LeaveRequestServiceTests.Already_decided_is_a_conflict_naming_the_decision | pass |
| AC13 | LeaveTracker.Tests.LeaveRequestApiTests.Get_request_by_id_returns_it; Get_unknown_request_returns_404_with_plain_message; LeaveTracker.Tests.DecisionApiTests.Deciding_unknown_request_is_404 (2); LeaveTracker.Tests.LeaveRequestServiceTests.Deciding_unknown_request_is_not_found | pass |
| AC14 | LeaveTracker.Tests.LeaveRequestServiceTests.Days_covered_is_inclusive_calendar_days (5: same day, 3 days, month end, year end, leap year); `daysCovered` also asserted in LeaveRequestApiTests.Fresh_start_has_requests_in_every_state, AskForLeaveApiTests.Employee_asks_for_leave_and_it_is_pending, MyAndTeamRequestsApiTests.* and DecisionApiTests.Manager_approves_with_and_without_comment | pass |

LT-001 regression: all 37 LT-001 tests pass unchanged, except two strengthened assertions in `LeaveRequestApiTests`
(decision time ends with `Z`, and `daysCovered` is correct). This covers spec Q10 and the LT-001 review item 1 fix.

Optional manual check (about 2 minutes): run `dotnet run --project app/src/LeaveTracker.Api`. Using any HTTP client
(for example the VS Code REST Client), send `X-Employee-Id` with Alex's id from `/api/employees` and POST `/api/leave-requests`
`{"firstDay":"2026-12-14","lastDay":"2026-12-16","type":"annual"}`. Expect 201 with `"daysCovered":3`. As Maya, GET
`/api/team/leave-requests/pending`, then POST `/api/leave-requests/{id}/approve`. POST it again and expect a 409 with
"already approved by Maya Manager".

## Test results
```
Passed!  - Failed:     0, Passed:    97, Skipped:     0, Total:    97, Duration: 8 s - LeaveTracker.Tests.dll (net8.0)
```
