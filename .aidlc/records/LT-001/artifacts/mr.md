# LT-001: Keep track of employees and their leave

## Summary
This lays the foundation of the Team Leave Tracker. It creates the .NET 8 minimal API and the test project. The system now
stores employees (unique email ignoring case, and every non-manager reports to exactly one manager) and leave
requests (requester, inclusive first/last day, annual/sick/unpaid, optional reason, pending/approved/rejected with
who decided, when and a comment). On first start the app creates a fictional demo team (1 manager, 3 reports,
4 requests), and restarting never duplicates it. Read-only endpoints let you check the data. There are no screens and
no request or decision actions; those come in LT-002.

## Changes
- **Database:** `Employee` and `LeaveRequest` entities in `LeaveDbContext` (SQLite `leave.db`, created by
  `EnsureCreated()` at startup), with a unique index on `NormalizedEmail`, restrict-delete FKs, enums stored as text and
  `DateOnly` leave days. `DemoSeeder` seeds the demo team through the business services in one transaction,
  only when no employees exist.
- **Business rules:** `EmployeeService` (required name and email, email format, case-insensitive unique email, manager rules) and
  `LeaveRequestService` (new requests are always Pending; date order; defined leave type; existing requester;
  500-character limits; decisions only from Pending and only by a manager). Violations are reported through
  `RuleViolationException`, with plain-words messages per field.
- **API:** `GET /api/health`, `GET /api/employees`, `GET /api/employees/{id}`, `GET /api/leave-requests`,
  `GET /api/leave-requests/{id}`. JSON is camelCase with lower-case enum strings, and an unknown id returns a 404 ProblemDetails with a plain message.
- **UI:** None. It is out of scope for LT-001.
- **Tests:** 37 xUnit tests: service rules against SQLite in-memory, plus API, seeding and restart tests through
  WebApplicationFactory.
- **No new dependencies** beyond the packages allowed in project.md (EF Core Sqlite 8.0.11, Mvc.Testing 8.0.11, xunit and the test SDK).

## Acceptance criteria
- [x] AC1: App reports healthy and the database is created automatically (`HealthTests.Health_returns_200_and_database_is_created`)
- [x] AC2: Fresh start has 1 manager and 3 reports, each with a name and an email (`EmployeeApiTests.Fresh_start_has_one_manager_and_three_reports`)
- [x] AC3: Restarting never duplicates the demo team or requests (`SeedTests.Seeding_twice_does_not_duplicate`, `SeedTests.Restarting_the_app_does_not_duplicate`)
- [x] AC4: Duplicate email refused, ignoring case (`EmployeeServiceTests.Duplicate_email_is_refused_ignoring_case`)
- [x] AC5: Non-manager must report to exactly one manager (`EmployeeServiceTests.Non_manager_without_manager_is_refused`, `..._whose_manager_is_not_a_manager_is_refused`, `..._with_unknown_manager_is_refused`)
- [x] AC6: Requests exist in every state with requester, days, type and reason (`LeaveRequestApiTests.Fresh_start_has_requests_in_every_state`)
- [x] AC7: Decided requests show decider, time and comment; pending ones show none (`LeaveRequestApiTests.Decided_requests_show_decider_time_and_comment`)
- [x] AC8: New request is pending (`LeaveRequestServiceTests.New_request_is_pending_without_decision`)
- [x] AC9: Invalid dates, type or requester refused with a plain message (`LeaveRequestServiceTests.Last_day_before_first_day_is_refused`, `Undefined_leave_type_is_refused`, `Unknown_requester_is_refused`, and others)
- [x] AC10: Single-day request accepted (`LeaveRequestServiceTests.Single_day_request_is_accepted`)

**Known follow-up (from the AI review, accepted at G3):** `decidedAtUtc` is stored in UTC but is probably sent
without a `Z` marker, because SQLite reads it back as `DateTimeKind.Unspecified`. Suggested fix for LT-002: add a value converter on
`DecidedAtUtc` in `LeaveDbContext`, plus a test assertion. See `review.md`, Code quality, item 1.

## Testing
- Automated: `dotnet test app/LeaveTracker.sln` → `Passed! - Failed: 0, Passed: 37, Skipped: 0, Total: 37`
  (the engine re-ran it independently in the TEST stage; the output is in `test-output.log`).
- Manual (under a minute):
  1. `dotnet run --project app/src/LeaveTracker.Api`
  2. Open `/api/health` and check that the status is `healthy`.
  3. Open `/api/employees` and check that there are 4 people, with Maya Manager as the only manager.
  4. Open `/api/leave-requests` and check that there are pending, approved and rejected requests, with decider details on the decided ones.
  5. Stop the app, start it again, and check that the counts are unchanged.

## AI disclosure
| Stage | Performed by |
|---|---|
| INTAKE | engine |
| ANALYZE | ai:claude-code (claude-opus-5-5) |
| DESIGN | ai:claude-code (claude-opus-5-5) |
| PLAN | ai:claude-code (claude-opus-5-5) |
| IMPLEMENT | ai:claude-code (claude-opus-5-5) |
| TEST | ai:claude-code (claude-opus-5-5) |
| REVIEW | ai:claude-code (claude-opus-5-5) |
| MR | ai:claude-code (claude-opus-5-5) |

| Gate | Approved by | When |
|---|---|---|
| G1 (Specification) | Ashish Doke | 2026-11-25T16:03:15Z |
| G2 (Design) | Ashish Doke | 2026-11-25T16:08:09Z |
| G3 (Raise MR) | Ashish Doke | 2026-11-25T16:23:36Z (bound to HEAD 0a64a8f) |

Generated with AIDLC. All gates approved by humans. AI did not merge.
Full trace: `.aidlc/records/LT-001/`
