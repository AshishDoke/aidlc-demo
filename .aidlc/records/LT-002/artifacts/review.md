# LT-002 Ask for leave and decide on it: AI review

## Summary
- Employees ask for leave for themselves (`POST /api/leave-requests`). The request starts as pending, with plain-words validation of dates, type and reason, and asking for someone else is refused (403).
- Employees see all their own requests, newest first (`GET /api/me/leave-requests`). Managers see only their direct reports' pending requests (`GET /api/team/leave-requests/pending`).
- Managers approve (optional comment) or reject (reason required) pending requests from their own team only (`POST …/{id}/approve|reject`). A second decision is refused with 409 and says how and by whom the request was decided.
- Who you are is the `X-Employee-Id` header (picked person, no login). A missing, non-numeric or unknown value gets 400 "Pick who you are …". Rule failures carry a `Kind`, mapped to 400/403/404/409.
- Every request shows `daysCovered` (calendar days, inclusive). Decision times now end in `Z`, which fixes LT-001 review item 1.

## Acceptance criteria verification
| AC | Met? | Evidence |
|---|---|---|
| AC1 | Yes | `GET /api/employees` unchanged and anonymous. `IdentityApiTests.Employee_list_needs_no_picked_person` |
| AC2 | Yes | `LeaveRequestEndpoints` POST → `AskForSelfAsync`. `AskForLeaveApiTests.Employee_asks_for_leave_and_it_is_pending` (201, pending, no decision, 3 days) |
| AC3 | Yes | `Endpoints/CurrentEmployee.cs`. `IdentityApiTests.Actions_without_picked_person_are_refused` (15 cases across all 5 identity-dependent routes) |
| AC4 | Yes | `ParseDay`/`ParseType` plus the LT-001 `AddAsync` rules. `AskForLeaveApiTests.Last_day_before_first_day_is_refused`, `Bad_type_is_refused`, `Bad_or_missing_dates_are_refused`, `Reason_too_long_is_refused` |
| AC5 | Yes | `LeaveRequestService.AskForSelfAsync` (Forbidden). `AskForLeaveApiTests.Asking_for_someone_else_is_refused` |
| AC6 | Yes | `GetForEmployeeAsync` (descending id). `MyAndTeamRequestsApiTests.Own_requests_are_all_states_newest_first` |
| AC7 | Yes | `GetPendingForTeamAsync` (Pending and `Employee.ManagerId == me`). `MyAndTeamRequestsApiTests.Team_pending_shows_only_own_direct_reports` (other team, own request and decided ones excluded; order checked) |
| AC8 | Yes | Forbidden in `GetPendingForTeamAsync` and `RecordDecisionAsync`. `MyAndTeamRequestsApiTests.Non_manager_cannot_see_team_requests`, `DecisionApiTests.Non_manager_cannot_decide` |
| AC9 | Yes | `RecordDecisionAsync`, `TimeProvider`, UTC converter. `DecisionApiTests.Manager_approves_with_and_without_comment` (`Z` asserted, time within 5 minutes of now) |
| AC10 | Yes | Reason rule in `RecordDecisionAsync`. `DecisionApiTests.Manager_rejects_with_reason`, `Reject_without_reason_is_refused` (null, empty, spaces; stays pending) |
| AC11 | Yes | Team rule (`request.Employee.ManagerId != deciderId`). `DecisionApiTests.Cannot_decide_request_outside_team` (other team's report, own request, other manager on Maya's request) |
| AC12 | Yes | Conflict rule. `DecisionApiTests.Already_decided_request_cannot_be_decided_again` (original comment and time unchanged), `Seeded_rejected_request_cannot_be_approved` |
| AC13 | Yes | Anonymous `GET /api/leave-requests/{id}`, NotFound kind. `LeaveRequestApiTests.Get_request_by_id_returns_it`, `Get_unknown_request_returns_404_with_plain_message`, `DecisionApiTests.Deciding_unknown_request_is_404` |
| AC14 | Yes | `LeaveRequestService.DaysCovered`, `LeaveRequestResponse.From` used by every endpoint that returns requests. `LeaveRequestServiceTests.Days_covered_is_inclusive_calendar_days` (5 cases) plus API assertions |

## Code quality
1. **Minor: an empty or malformed POST body gets the framework's generic 400.** If `POST /api/leave-requests` is sent
   with no body or invalid JSON, minimal-API binding rejects it before our code runs. The response is a 400 with no
   plain-words `detail` (a body that is valid JSON but empty is fine: it gets the "First day is required" messages). The
   spec's "clear message" rule is about bad *content*, which is fully covered, and the screens in LT-003/LT-004 will always
   send JSON. Still, a small fix would be to make the body parameter nullable and return "Send the request as JSON with firstDay,
   lastDay and type." I did not change it, to stay inside the approved design. Recommended for LT-003.
2. **Concurrency:** two managers deciding the same request in the same instant could both pass the Pending check. This is
   accepted in the design for a one-team demo. In practice only one manager can decide (the requester's own manager), so the
   remaining race is the same manager double-clicking. LT-004's screen should disable the button after a click. A concurrency token
   would be the proper fix if it ever matters.
3. **Layering:** the rules live in `LeaveRequestService`. The endpoints only resolve identity, parse formats and map
   `RuleKind` to HTTP. That follows project.md. Format parsing of dates and type in the endpoint is input shape, not business rules.
4. **Rule order on decide** matches the design (404 → manager 403 → team 403 → 409 → 400), so a manager from another team
   never learns a request's decision state through the error message. One consequence is that an *unknown decider id* reaching the
   service would be a 400 Validation. Over HTTP this cannot happen, because `CurrentEmployee` rejects unknown ids first.
5. **Reads now materialize entities** (`Include` plus in-memory mapping) instead of projecting in SQL, because `DateOnly.DayNumber`
   does not translate. That is fine at demo scale. A large list would want paging (not in scope).
6. **Compatibility:** `LeaveRequestResponse` only gained a field. `RuleViolationException` constructors gained optional
   parameters. `RecordDecisionAsync` went from internal to public, with one extra optional parameter. All 37 LT-001 tests still pass.
7. **Small leftovers:** the `InternalsVisibleTo` added in LT-001 is no longer needed for decisions but is harmless. The
   `CurrentEmployee` messages echo the raw header value. That is safe in JSON, but unbounded in length (cosmetic).

## Security
- **Identity is not authentication.** Anyone can send any `X-Employee-Id` and act as any employee, including a manager. This is
  the documented demo limitation (project.md, spec: "Logging in" out of scope). It is called out in a code comment in
  `CurrentEmployee.cs`. **This must not go to real users without a login.**
- **Authorization rules inside that trust model are enforced server-side** in the service (self-only asking, managers only,
  own team only, pending only), not in the UI. They are covered by API tests.
- **Information exposure:** the employee list and single-request lookups stay anonymous by design (AC1, AC13). That includes leave
  reasons, which can be sensitive for sick leave. It is acceptable with fictional data. In a real deployment, sick-leave reasons should be
  limited to the requester and their manager. This is noted for LT-005/HR.
- **Input handling:** all data access is through EF Core LINQ (parameterized). Dates are parsed with an exact format and the
  invariant culture. The leave type rejects numeric strings. Text fields are trimmed and limited to 500 characters.
- **Secrets / logging:** there are no secrets and no new logging of personal data. EF sensitive-data logging stays off.
- **Dependencies:** none added (`TimeProvider` is in the .NET 8 BCL).

## Guardrail report
- Files changed: 18 · Lines changed: 1167 · Violations: 0
- Tests: 97 passed, 0 failed, 0 skipped (re-run by the engine in TEST).

## Recommendation
**APPROVE.** All 14 acceptance criteria are met with automated API-level evidence, the LT-001 review item is fixed, the
guardrail scan is clean and there are no new dependencies. Known follow-ups, none of which block this change: plain message for an
empty or malformed body (Code quality, item 1) and disabling the decide button against double-clicks (item 2). Both fit LT-003/LT-004. The
login gap is a deliberate, documented demo limitation.
