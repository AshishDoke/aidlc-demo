# LT-002 Ask for leave and decide on it: Implementation notes

## Summary
Added the leave process on top of LT-001. Employees ask for leave for themselves, see their own requests newest first,
and managers see and decide on their own team's pending requests. Identity is the picked person from the `X-Employee-Id`
header. Rule failures carry a kind (validation / not found / forbidden / conflict), which the API maps to 400/404/403/409 with plain messages.
Every request shows `daysCovered`, and decision times are now marked as UTC (LT-001 review item 1 fixed).
97 tests pass (37 from LT-001, updated where the design said so, plus 60 new). There is no schema change and no new package.

## Tasks
### T1: Days covered on every request and UTC decision times
- Commit: 437d9c2
- Changes: `LeaveDbContext` (read-side UTC converter on `DecidedAtUtc`), `LeaveRequestService.DaysCovered`,
  `LeaveRequestResponse` (+`DaysCovered`, `From(entity)` mapper), and `LeaveRequestEndpoints` reads using `Include` plus the mapper.
  Tests: `Z` and `daysCovered` assertions in `LeaveRequestApiTests`, and the `Days_covered_is_inclusive_calendar_days` theory.
- Notes: Reads now load entities and map them in memory, because `DateOnly.DayNumber` does not translate to SQL.

### T2: Decision rules, failure kinds and self-only requests in the service
- Commit: bfe5ab2
- Changes: `RuleKind` and `Kind` on `RuleViolationException` (default Validation), and `LeaveRequestService`: public
  `RecordDecisionAsync` with the designed rule order, a `TimeProvider` clock and `AskForSelfAsync`. `Program.cs` registers
  `TimeProvider.System`. Tests added to `LeaveRequestServiceTests`.
- Notes: `DemoSeeder` and `SeedTests` did **not** need changes. The seeder's positional call still compiles against the new
  optional `decidedAtUtc` parameter, and Maya decides only on her own reports, so the team rule holds. The "already decided" message
  keeps the LT-001 phrase "only pending requests can be decided", so the LT-001 test is unchanged. The service still
  has an `InternalsVisibleTo` entry for tests, which is now unused by `RecordDecisionAsync` (public) but harmless.

### T3: Picked-person identity and asking for leave over HTTP
- Commit: b7cc15e
- Changes: `Endpoints/CurrentEmployee.cs` (400 "Pick who you are …" for a missing, non-numeric or unknown id),
  `Endpoints/RuleResults.cs`, `Contracts/CreateLeaveRequest.cs`, `POST /api/leave-requests` (string dates and type
  parsed with plain messages, 201 plus Location), and `TestAppFactory.CreateClientAs` / `EmployeeIdAsync`.
  Tests: `IdentityApiTests`, `AskForLeaveApiTests`.
- Notes: Numeric type strings such as `"1"` are rejected on purpose (`Enum.TryParse` would otherwise accept them). The type is
  case-insensitive. Date-format errors are returned on their own, before the service rules run.

### T4: Approve and reject endpoints
- Commit: b145177
- Changes: `Contracts/DecisionRequests.cs`, `POST /api/leave-requests/{id}/approve` and `/reject` (body optional, nullable),
  `DecisionApiTests`, and the approve/reject cases in the `IdentityApiTests` theory.
- Notes: A rejection reason is stored in the existing `DecisionComment` column, as the spec glossary says.

### T5: Own requests and team pending requests
- Commit: c7e1910
- Changes: `LeaveRequestService.GetForEmployeeAsync` (descending id) and `GetPendingForTeamAsync` (direct reports,
  Pending, first day ascending, Forbidden for non-managers), `Endpoints/MyRequestsEndpoints.cs`, mapping in `Program.cs`,
  `MyAndTeamRequestsApiTests`, and the mine/team cases in the `IdentityApiTests` theory.
- Notes: The `IdentityApiTests` AC3 theory was extended in T4 and T5 as the endpoints came into existence, so each commit stays green.
