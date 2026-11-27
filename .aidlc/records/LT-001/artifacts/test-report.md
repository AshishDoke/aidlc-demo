# LT-001 Keep track of employees and their leave: Test report

## Summary
37 tests, all passing (0 failed, 0 skipped), with `dotnet test app/LeaveTracker.sln`. Every acceptance
criterion already had automated evidence from IMPLEMENT, so no extra test commits were needed in this stage.
All tests use SQLite in-memory and the in-process host (WebApplicationFactory). No running server is needed.

## Acceptance criteria evidence
| AC | Evidence (test or manual check) | Result |
|---|---|---|
| AC1 | LeaveTracker.Tests.HealthTests.Health_returns_200_and_database_is_created | pass |
| AC2 | LeaveTracker.Tests.EmployeeApiTests.Fresh_start_has_one_manager_and_three_reports; LeaveTracker.Tests.SeedTests.Seed_creates_the_designed_demo_team | pass |
| AC3 | LeaveTracker.Tests.SeedTests.Seeding_twice_does_not_duplicate; LeaveTracker.Tests.SeedTests.Restarting_the_app_does_not_duplicate; LeaveTracker.Tests.SeedTests.Seeding_is_skipped_when_any_employee_exists | pass |
| AC4 | LeaveTracker.Tests.EmployeeServiceTests.Duplicate_email_is_refused_ignoring_case | pass |
| AC5 | LeaveTracker.Tests.EmployeeServiceTests.Non_manager_without_manager_is_refused; LeaveTracker.Tests.EmployeeServiceTests.Non_manager_whose_manager_is_not_a_manager_is_refused; LeaveTracker.Tests.EmployeeServiceTests.Non_manager_with_unknown_manager_is_refused | pass |
| AC6 | LeaveTracker.Tests.LeaveRequestApiTests.Fresh_start_has_requests_in_every_state | pass |
| AC7 | LeaveTracker.Tests.LeaveRequestApiTests.Decided_requests_show_decider_time_and_comment; LeaveTracker.Tests.LeaveRequestServiceTests.Decision_only_from_pending_and_by_a_manager | pass |
| AC8 | LeaveTracker.Tests.LeaveRequestServiceTests.New_request_is_pending_without_decision | pass |
| AC9 | LeaveTracker.Tests.LeaveRequestServiceTests.Last_day_before_first_day_is_refused; LeaveTracker.Tests.LeaveRequestServiceTests.Undefined_leave_type_is_refused; LeaveTracker.Tests.LeaveRequestServiceTests.Missing_leave_type_is_refused; LeaveTracker.Tests.LeaveRequestServiceTests.Unknown_requester_is_refused; LeaveTracker.Tests.LeaveRequestServiceTests.Missing_days_are_refused | pass |
| AC10 | LeaveTracker.Tests.LeaveRequestServiceTests.Single_day_request_is_accepted | pass |

Supporting tests for open-question assumptions: Q2 `EmployeeServiceTests.Manager_without_manager_is_accepted`;
Q7 `LeaveRequestServiceTests.Reason_over_500_chars_is_refused`, `Decision_comment_over_500_chars_is_refused`,
`Reason_is_optional_and_blank_is_stored_as_none`; Q8 `EmployeeServiceTests.Missing_or_malformed_name_or_email_is_refused`
(8 cases), `Email_is_stored_trimmed_with_normalized_copy`; Q9/Q10 `SeedTests.*`; 404 handling
`EmployeeApiTests.Get_unknown_employee_returns_404_with_plain_message`, `LeaveRequestApiTests.Get_unknown_request_returns_404_with_plain_message`.

Optional manual check (under a minute): run `dotnet run --project app/src/LeaveTracker.Api`, then open
`/api/health`, `/api/employees` (4 people, 1 manager) and `/api/leave-requests` (pending, approved and rejected).
Stop the app, start it again, and confirm the counts are unchanged.

## Test results
```
Passed!  - Failed:     0, Passed:    37, Skipped:     0, Total:    37, Duration: 2 s - LeaveTracker.Tests.dll (net8.0)
```
