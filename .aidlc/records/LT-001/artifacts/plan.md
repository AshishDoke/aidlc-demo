# LT-001 Keep track of employees and their leave: Plan

## Tasks
- **T1**: Create the solution, API project and test project. Add the data model (Employee, LeaveRequest,
  LeaveType, LeaveStatus, LeaveDbContext with the unique `NormalizedEmail` index and FKs), `appsettings.json`,
  `Program.cs` with `EnsureCreated()`, and `GET /api/health`. Add the test infrastructure (`TestDb`,
  `TestAppFactory`) and `HealthTests`. (covers AC1)
- **T2**: Add `RuleViolationException` and `EmployeeService` (name/email validation, case-insensitive unique email,
  a non-manager must report to exactly one manager), with `EmployeeServiceTests`. (covers AC4, AC5)
- **T3**: Add `LeaveRequestService`: create a request as Pending (date order, defined leave type, existing requester,
  reason ≤ 500), plus `RecordDecisionAsync` (Pending only, decider must be a manager, comment ≤ 500, UTC time).
  Add `LeaveRequestServiceTests`. (covers AC8, AC9, AC10)
- **T4**: Add `DemoSeeder` (1 manager, 3 reports, 4 requests created through the services in one transaction,
  skipped when any employee exists), wire it into startup, and add `SeedTests`. (covers AC3)
- **T5**: Add the read-only contracts and endpoints `GET /api/employees[/{id}]` and `GET /api/leave-requests[/{id}]`
  (camelCase, lower-case enum strings, 404 ProblemDetails), with `EmployeeApiTests` and `LeaveRequestApiTests`.
  (covers AC2, AC6, AC7)

`allowed_files` lists exactly the files in design.md "Affected files". No other repository file is touched.

## Plan data
```aidlc-plan
{
  "tasks": [
    {"id": "T1", "title": "Create solution, projects, data model, health endpoint and test infrastructure", "covers": ["AC1"]},
    {"id": "T2", "title": "Add EmployeeService with email and manager rules", "covers": ["AC4", "AC5"]},
    {"id": "T3", "title": "Add LeaveRequestService with pending creation, validation and decision recording", "covers": ["AC8", "AC9", "AC10"]},
    {"id": "T4", "title": "Add idempotent demo team seeding", "covers": ["AC3"]},
    {"id": "T5", "title": "Add read-only employee and leave request endpoints", "covers": ["AC2", "AC6", "AC7"]}
  ],
  "allowed_files": [
    "app/LeaveTracker.sln",
    "app/src/LeaveTracker.Api/LeaveTracker.Api.csproj",
    "app/src/LeaveTracker.Api/Program.cs",
    "app/src/LeaveTracker.Api/appsettings.json",
    "app/src/LeaveTracker.Api/Data/LeaveDbContext.cs",
    "app/src/LeaveTracker.Api/Data/Employee.cs",
    "app/src/LeaveTracker.Api/Data/LeaveRequest.cs",
    "app/src/LeaveTracker.Api/Data/LeaveType.cs",
    "app/src/LeaveTracker.Api/Data/LeaveStatus.cs",
    "app/src/LeaveTracker.Api/Data/DemoSeeder.cs",
    "app/src/LeaveTracker.Api/Services/RuleViolationException.cs",
    "app/src/LeaveTracker.Api/Services/EmployeeService.cs",
    "app/src/LeaveTracker.Api/Services/LeaveRequestService.cs",
    "app/src/LeaveTracker.Api/Contracts/EmployeeResponse.cs",
    "app/src/LeaveTracker.Api/Contracts/LeaveRequestResponse.cs",
    "app/src/LeaveTracker.Api/Endpoints/HealthEndpoints.cs",
    "app/src/LeaveTracker.Api/Endpoints/EmployeeEndpoints.cs",
    "app/src/LeaveTracker.Api/Endpoints/LeaveRequestEndpoints.cs",
    "app/tests/LeaveTracker.Tests/LeaveTracker.Tests.csproj",
    "app/tests/LeaveTracker.Tests/TestAppFactory.cs",
    "app/tests/LeaveTracker.Tests/TestDb.cs",
    "app/tests/LeaveTracker.Tests/HealthTests.cs",
    "app/tests/LeaveTracker.Tests/SeedTests.cs",
    "app/tests/LeaveTracker.Tests/EmployeeServiceTests.cs",
    "app/tests/LeaveTracker.Tests/LeaveRequestServiceTests.cs",
    "app/tests/LeaveTracker.Tests/EmployeeApiTests.cs",
    "app/tests/LeaveTracker.Tests/LeaveRequestApiTests.cs"
  ],
  "test_command": "dotnet test app/LeaveTracker.sln"
}
```
