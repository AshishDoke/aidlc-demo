# LT-002 Ask for leave and decide on it: Design

## Context
LT-001 (merged) provides:
- `Employee` and `LeaveRequest` entities in `LeaveDbContext` (SQLite, `EnsureCreated()`, idempotent `DemoSeeder`).
- `EmployeeService` and `LeaveRequestService`. `LeaveRequestService.AddAsync` creates Pending requests and validates dates,
  type, requester and reason ≤ 500. `RecordDecisionAsync` is **internal**: it allows Pending only, the decider must be a manager,
  the comment is ≤ 500, and it is used only by the seeder.
- `RuleViolationException` (field → messages). It has no notion of *kind*, so every failure looks like a validation error.
- Read-only endpoints: `GET /api/health`, `GET /api/employees[/{id}]`, `GET /api/leave-requests[/{id}]`.
  JSON is camelCase with camelCase enum strings and 404 ProblemDetails.
- **Known defect (LT-001 review, item 1):** `DecidedAtUtc` is read back from SQLite as `DateTimeKind.Unspecified` and
  serialized without `Z`. Spec Q10 says to fix it here.
- 37 tests: xUnit, `TestDb` (SQLite in-memory), `TestAppFactory` (WebApplicationFactory, optional shared connection).

Nothing in the data model needs to change for LT-002 (see Data).

## Approach
1. **Identity (`X-Employee-Id`):** add a small `CurrentEmployee` helper in `Endpoints/`. It reads the header, parses an int and
   loads the employee. A missing or non-numeric header, or an unknown id, returns **400** ProblemDetails with
   "Pick who you are from the employee list (send your employee id in the X-Employee-Id header)." (AC3). It is
   used only on the endpoints that depend on who you are. `GET /api/employees` and both `GET /api/leave-requests` routes stay
   anonymous (AC1, AC13, Q7). As project.md requires, the header is trusted as-is and documented as a demo limitation.
2. **Failure kinds:** extend `RuleViolationException` with a `Kind` (`Validation`, `NotFound`, `Forbidden`, `Conflict`),
   defaulting to `Validation` so LT-001 call sites are unchanged. A new `Endpoints/RuleResults.cs` maps a caught
   exception to `400 ValidationProblem` / `404` / `403` / `409` ProblemDetails, with `detail` set to the plain message (AC10–AC13).
   The services decide *what* went wrong, and the endpoints only translate it to HTTP.
3. **Asking for leave:** `POST /api/leave-requests` with the current person as requester. The body may include
   `employeeId`. If it is present and differs from the current person, the service-level check throws Forbidden:
   "You can only ask for leave for yourself." (AC5). Dates and type arrive as **strings** and are parsed in the endpoint,
   so bad formats get plain messages ("First day must be a date like 2026-12-14.", "Leave type must be annual, sick or
   unpaid.") instead of the framework's generic binding error. The rest is the existing `AddAsync` (AC2, AC4).
4. **Deciding:** make `LeaveRequestService.RecordDecisionAsync` **public** and add these rules, in this order:
   request exists (NotFound) → decider is a manager (Forbidden) → requester reports to the decider (Forbidden:
   "This request is from Alex Turner, who is not in your team.") → status is Pending (Conflict: "This request was
   already approved by Maya Manager.") → a rejection needs a non-blank reason (Validation) → comment/reason ≤ 500. The time comes
   from an injected `TimeProvider` (BCL, `TimeProvider.System` in DI), so tests can use a fixed clock. Two thin endpoints
   call it: `POST /api/leave-requests/{id}/approve` `{ comment? }` and `POST /api/leave-requests/{id}/reject` `{ reason }`
   (AC8–AC12). The seeder keeps calling the same method, and its decisions already satisfy the team rule.
5. **Own and team lists:** add `LeaveRequestService` query methods `GetForEmployeeAsync(id)` (newest first = **descending
   id**, Q5) and `GetPendingForTeamAsync(managerId)` (direct reports only, Pending only, ordered by first day then id,
   Q3/Q6). A non-manager calling the team list gets Forbidden "Only managers can see team requests." (AC6–AC8).
   Routes: `GET /api/me/leave-requests` and `GET /api/team/leave-requests/pending`.
6. **Days covered:** a static `LeaveRequestService.DaysCovered(first, last) = last.DayNumber - first.DayNumber + 1`
   (calendar days, inclusive, Q4). `LeaveRequestResponse` gains `daysCovered`. All endpoints that return requests build
   the response through one shared mapper (`LeaveRequestResponse.From(entity)`). The mapper runs after loading with
   `Include(Employee, DecidedBy)`, because `DayNumber` does not translate to SQL (AC14).
7. **UTC fix:** add a value converter in `LeaveDbContext` that marks `DecidedAtUtc` as `DateTimeKind.Utc` on read, so JSON
   ends with `Z` (AC9, Q10).

**Alternatives considered**
- *Add a `CreatedAtUtc` column for "newest first"*: rejected. `EnsureCreated()` does not alter an existing `leave.db`,
  so anyone with an LT-001 database would crash on start, and migrations are off by convention. The SQLite integer primary key
  only grows, and nothing deletes requests, so descending id gives exactly "the order in which requests were made".
  If LT-005 needs real timestamps, that requirement can introduce a schema change deliberately.
- *Typed `DateOnly`/`LeaveType` fields in the POST body*: rejected. The framework's binding errors are generic, while the spec
  requires plain messages naming the problem.
- *Separate exception classes per failure (`NotFoundException`, …)*: rejected in favour of one `Kind` on the existing
  exception. It is smaller, keeps field-level errors in one shape, and needs only one catch.
- *401 for a missing identity*: rejected. There is no authentication scheme, and project.md maps "not allowed for this caller"
  to 403 and bad input to 400. A missing or unknown picked person is bad input.

## Affected files
| Path | Change |
|---|---|
| app/src/LeaveTracker.Api/Program.cs | modify (register `TimeProvider.System`, map new endpoint groups) |
| app/src/LeaveTracker.Api/Data/LeaveDbContext.cs | modify (UTC converter on `DecidedAtUtc`) |
| app/src/LeaveTracker.Api/Services/RuleViolationException.cs | modify (add `RuleKind` and `Kind`) |
| app/src/LeaveTracker.Api/Services/LeaveRequestService.cs | modify (public decisions with team/reason rules, `TimeProvider`, requester check, queries, `DaysCovered`) |
| app/src/LeaveTracker.Api/Data/DemoSeeder.cs | modify (call the now-public decision method with explicit times) |
| app/src/LeaveTracker.Api/Contracts/LeaveRequestResponse.cs | modify (add `DaysCovered`, `From(entity)` mapper) |
| app/src/LeaveTracker.Api/Contracts/CreateLeaveRequest.cs | create |
| app/src/LeaveTracker.Api/Contracts/DecisionRequests.cs | create (`ApproveRequest`, `RejectRequest`) |
| app/src/LeaveTracker.Api/Endpoints/CurrentEmployee.cs | create |
| app/src/LeaveTracker.Api/Endpoints/RuleResults.cs | create |
| app/src/LeaveTracker.Api/Endpoints/LeaveRequestEndpoints.cs | modify (POST create, approve, reject; mapper for reads) |
| app/src/LeaveTracker.Api/Endpoints/MyRequestsEndpoints.cs | create (`/api/me/leave-requests`, `/api/team/leave-requests/pending`) |
| app/tests/LeaveTracker.Tests/TestAppFactory.cs | modify (`CreateClientAs(employeeId)`, fixed `TimeProvider` option) |
| app/tests/LeaveTracker.Tests/LeaveRequestServiceTests.cs | modify (team rule, reason rule, kinds, `DaysCovered`) |
| app/tests/LeaveTracker.Tests/LeaveRequestApiTests.cs | modify (`daysCovered`, UTC `Z` assertion) |
| app/tests/LeaveTracker.Tests/SeedTests.cs | modify only if the seeder signature change requires it |
| app/tests/LeaveTracker.Tests/IdentityApiTests.cs | create |
| app/tests/LeaveTracker.Tests/AskForLeaveApiTests.cs | create |
| app/tests/LeaveTracker.Tests/MyAndTeamRequestsApiTests.cs | create |
| app/tests/LeaveTracker.Tests/DecisionApiTests.cs | create |

## Data
- **No schema change.** No new tables or columns, so existing `leave.db` files from LT-001 keep working.
- `LeaveDbContext`: `DecidedAtUtc` gets a read-side converter `v => DateTime.SpecifyKind(v, Utc)`. Stored values are
  unchanged (they were already written in UTC).
- "Newest first" uses descending `Id`, which follows insertion order (see Alternatives).
- Seed data is unchanged: 1 manager, 3 reports, 4 requests. The seeder's decisions are by Maya on her own reports, which
  satisfies the new team rule. The rejected seed request already has a reason.

## API
All new error responses are ProblemDetails, with `detail` set to the plain message. Validation failures are `ValidationProblem`
with per-field messages. "Who" means the `X-Employee-Id` header.

| Method | Route | Request | Response | Codes |
|---|---|---|---|---|
| GET | /api/employees | — | unchanged (anonymous) | 200 |
| POST | /api/leave-requests | who + `{ firstDay: "yyyy-MM-dd", lastDay, type: "annual"\|"sick"\|"unpaid", reason?, employeeId? }` | `LeaveRequestResponse` (status `pending`), `Location: /api/leave-requests/{id}` | 201; 400 (missing/unknown who, bad dates/type/reason, last < first); 403 (`employeeId` ≠ who) |
| GET | /api/me/leave-requests | who | `LeaveRequestResponse[]`, newest first | 200; 400 (who) |
| GET | /api/team/leave-requests/pending | who | `LeaveRequestResponse[]`, Pending from direct reports, first day ascending | 200; 400 (who); 403 (not a manager) |
| POST | /api/leave-requests/{id}/approve | who + `{ comment? }` | `LeaveRequestResponse` (status `approved`) | 200; 400 (who, comment > 500); 403 (not a manager / not your team); 404 (no such request); 409 (already decided) |
| POST | /api/leave-requests/{id}/reject | who + `{ reason }` | `LeaveRequestResponse` (status `rejected`) | 200; 400 (who, missing/blank reason, > 500); 403; 404; 409 |
| GET | /api/leave-requests | — | `LeaveRequestResponse[]` (now with `daysCovered`) | 200 |
| GET | /api/leave-requests/{id} | — | `LeaveRequestResponse` (now with `daysCovered`) | 200; 404 |

`LeaveRequestResponse` = LT-001 fields + `daysCovered` (int). `decidedAtUtc` is ISO 8601 with `Z`.

Rule check order on decide (first failure wins): who → request exists (404) → caller is a manager (403) → requester in
caller's team (403) → Pending (409) → reason/comment (400). This order means a stranger to the team never learns the
decision state of a request they may not touch. A 404 still reveals whether the request exists, but anyone can read
it anonymously anyway (AC13).

## Risks
| Risk | Mitigation |
|---|---|
| Anyone can act as anyone by setting `X-Employee-Id` | This is the known demo limitation in project.md. It is documented in code comments and in the MR. No real data is used. Logging in is out of scope (stated). |
| Two managers deciding the same request at the same moment | SQLite serializes writes. The Pending check and update happen in one `SaveChanges` on the tracked entity. A concurrent second decision may still overwrite in a race, which is acceptable for a single-team demo and noted for LT-005. |
| Making `RecordDecisionAsync` public widens the service surface | It is now the single guarded entry point for decisions, with all rules inside the service. The endpoints add no business logic. |
| `Kind` defaulting to Validation could hide a wrong mapping | Each new failure path has an API test asserting the exact status code and message. |
| Descending id ≠ creation order if rows were deleted and SQLite reused the max rowid | Nothing deletes requests in LT-001/002. Cancelling (Q1) is out of scope. If cancel is added as a delete, revisit this ordering. |
| Changing the shape of `LeaveRequestResponse` (new field) | The change is additive only. No existing field is renamed or removed. |
| Seeder now passes through the team rule | `SeedTests` still assert counts and states. A failure there would show the rule is wrong. |
| New dependency | None. `TimeProvider` is part of .NET 8. No NuGet changes. |

## Test strategy
All HTTP behaviour is tested through `TestAppFactory` (SQLite in-memory). `CreateClientAs(id)` sets `X-Employee-Id`.
Extra employees (a second manager and their report) are added in tests through `EmployeeService`.

| AC | Test type | Test |
|---|---|---|
| AC1 | Integration (API) | `IdentityApiTests.Employee_list_needs_no_picked_person` (no header → 200, names and `isManager` present) |
| AC2 | Integration (API) | `AskForLeaveApiTests.Employee_asks_for_leave_and_it_is_pending` (201, Location, pending, no decision, `daysCovered` 3) |
| AC3 | Integration (API) | `IdentityApiTests.Actions_without_picked_person_are_refused` (theory: no header / `abc` / unknown id × create, mine, team, approve, reject → 400 with "Pick who you are") |
| AC4 | Integration (API) | `AskForLeaveApiTests.Last_day_before_first_day_is_refused`, `Bad_type_is_refused`, `Bad_date_format_is_refused`, `Reason_too_long_is_refused` (400, plain message, nothing kept) |
| AC5 | Integration (API) | `AskForLeaveApiTests.Asking_for_someone_else_is_refused` (403, "only ask for leave for yourself", nothing kept) |
| AC6 | Integration (API) | `MyAndTeamRequestsApiTests.Own_requests_are_all_states_newest_first` (Sam: 2 seeded + 1 new → new first, only Sam's, `daysCovered` present) |
| AC7 | Integration (API) | `MyAndTeamRequestsApiTests.Team_pending_shows_only_own_direct_reports` (second manager plus report with a pending request is excluded; decided ones excluded) |
| AC8 | Integration (API) | `MyAndTeamRequestsApiTests.Non_manager_cannot_see_team_requests`, `DecisionApiTests.Non_manager_cannot_decide` (403) |
| AC9 | Integration (API) | `DecisionApiTests.Manager_approves_with_and_without_comment` (200, approved, `decidedById`, `decidedAtUtc` ends with `Z`, comment) |
| AC10 | Integration (API) | `DecisionApiTests.Manager_rejects_with_reason`, `Reject_without_reason_is_refused` (theory: null, "", "   " → 400, still pending) |
| AC11 | Integration (API) | `DecisionApiTests.Cannot_decide_request_outside_team` (other manager's report and Maya's own request → 403, still pending) |
| AC12 | Integration (API) | `DecisionApiTests.Already_decided_request_cannot_be_decided_again` (approve twice and reject an approved request → 409, "already approved by Maya Manager", unchanged) |
| AC13 | Integration (API) | `LeaveRequestApiTests.Get_request_by_id_returns_it` (+ `daysCovered`, no header), `Get_unknown_request_returns_404_with_plain_message`, `DecisionApiTests.Deciding_unknown_request_is_404` |
| AC14 | Unit + API | `LeaveRequestServiceTests.Days_covered_is_inclusive_calendar_days` (theory: same day 1, 14→16 is 3, across month and year ends), plus `daysCovered` asserted in the AC2/AC6/AC7/AC13 tests and in `LeaveRequestApiTests.Fresh_start_has_requests_in_every_state` |
| LT-001 regression | Unit + API | Existing 37 tests stay green. `LeaveRequestServiceTests.Decision_only_from_pending_and_by_a_manager` is updated for the team rule. `LeaveRequestApiTests.Decided_requests_show_decider_time_and_comment` gains the `Z` assertion. |

Manual check (under 2 minutes): `dotnet run --project app/src/LeaveTracker.Api`. Then, using any HTTP client with
`X-Employee-Id`: POST a request as Alex, GET `/api/me/leave-requests` as Alex, GET `/api/team/leave-requests/pending`
as Maya, POST `/approve` as Maya, and POST `/approve` again to see the 409 message.
