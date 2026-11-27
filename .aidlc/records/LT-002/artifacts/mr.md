# LT-002: Ask for leave and decide on it

## Summary
This adds the leave process on top of LT-001. An employee asks for leave for themselves and the request starts as
pending. They can see all their own requests, newest first. A manager sees only their direct reports' pending requests, and
approves them (optional comment) or rejects them (reason required). A decided request cannot be decided again. Every request
shows how many calendar days it covers, and decision times are now clearly UTC (fixes LT-001 review item 1). Every refusal
gives a plain-words message with the matching status code (400/403/404/409). There are no screens yet (LT-003/LT-004), no
schema change and no new packages.

## Changes
- **Database:** no schema change. A read-side converter marks `DecidedAtUtc` as UTC, so JSON ends in `Z`. "Newest first" uses
  insertion order (descending id), so existing `leave.db` files keep working.
- **Business rules (`LeaveRequestService`):** `AskForSelfAsync` (self only), and a now-public `RecordDecisionAsync` with
  this rule order: request exists → caller is a manager → requester is in caller's team → still pending → a rejection
  has a reason → text ≤ 500. It also adds `GetForEmployeeAsync`, `GetPendingForTeamAsync` and `DaysCovered`, with the clock
  taken from `TimeProvider`. `RuleViolationException` gained a `Kind` (Validation/NotFound/Forbidden/Conflict).
- **API:**
  - `POST /api/leave-requests` (201)
  - `GET /api/me/leave-requests`
  - `GET /api/team/leave-requests/pending`
  - `POST /api/leave-requests/{id}/approve`
  - `POST /api/leave-requests/{id}/reject`

  Who you are is the `X-Employee-Id` header, and a missing or unknown value gives a 400 "Pick who you are …". All request
  responses gain `daysCovered`. The existing reads stay anonymous.
- **UI:** None. It comes in LT-003/LT-004.
- **Tests:** 60 new (identity, asking, deciding, own and team lists, days covered, service rules) and 2 LT-001 assertions
  strengthened. 97 in total.

## Acceptance criteria
- [x] AC1: Employee list without picking a person (`IdentityApiTests.Employee_list_needs_no_picked_person`)
- [x] AC2: Ask for leave as yourself, which starts as pending with 3 days (`AskForLeaveApiTests.Employee_asks_for_leave_and_it_is_pending`)
- [x] AC3: Missing, invalid or unknown picked person refused on all 5 identity routes (`IdentityApiTests.Actions_without_picked_person_are_refused`, 15 cases)
- [x] AC4: Last day before first day, bad type, bad or missing dates and a long reason are refused (`AskForLeaveApiTests.*_is_refused`)
- [x] AC5: Asking for someone else is refused (`AskForLeaveApiTests.Asking_for_someone_else_is_refused`)
- [x] AC6: Own requests, all states, newest first (`MyAndTeamRequestsApiTests.Own_requests_are_all_states_newest_first`)
- [x] AC7: Team pending shows direct reports only (`MyAndTeamRequestsApiTests.Team_pending_shows_only_own_direct_reports`)
- [x] AC8: Non-managers cannot see team requests or decide (`MyAndTeamRequestsApiTests.Non_manager_cannot_see_team_requests`, `DecisionApiTests.Non_manager_cannot_decide`)
- [x] AC9: Approve with or without a comment; decision time in UTC (`DecisionApiTests.Manager_approves_with_and_without_comment`)
- [x] AC10: Reject with a reason; a missing or blank reason is refused (`DecisionApiTests.Manager_rejects_with_reason`, `Reject_without_reason_is_refused`)
- [x] AC11: Requests outside the team, including your own, cannot be decided (`DecisionApiTests.Cannot_decide_request_outside_team`)
- [x] AC12: An already decided request cannot be decided again (`DecisionApiTests.Already_decided_request_cannot_be_decided_again`, `Seeded_rejected_request_cannot_be_approved`)
- [x] AC13: Anonymous single lookup; unknown request gives 404 (`LeaveRequestApiTests.Get_request_by_id_returns_it`, `DecisionApiTests.Deciding_unknown_request_is_404`)
- [x] AC14: Days covered, calendar days with both ends included, on every request (`LeaveRequestServiceTests.Days_covered_is_inclusive_calendar_days` plus API assertions)

**Known follow-ups (from the AI review, accepted at G3):**
1. An empty or malformed POST body gets the framework's generic 400 instead of a plain message (review, Code quality, item 1). Suggested for LT-003.
2. A double-click on approve or reject could race. LT-004's screen should disable the button after a click (review, item 2).
3. Demo limitation: `X-Employee-Id` is trusted, so anyone can act as anyone. This must not go to real users without a login.

## Testing
- Automated: `dotnet test app/LeaveTracker.sln` → `Passed! - Failed: 0, Passed: 97, Skipped: 0, Total: 97`
  (re-run independently by the engine in the TEST stage; the output is in `test-output.log`).
- Manual (about 2 minutes):
  1. Run `dotnet run --project app/src/LeaveTracker.Api` and note Alex's and Maya's ids from `/api/employees`.
  2. As Alex (`X-Employee-Id: <alex>`), POST `/api/leave-requests` with `{"firstDay":"2026-12-14","lastDay":"2026-12-16","type":"annual"}`. Expect 201, `pending` and `daysCovered: 3`.
  3. As Alex, GET `/api/me/leave-requests`. The new request is first.
  4. As Maya, GET `/api/team/leave-requests/pending`, then POST `/api/leave-requests/{id}/approve`. Expect 200 and a `decidedAtUtc` ending in `Z`.
  5. POST `/approve` again. Expect 409 "already approved by Maya Manager".

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
| G1 (Specification) | Ashish Doke | 2026-11-27T09:43:02Z |
| G2 (Design) | Ashish Doke | 2026-11-27T09:53:30Z |
| G3 (Raise MR) | Ashish Doke | 2026-11-27T10:22:45Z (bound to HEAD c7e1910) |

Generated with AIDLC. All gates approved by humans. AI did not merge.
Full trace: `.aidlc/records/LT-002/`
