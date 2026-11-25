# LT-001 Keep track of employees and their leave: Design

## Context
- `app/` contains only `README.md`. No solution, projects or tests exist yet. This requirement creates them
  using the layout and stack fixed in `.aidlc/context/project.md` (.NET 8 minimal API, EF Core 8 SQLite, xUnit).
- No earlier sealed specs or designs exist. The entities and terms defined here (Employee, LeaveRequest,
  LeaveType, LeaveStatus) will be reused by LT-002 onward.
- Build environment: .NET SDK 9.0 is installed with the .NET 8 runtime, so projects target `net8.0` and
  build/test with the installed SDK.
- Root `.gitignore` already ignores `bin/`, `obj/`, `TestResults/` and `*.db*`, so `leave.db` is never committed.

## Approach
Create the solution with one API project and one test project.

1. **Data model (EF Core, `Data/`)**: `Employee` and `LeaveRequest` entities in `LeaveDbContext`. Enums
   `LeaveType` (Annual, Sick, Unpaid) and `LeaveStatus` (Pending, Approved, Rejected) are stored as strings so
   the database stays readable. Emails are stored trimmed as given, with an extra `NormalizedEmail` column
   (trimmed and lower-cased) that has a **unique index**. That makes uniqueness ignore case (spec Q1) and enforces it in the
   database as well as in the service.
2. **Business rules (`Services/`)**: `EmployeeService` and `LeaveRequestService` are the only way to add
   employees and leave requests. They validate the input and throw `RuleViolationException`, which holds a
   field-to-message dictionary of plain-words messages, when a rule is broken (AC4, AC5, AC9, AC10).
   `LeaveRequestService.AddAsync` always creates the request as **Pending** with no decision fields (AC8).
   It also has an internal `RecordDecisionAsync(requestId, deciderId, approve, comment, decidedAtUtc)`
   used only by the seeder in LT-001. That method is the building block LT-002 will expose. It allows
   Pending → Approved/Rejected only, the decider must be a manager, and the comment can be at most 500 characters.
   No HTTP endpoint creates or changes data in LT-001, because asking for and deciding on leave are out of scope.
3. **Seed (`Data/DemoSeeder.cs`)**: at startup, after `EnsureCreated()`, `DemoSeeder.SeedAsync` runs.
   If **any** employee exists, it does nothing (spec Q9). Otherwise it creates the demo team **through the
   services**, so seed data obeys the same rules, inside one transaction. The team is 1 manager, 3 reports
   and 4 requests (see Data).
4. **Read-only API (`Endpoints/`)**: `GET` endpoints for health, employees and leave requests (spec Q5), returning
   DTO records from `Contracts/`. JSON uses camelCase, and enums are serialized as camelCase strings
   (`"annual"`, `"pending"`) through `JsonStringEnumConverter`, which is part of the BCL, not a new package.
5. **Composition (`Program.cs`)**: registers the DbContext with the connection string `ConnectionStrings:Leave`
   (default `Data Source=leave.db`), the services, and the endpoint groups. On startup it calls `EnsureCreated()`
   and then the seeder. It ends with `public partial class Program;` so `WebApplicationFactory` can reach it.

**Alternatives considered**
- *SQLite `COLLATE NOCASE` on the Email column instead of a `NormalizedEmail` column*: rejected because
  NOCASE only folds ASCII and is easy to lose when the schema is regenerated. An explicit normalized column with a
  unique index is clear and testable.
- *POST endpoints for employees and requests in LT-001, so AC4/AC5/AC9 can be tested over HTTP*: rejected
  because creating requests is LT-002's scope and managing employees is out of scope. The rules are tested
  at the service level against a real SQLite database instead.
- *Hard-coding seed rows with `HasData` (EF model seeding)*: rejected because it skips the business rules, and
  `HasData` needs fixed IDs and dates that would go stale. Runtime seeding through the services is simpler and
  idempotent by the "no employees" check.

## Affected files
| Path | Change |
|---|---|
| app/LeaveTracker.sln | create |
| app/src/LeaveTracker.Api/LeaveTracker.Api.csproj | create |
| app/src/LeaveTracker.Api/Program.cs | create |
| app/src/LeaveTracker.Api/appsettings.json | create |
| app/src/LeaveTracker.Api/Data/LeaveDbContext.cs | create |
| app/src/LeaveTracker.Api/Data/Employee.cs | create |
| app/src/LeaveTracker.Api/Data/LeaveRequest.cs | create |
| app/src/LeaveTracker.Api/Data/LeaveType.cs | create |
| app/src/LeaveTracker.Api/Data/LeaveStatus.cs | create |
| app/src/LeaveTracker.Api/Data/DemoSeeder.cs | create |
| app/src/LeaveTracker.Api/Services/RuleViolationException.cs | create |
| app/src/LeaveTracker.Api/Services/EmployeeService.cs | create |
| app/src/LeaveTracker.Api/Services/LeaveRequestService.cs | create |
| app/src/LeaveTracker.Api/Contracts/EmployeeResponse.cs | create |
| app/src/LeaveTracker.Api/Contracts/LeaveRequestResponse.cs | create |
| app/src/LeaveTracker.Api/Endpoints/HealthEndpoints.cs | create |
| app/src/LeaveTracker.Api/Endpoints/EmployeeEndpoints.cs | create |
| app/src/LeaveTracker.Api/Endpoints/LeaveRequestEndpoints.cs | create |
| app/tests/LeaveTracker.Tests/LeaveTracker.Tests.csproj | create |
| app/tests/LeaveTracker.Tests/TestAppFactory.cs | create |
| app/tests/LeaveTracker.Tests/TestDb.cs | create |
| app/tests/LeaveTracker.Tests/HealthTests.cs | create |
| app/tests/LeaveTracker.Tests/SeedTests.cs | create |
| app/tests/LeaveTracker.Tests/EmployeeServiceTests.cs | create |
| app/tests/LeaveTracker.Tests/LeaveRequestServiceTests.cs | create |
| app/tests/LeaveTracker.Tests/EmployeeApiTests.cs | create |
| app/tests/LeaveTracker.Tests/LeaveRequestApiTests.cs | create |

## Data
Schema is created by `Database.EnsureCreated()` at startup. There are no migrations, per project convention.

**Employee**
| Column | Type | Rules |
|---|---|---|
| Id | int, PK, identity | |
| Name | text, required | trimmed, 1–100 chars |
| Email | text, required | trimmed, ≤ 254 chars, exactly one `@` with text on both sides (spec Q8) |
| NormalizedEmail | text, required | `Email.Trim().ToLowerInvariant()`, **unique index** (AC4, Q1) |
| IsManager | bool | |
| ManagerId | int?, FK → Employee.Id (restrict delete) | required when `IsManager = false` and must point to an employee with `IsManager = true` (AC5). Optional for managers (spec Q2) |

**LeaveRequest**
| Column | Type | Rules |
|---|---|---|
| Id | int, PK, identity | |
| EmployeeId | int, FK → Employee.Id (restrict delete) | must exist (AC9) |
| FirstDay | DateOnly (TEXT `yyyy-MM-dd`) | required |
| LastDay | DateOnly (TEXT `yyyy-MM-dd`) | required, `LastDay >= FirstDay`. Equal is allowed (AC9, AC10). Both days are inclusive (Q3) |
| Type | text (`Annual`/`Sick`/`Unpaid`) | must be a defined enum value (AC9) |
| Reason | text? | optional, ≤ 500 chars (Q7) |
| Status | text (`Pending`/`Approved`/`Rejected`) | `Pending` on creation (AC8) |
| DecidedById | int?, FK → Employee.Id (restrict delete) | null while Pending. When decided, it is set and must be a manager (AC7) |
| DecidedAtUtc | DateTime? (UTC) | null while Pending, set when decided (Q4) |
| DecisionComment | text? | null while Pending, optional when decided, ≤ 500 chars (Q7) |

**Seed data** (fictional, `example.com`, spec Q10). Created only when the Employee table is empty. Dates are
relative to the seed day *T* (today, UTC), so the demo stays current:
| Employee | Email | Manager |
|---|---|---|
| Maya Manager | maya.manager@example.com | — (IsManager) |
| Alex Turner | alex.turner@example.com | Maya |
| Priya Shah | priya.shah@example.com | Maya |
| Sam Lee | sam.lee@example.com | Maya |

| Requester | Type | First → Last | Status | Decision |
|---|---|---|---|---|
| Alex | Annual | T+14 → T+18 | Pending | — |
| Priya | Sick | T−7 → T−6 | Approved | by Maya, comment "Get well soon." |
| Sam | Unpaid | T+30 → T+30 | Rejected | by Maya, comment "Busy release week, please pick another day." |
| Sam | Annual | T+45 → T+49 | Approved | by Maya, no comment |

## API
All routes are read-only. JSON is camelCase and enums are lower-case strings.

| Method | Route | Request | Response | Codes |
|---|---|---|---|---|
| GET | /api/health | — | `{ "status": "healthy" }` | 200 |
| GET | /api/employees | — | `EmployeeResponse[]` ordered by name: `{ id, name, email, isManager, managerId, managerName }` | 200 |
| GET | /api/employees/{id} | — | `EmployeeResponse` | 200, 404 (plain-words `ProblemDetails`: "No employee with id 42.") |
| GET | /api/leave-requests | — | `LeaveRequestResponse[]` ordered by first day, then id: `{ id, employeeId, employeeName, firstDay, lastDay, type, reason, status, decidedById, decidedByName, decidedAtUtc, decisionComment }` | 200 |
| GET | /api/leave-requests/{id} | — | `LeaveRequestResponse` | 200, 404 (plain-words `ProblemDetails`) |

There are no write endpoints in LT-001. `RuleViolationException` is mapped to `ValidationProblem` (400) when LT-002
adds the first write endpoint. That mapping is not part of LT-001.

## Risks
| Risk | Mitigation |
|---|---|
| Duplicate demo data when the app starts twice at the same moment (two processes) | Seeding runs in a single transaction. The unique index on `NormalizedEmail` makes a second concurrent insert fail instead of duplicating data. The demo runs as one process. |
| Rules bypassed by writing to `LeaveDbContext` directly | All creation goes through the services, including the seeder. The DB-level unique index and FKs back up the most important rules. The read endpoints never write. |
| Case-insensitive email rule surprises users with non-ASCII emails | `ToLowerInvariant()` handles Unicode. The rule and its assumption are documented as spec Q1. |
| SQLite in-memory test database closes too early | Tests hold one open `SqliteConnection` per test or factory and dispose it at the end (`TestDb`, `TestAppFactory`). |
| Tests depend on the clock (relative seed dates) | Tests assert on states, counts and the `LastDay >= FirstDay` relation, not fixed dates. |
| No authentication; anyone can read all employees and requests | This is a known demo limitation stated in project.md. Only fictional data is used. The identity header is not needed in LT-001. |
| `leave.db` committed by accident | `*.db*` is already in the root `.gitignore`. Tests never create a file database. |
| Build SDK is 9.0 while the target is net8.0 | net8.0 targeting with the installed 8.0 runtime is supported. No `global.json` is added. |
| New dependency | None. Only the packages allowed in project.md are used: `Microsoft.EntityFrameworkCore.Sqlite` 8.0.x, `Microsoft.AspNetCore.Mvc.Testing` 8.0.x, `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`, `coverlet.collector`. |

## Test strategy
All tests use SQLite in-memory (a shared open connection), so no running server or file database is needed.
`TestAppFactory` (WebApplicationFactory) swaps the DbContext to the in-memory connection.

| AC | Test type | Test |
|---|---|---|
| AC1 | Integration (API) | `HealthTests.Health_returns_200_and_database_is_created`: GET /api/health → 200. The DbContext can query Employees on a fresh database without manual setup. |
| AC2 | Integration (API) | `EmployeeApiTests.Fresh_start_has_one_manager_and_three_reports`: GET /api/employees → 4 entries. Exactly 1 has `isManager`. The other 3 have `managerId` set to that manager. All have a name and an email. `Get_unknown_employee_returns_404`. |
| AC3 | Integration | `SeedTests.Seeding_twice_does_not_duplicate`: run the seeder twice on the same DB, and start a second `TestAppFactory` host on the same connection. Employee and request counts are unchanged (4 and 4). |
| AC4 | Unit (service + SQLite) | `EmployeeServiceTests.Duplicate_email_is_refused_ignoring_case`: adding `ALEX.TURNER@example.com ` is refused with a message containing "already in use", and the count is unchanged. |
| AC5 | Unit (service + SQLite) | `EmployeeServiceTests.Non_manager_without_manager_is_refused` and `Non_manager_whose_manager_is_not_a_manager_is_refused`: both are refused with "must report to exactly one manager". `Manager_without_manager_is_accepted`. |
| AC6 | Integration (API) | `LeaveRequestApiTests.Fresh_start_has_requests_in_every_state`: GET /api/leave-requests has at least one pending, one approved and one rejected request, each with `employeeId`, `firstDay`, `lastDay` and a `type` in {annual, sick, unpaid}. |
| AC7 | Integration (API) | `LeaveRequestApiTests.Decided_requests_show_decider_time_and_comment`: decided requests have `decidedById` set to a manager and `decidedAtUtc` set. Pending requests have all three decision fields null. `Get_unknown_request_returns_404`. |
| AC8 | Unit (service + SQLite) | `LeaveRequestServiceTests.New_request_is_pending_without_decision`. |
| AC9 | Unit (service + SQLite) | `LeaveRequestServiceTests.Last_day_before_first_day_is_refused`, `Undefined_leave_type_is_refused`, `Unknown_requester_is_refused`: each one is refused with a message naming the problem. Also `Reason_over_500_chars_is_refused` (Q7). |
| AC10 | Unit (service + SQLite) | `LeaveRequestServiceTests.Single_day_request_is_accepted`. |
| (supporting) | Unit | `LeaveRequestServiceTests.Decision_only_from_pending_and_by_a_manager`: this covers the rule the seeder relies on for AC7. |

Manual check: `dotnet run --project app/src/LeaveTracker.Api`, open `/api/health`, `/api/employees` and `/api/leave-requests`, restart, and confirm the counts are unchanged.
