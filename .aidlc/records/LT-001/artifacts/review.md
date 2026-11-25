# LT-001 Keep track of employees and their leave: AI review

## Summary
- Created the .NET 8 solution (`app/LeaveTracker.sln`), the minimal API project and the xUnit test project, following the layout in project.md.
- Added the data model: `Employee` (unique case-insensitive email through `NormalizedEmail`, optional manager link) and `LeaveRequest` (requester, inclusive first/last day, type, reason, status, decision fields). The schema is created with `EnsureCreated()` at startup.
- Business rules are in `EmployeeService` and `LeaveRequestService`, which throw `RuleViolationException` with plain-words, per-field messages. New requests are always Pending. Decisions go Pending → Approved/Rejected only, and only a manager can decide.
- `DemoSeeder` creates 1 manager, 3 reports and 4 requests (pending, approved ×2, rejected) through the services in one transaction, only when no employees exist.
- The API is read-only: `GET /api/health`, `/api/employees[/{id}]`, `/api/leave-requests[/{id}]`, with camelCase JSON, lower-case enum strings and plain-words 404 ProblemDetails.

## Acceptance criteria verification
| AC | Met? | Evidence |
|---|---|---|
| AC1 | Yes | `Program.cs` (`EnsureCreatedAsync`), `Endpoints/HealthEndpoints.cs`. Test: `HealthTests.Health_returns_200_and_database_is_created` |
| AC2 | Yes | `Data/DemoSeeder.cs`, `Endpoints/EmployeeEndpoints.cs`. Tests: `EmployeeApiTests.Fresh_start_has_one_manager_and_three_reports`, `SeedTests.Seed_creates_the_designed_demo_team` |
| AC3 | Yes | `DemoSeeder.SeedAsync` returns early when any employee exists. Tests: `SeedTests.Seeding_twice_does_not_duplicate`, `SeedTests.Restarting_the_app_does_not_duplicate` (3 host starts on one database) |
| AC4 | Yes | `EmployeeService.AddAsync` (normalized lookup, plus catching a unique-index violation), unique index in `LeaveDbContext`. Test: `EmployeeServiceTests.Duplicate_email_is_refused_ignoring_case` |
| AC5 | Yes | `EmployeeService.CheckManagerAsync`. Tests: `EmployeeServiceTests.Non_manager_without_manager_is_refused`, `Non_manager_whose_manager_is_not_a_manager_is_refused`, `Non_manager_with_unknown_manager_is_refused` |
| AC6 | Yes | `Endpoints/LeaveRequestEndpoints.cs`. Test: `LeaveRequestApiTests.Fresh_start_has_requests_in_every_state` |
| AC7 | Yes, with one minor finding (see Code quality, item 1) | `LeaveRequestService.RecordDecisionAsync`, `LeaveRequestResponse`. Tests: `LeaveRequestApiTests.Decided_requests_show_decider_time_and_comment`, `LeaveRequestServiceTests.Decision_only_from_pending_and_by_a_manager` |
| AC8 | Yes | `LeaveRequestService.AddAsync` sets `Pending`. Test: `LeaveRequestServiceTests.New_request_is_pending_without_decision` |
| AC9 | Yes | `LeaveRequestService.AddAsync` validation. Tests: `Last_day_before_first_day_is_refused`, `Undefined_leave_type_is_refused`, `Missing_leave_type_is_refused`, `Unknown_requester_is_refused`, `Missing_days_are_refused` |
| AC10 | Yes | `last < first` is the only date-order rejection. Test: `LeaveRequestServiceTests.Single_day_request_is_accepted` |

## Code quality
1. **Minor defect (likely, not verified at runtime): `decidedAtUtc` may be emitted without a UTC marker.** The value is
   stored in UTC (Q4), but EF Core SQLite reads `DateTime` back with `Kind = Unspecified`, so the JSON is probably
   `"2026-11-24T12:00:00"` without the `Z`. A client could then treat it as local time. The AC7 test only checks
   that the value is a string, so it would not catch this. **Suggested fix** (about 5 lines): in `LeaveDbContext`, add a value
   converter on `DecidedAtUtc` that applies `DateTime.SpecifyKind(v, DateTimeKind.Utc)` on read, and assert in
   `LeaveRequestApiTests` that the value ends with `Z`. I could not apply it in REVIEW: the guard only allows
   `review.md` in this stage. I could not confirm it by running the app either, because `curl` to localhost is blocked
   by the guardrail. It does not change AC7 (who, when and the comment are all kept), but it weakens the Q4 assumption.
2. Conventions from project.md are followed: nullable enabled, file-scoped namespaces, records for DTOs and inputs,
   async throughout, no static mutable state, `DateOnly` for leave days (stored as `yyyy-MM-dd` TEXT), one static
   `MapXxxEndpoints` class per resource, and business rules only in `Services/`.
3. Error handling: messages are plain words and name the field and the problem (for example "The last day (2026-11-30)
   is before the first day (2026-12-01)."). Not-found and not-pending decision failures both throw
   `RuleViolationException` today. LT-002 must map them to 404 and 409 when it exposes decisions (noted in implementation-notes).
4. `RecordDecisionAsync` is `internal`, with `InternalsVisibleTo` for the test project. This matches the design but is
   one small addition to the csproj that the design did not spell out.
5. `/api/employees` orders by name with SQLite's default binary collation, so it is case-sensitive ("alex" sorts after
   "Zoe"). This is harmless with the seed data. It is worth revisiting if a sort order matters in the UI requirements.
6. The seeder depends on `DateTime.UtcNow` (injectable through `nowUtc`). Tests assert relations and states, not fixed dates,
   so they are not time-dependent.
7. Some duplication: each test class creates its own `TestAppFactory` (a host per test). That is acceptable at this
   size (the suite runs in about 2 s).

## Security
- **Input validation:** every write path (services) validates length, format and references before saving. The DB
  enforces uniqueness and foreign keys as a backstop. No write endpoints are exposed in LT-001.
- **Injection:** all data access is through EF Core LINQ (parameterized). There is no raw SQL and no string-built queries.
- **Authorization:** there is no authentication (a known demo limitation from project.md). The read endpoints expose
  all employee names, emails and leave reasons to any caller. This is acceptable for fictional demo data, but it
  would be a personal-data exposure in a real deployment (sick-leave reasons are sensitive).
- **Secrets:** none. The connection string is a local file path in `appsettings.json`, with no credentials.
- **Logging of personal data:** EF Core logging is set to `Warning`, and `EnableSensitiveDataLogging` is not used.
  No custom logging of names or emails.
- **Personal data:** the seed uses fictional names with `example.com` addresses only (Q10).

## Guardrail report
- Files changed: 27 · Lines changed: 1442 · Violations: 0
- Tests: 37 passed, 0 failed, 0 skipped (`dotnet test app/LeaveTracker.sln`, re-run by the engine in TEST).

## Recommendation
**APPROVE**, with one minor follow-up. All 10 acceptance criteria are met with automated evidence, the guardrail
scan is clean, and the code follows project conventions without new dependencies. The `decidedAtUtc` time-zone marker
(Code quality, item 1) is a small, likely defect that doesn't break any AC. If you want it fixed before merge,
reject G3 with that item as feedback and the fix plus a test assertion will be made in a rework pass. Otherwise it can be
fixed early in LT-002, which is the first requirement where clients read decision times.
