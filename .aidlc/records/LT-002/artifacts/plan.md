# LT-002 Ask for leave and decide on it: Plan

## Tasks
- **T1**: Days covered and the UTC marker. Add the `DecidedAtUtc` UTC converter in `LeaveDbContext`, the static
  `LeaveRequestService.DaysCovered`, `daysCovered` on `LeaveRequestResponse` with a shared `From(entity)` mapper, and
  read endpoints that use the mapper. Update `LeaveRequestApiTests` (`daysCovered`, `Z`) and add the `DaysCovered` unit theory.
  (covers AC13, AC14)
- **T2**: Decision rules in the service. Add `RuleKind`/`Kind` on `RuleViolationException`. Make `RecordDecisionAsync` public
  with the not-found, manager, team, pending and rejection-reason rules, in the designed order, with the time from an injected
  `TimeProvider`. Add a requester-must-be-self check for creation. Register `TimeProvider.System` in `Program.cs` and adapt
  `DemoSeeder`. Update `LeaveRequestServiceTests` (and `SeedTests` only if needed). (covers AC5, AC10, AC11, AC12)
- **T3**: Identity and asking for leave over HTTP. Add `Endpoints/CurrentEmployee.cs`, `Endpoints/RuleResults.cs`,
  `Contracts/CreateLeaveRequest.cs`, `POST /api/leave-requests` (string dates and type parsed with plain messages), and
  `TestAppFactory.CreateClientAs`. Add `IdentityApiTests` and `AskForLeaveApiTests`. (covers AC1, AC2, AC3, AC4, AC5)
- **T4**: Approve and reject over HTTP. Add `Contracts/DecisionRequests.cs`, `POST /api/leave-requests/{id}/approve` and
  `/reject`, and `DecisionApiTests`. (covers AC8, AC9, AC10, AC11, AC12, AC13)
- **T5**: Own and team lists. Add `GetForEmployeeAsync` / `GetPendingForTeamAsync` in the service,
  `Endpoints/MyRequestsEndpoints.cs` (`/api/me/leave-requests`, `/api/team/leave-requests/pending`), mapping in
  `Program.cs`, and `MyAndTeamRequestsApiTests`. (covers AC6, AC7, AC8)

`allowed_files` lists exactly the design's "Affected files". No other repository file is touched.

## Plan data
```aidlc-plan
{
  "tasks": [
    {"id": "T1", "title": "Days covered on every request and UTC decision times", "covers": ["AC13", "AC14"]},
    {"id": "T2", "title": "Decision rules, failure kinds and self-only requests in the service", "covers": ["AC5", "AC10", "AC11", "AC12"]},
    {"id": "T3", "title": "Picked-person identity and asking for leave over HTTP", "covers": ["AC1", "AC2", "AC3", "AC4", "AC5"]},
    {"id": "T4", "title": "Approve and reject endpoints", "covers": ["AC8", "AC9", "AC10", "AC11", "AC12", "AC13"]},
    {"id": "T5", "title": "Own requests and team pending requests", "covers": ["AC6", "AC7", "AC8"]}
  ],
  "allowed_files": [
    "app/src/LeaveTracker.Api/Program.cs",
    "app/src/LeaveTracker.Api/Data/LeaveDbContext.cs",
    "app/src/LeaveTracker.Api/Data/DemoSeeder.cs",
    "app/src/LeaveTracker.Api/Services/RuleViolationException.cs",
    "app/src/LeaveTracker.Api/Services/LeaveRequestService.cs",
    "app/src/LeaveTracker.Api/Contracts/LeaveRequestResponse.cs",
    "app/src/LeaveTracker.Api/Contracts/CreateLeaveRequest.cs",
    "app/src/LeaveTracker.Api/Contracts/DecisionRequests.cs",
    "app/src/LeaveTracker.Api/Endpoints/CurrentEmployee.cs",
    "app/src/LeaveTracker.Api/Endpoints/RuleResults.cs",
    "app/src/LeaveTracker.Api/Endpoints/LeaveRequestEndpoints.cs",
    "app/src/LeaveTracker.Api/Endpoints/MyRequestsEndpoints.cs",
    "app/tests/LeaveTracker.Tests/TestAppFactory.cs",
    "app/tests/LeaveTracker.Tests/LeaveRequestServiceTests.cs",
    "app/tests/LeaveTracker.Tests/LeaveRequestApiTests.cs",
    "app/tests/LeaveTracker.Tests/SeedTests.cs",
    "app/tests/LeaveTracker.Tests/IdentityApiTests.cs",
    "app/tests/LeaveTracker.Tests/AskForLeaveApiTests.cs",
    "app/tests/LeaveTracker.Tests/MyAndTeamRequestsApiTests.cs",
    "app/tests/LeaveTracker.Tests/DecisionApiTests.cs"
  ],
  "test_command": "dotnet test app/LeaveTracker.sln"
}
```
