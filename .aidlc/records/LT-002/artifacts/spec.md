# LT-002 Ask for leave and decide on it: Specification

## Overview
LT-001 gave the system its people and a place to keep leave requests. LT-002 adds the actual process.
An employee asks for leave for themselves, and their manager approves or rejects it. Everyone can see the
state of requests, and every request shows how many days it covers. There are no screens yet. The
employee and manager screens (LT-003, LT-004) will be built on this behaviour, so it must give clear,
plain-words messages whenever something is not allowed.

## Requirement
1. Anyone can see the list of employees, so they can pick who they are.
2. The person acting picks who they are from that list. There is no login (stated as not included).
   Actions that depend on who you are (asking for leave, seeing your own requests, seeing your team's
   pending requests, deciding) require a picked person. If none is picked, or the picked person is unknown,
   the action is refused with a clear message.
3. An employee can ask for leave for themselves only: first day, last day, leave type (annual, sick, unpaid)
   and an optional reason. The new request is pending. The rules for keeping a request from LT-001 still apply.
4. An employee can see all of their own requests (any state), newest first.
5. A manager can see the pending requests of the people in their team, and only their team.
6. A manager can approve or reject a pending request from someone in their team. A rejection must include
   a reason. An approval may include an optional comment. The system keeps who decided, when, and the
   reason or comment (as in LT-001).
7. A request that has been approved or rejected cannot be decided again.
8. Anyone can look up a single request.
9. Every request, wherever it is shown, includes how many days it covers.
10. When something is not allowed or does not make sense, the person gets a clear message saying why. This
    covers at least: the request does not exist, it is not your team, it was already decided, and the last
    day is before the first day.

## Glossary
- **Picked person / current person**: the employee the caller says they are, chosen from the employee list.
  It is trusted as-is (no login). It is not a security control.
- **Team (of a manager)**: the employees whose manager (from LT-001) is that manager. These are direct reports
  only, not reports of reports. A manager is never in their own team.
- **Manager**: an employee marked as a manager (LT-001).
- **Days it covers**: the number of calendar days from the first day to the last day, both included. A
  single-day request covers 1 day. Weekends and holidays are counted (weekend rules come in LT-005).
- **Newest first**: the most recently asked-for request first (the order in which requests were made), not
  ordered by leave dates.
- **Decide / decision**: approve or reject. **Rejection reason**: the text a manager must give when rejecting,
  kept as the decision comment from LT-001. **Approval comment**: optional text kept the same way.
- **Pending / approved / rejected**: as in LT-001.

## Scope
- Listing all employees without having picked a person (already exists in LT-001; confirmed here).
- Identifying the current person by picking an employee, with clear refusals when missing or unknown.
- An employee asking for leave for themselves, starting as pending.
- An employee seeing all their own requests, newest first.
- A manager seeing the pending requests of their own team only.
- A manager approving (optional comment) or rejecting (required reason) a pending request from their team.
- Refusing a second decision on an already decided request.
- Looking up any single request without having picked a person.
- Showing the number of days covered on every request.
- Clear, plain-words refusal messages for every rule in this spec.
- Decision times are shown as clearly UTC (carried over from the LT-001 review, item 1).

## Acceptance criteria
- AC1 (stated): Given no person is picked, when someone asks for the employee list, then they get every
  employee with their name and whether they are a manager, so they can pick who they are.
- AC2 (stated): Given employee Alex is picked, when Alex asks for leave from 2026-12-14 to 2026-12-16 of type
  annual with reason "Trip", then a new request is kept for Alex with those details, its state is pending, it
  has no decision, and it shows 3 days.
- AC3 (derived): Given no person is picked, or the picked person does not exist, when someone tries to ask
  for leave, see their own requests, see team requests, or decide, then the action is refused with a clear
  message saying who they are must be picked from the employee list.
- AC4 (stated): Given Alex is picked, when Alex asks for leave with a last day before the first day, then it
  is refused with a message saying the last day is before the first day, and nothing is kept. The other LT-001
  rules (leave type must be annual, sick or unpaid; reason at most 500 characters) are refused the same way.
- AC5 (derived): Given Alex is picked, when Alex tries to ask for leave on behalf of another employee, then
  it is refused with a message saying you can only ask for leave for yourself, and nothing is kept.
- AC6 (stated): Given Sam has made three requests at different times, when Sam asks for their own requests,
  then all three are returned (pending, approved and rejected alike), the most recently made one first, each
  with its days covered. No requests of other employees are included.
- AC7 (stated): Given manager Maya is picked and her team has pending and decided requests, and another
  manager's team also has pending requests, when Maya asks for her team's pending requests, then only the
  pending requests of Maya's direct reports are returned, each with requester name and days covered.
- AC8 (derived): Given a person who is not a manager is picked, when they ask for team pending requests or
  try to decide a request, then it is refused with a message saying only managers can do this.
- AC9 (stated): Given Maya is picked and Alex (her report) has a pending request, when Maya approves it with
  or without a comment, then the request becomes approved and shows Maya as the decider, the decision time
  (marked as UTC) and the comment if one was given.
- AC10 (stated): Given Maya is picked and Alex has a pending request, when Maya rejects it with the reason
  "Release week", then it becomes rejected and shows Maya, the decision time and the reason. When Maya rejects
  it with no reason or only spaces, then it is refused with a message saying a rejection needs a reason, and
  the request stays pending.
- AC11 (stated): Given a pending request from someone who is not in Maya's team (including Maya's own
  request), when Maya tries to approve or reject it, then it is refused with a message saying the request is
  not from your team, and it stays pending.
- AC12 (stated): Given a request that is already approved or rejected, when its manager tries to approve or
  reject it again, then it is refused with a message saying it was already decided (and how), and the original
  decision is unchanged.
- AC13 (stated): Given no person is picked, when someone looks up a single existing request, then they see
  all its details, including state, decision details and days covered. When they look up a request that does
  not exist, then they get a message saying no such request exists. Deciding a request that does not exist
  gives the same kind of message.
- AC14 (stated): Given any request shown anywhere (single lookup, own list, team list, full list), then it
  includes the days covered, calculated as calendar days from first to last day inclusive (for example,
  2026-12-14 to 2026-12-16 is 3; a single day is 1).

## Out of scope
- Detailed leave rules: requests in the past, maximum length, overlapping requests, weekends and holidays
  (stated; LT-005). Days covered therefore counts calendar days.
- Logging in or any real identity check (stated). The picked person is trusted.
- Screens (stated; LT-003, LT-004).
- Cancelling or withdrawing a request (requirement lists it as still to decide; see Q1).
- Editing a request after it was made, or changing a decision.
- Managers seeing the decided requests of their team, or the requests of indirect reports.
- Notifications (email etc.).
- Leave balances or allowances.

## Dependencies
- LT-001: employees, managers and the "reports to" link; leave request records with decision details; the
  demo team; the rules for keeping requests (dates, type, reason length). Matches front matter: yes.

## Open questions
- Q1: Can an employee cancel their own pending request? (Listed by the requirement as "still to decide".)
  **Assumption:** not in LT-002. No cancel action is built, and it should be decided before LT-003.
- Q2: Can a manager ask for leave for themselves, and who decides it? **Assumption:** yes, any employee
  including a manager can ask. It can only be decided by the manager the requester reports to. A manager who
  reports to no one (like the demo manager) can ask, but nobody can decide it in LT-002. It stays pending, and
  this is noted for HR.
- Q3: Does "their team" include indirect reports (reports of reports)? **Assumption:** no, direct reports only.
- Q4: Does "how many days it covers" count calendar days or working days? **Assumption:** calendar days,
  first and last day included. Working-day counting belongs with the weekend rules in LT-005 and may change
  this number then.
- Q5: Is "newest first" by when the request was made, or by leave dates? **Assumption:** by when it was made
  (most recent first).
- Q6: In what order are a manager's team pending requests shown? **Assumption:** the requirement does not say.
  Earliest first day first, so the most urgent decisions come first.
- Q7: Can "anyone" who looks up a single request, or lists employees, do so without picking a person?
  **Assumption:** yes, no picked person is needed for these two reads (and for the existing full request list
  from LT-001).
- Q8: How long can a rejection reason or approval comment be? **Assumption:** up to 500 characters, as in
  LT-001. Surrounding spaces are removed, and a reason of only spaces counts as no reason.
- Q9: Should the "already decided" message say how it was decided? **Assumption:** yes, for example "This
  request was already approved by Maya Manager."
- Q10: What happens to decision times that were already shown without a UTC marker (LT-001 review item 1)?
  **Assumption:** LT-002 fixes it so all decision times are shown as UTC. No stored data changes.
