# LT-001 Keep track of employees and their leave: Implementation notes

## Summary
Created the .NET 8 solution (API + test project) with the Employee/LeaveRequest data model, the business rules
in `EmployeeService` and `LeaveRequestService`, idempotent demo seeding through those services, and read-only
endpoints for health, employees and leave requests. All 37 tests pass with `dotnet test app/LeaveTracker.sln`.
Only files listed in the plan's `allowed_files` were touched. No packages beyond the approved list were added.

## Tasks
### T1: Create solution, projects, data model, health endpoint and test infrastructure
- Commit: 94217f1
- Changes: `app/LeaveTracker.sln`, API csproj, `Program.cs` (`EnsureCreated` at startup, `public partial class Program`),
  `appsettings.json` (`ConnectionStrings:Leave = Data Source=leave.db`), `Data/` entities, enums and `LeaveDbContext`
  (unique index on `NormalizedEmail`, restrict-delete FKs, enums stored as strings), `Endpoints/HealthEndpoints.cs`,
  and the test csproj, `TestDb`, `TestAppFactory` and `HealthTests` (AC1).
- Notes: Only the .NET 9 SDK is installed. It builds `net8.0` fine and tests run on the installed 8.0 runtime.
  Package versions: EF Core Sqlite 8.0.11, Mvc.Testing 8.0.11, Test SDK 17.12.0, xunit 2.9.2,
  xunit.runner.visualstudio 2.8.2, coverlet.collector 6.0.2. The solution was generated with `dotnet new sln`.

### T2: Add EmployeeService with email and manager rules
- Commit: 4185899
- Changes: `Services/RuleViolationException.cs` (field-to-messages dictionary, ready for `ValidationProblem` in
  LT-002, plus an internal `RuleErrors` collector), `Services/EmployeeService.cs` (`NewEmployee` input record),
  and `EmployeeServiceTests` (AC4, AC5, Q2, Q8).
- Notes: A SQLite unique-constraint error (code 19) from a concurrent duplicate is turned into the same
  "already in use" rule violation. If a manager is given a `ManagerId`, that id must also be a manager (Q2).

### T3: Add LeaveRequestService with pending creation, validation and decision recording
- Commit: 82b845f
- Changes: `Services/LeaveRequestService.cs` (`NewLeaveRequest` record, `AddAsync`, internal `RecordDecisionAsync`),
  an `InternalsVisibleTo LeaveTracker.Tests` entry in the API csproj, and `LeaveRequestServiceTests` (AC8, AC9, AC10, Q7).
- Notes: The design marked `RecordDecisionAsync` as internal, so the API csproj gained `InternalsVisibleTo` to let tests
  reach it. A blank reason or comment is stored as null. Every decision failure currently throws
  `RuleViolationException`. When LT-002 exposes decisions, it should map "not found" to 404 and "not pending" to 409.

### T4: Add idempotent demo team seeding
- Commit: 6d30d18
- Changes: `Data/DemoSeeder.cs` (skipped when any employee exists; one transaction; created through the services),
  seeding wired into `Program.cs` after `EnsureCreated`, services registered in DI, and `SeedTests` (AC3, Q9, Q10).
- Notes: `SeedAsync` accepts an optional `nowUtc` so tests can use a fixed clock. Seed dates are relative to today (UTC).

### T5: Add read-only employee and leave request endpoints
- Commit: 0a64a8f
- Changes: `Contracts/EmployeeResponse.cs`, `Contracts/LeaveRequestResponse.cs`, `Endpoints/EmployeeEndpoints.cs`,
  `Endpoints/LeaveRequestEndpoints.cs`, `Program.cs` (camelCase `JsonStringEnumConverter`, `AddProblemDetails`,
  endpoint mapping), `EmployeeApiTests` and `LeaveRequestApiTests` (AC2, AC6, AC7, plus 404s).
- Notes: The first attempt called `OrderBy` after projecting into the response record, and EF Core could not
  translate that. Ordering now happens before the projection.
