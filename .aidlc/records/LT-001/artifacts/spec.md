# LT-001 Keep track of employees and their leave: Specification

## Overview
Leave is currently tracked in spreadsheets and emails, so nobody is sure who is off when or whether a
request was approved. This requirement gives the Team Leave Tracker its foundation: it records the team's
employees (including who manages whom) and their leave requests with their decision history. It also
provides a ready-made demo team so later requirements (starting with LT-002) and demos have data to work with.
HR, managers and employees benefit indirectly; no screens or request/decision actions are delivered here.

## Requirement
1. The system keeps a record of every employee: their name, their email address, and whether they are a manager.
2. Every employee who is not a manager is linked to exactly one manager. A manager is not required to have a manager.
3. Email addresses are unique across all employees; the system refuses to keep two employees with the same email.
4. The system keeps leave requests. Each request records the employee who asked, the first day and last day
   of leave, the leave type (one of: annual, sick, unpaid) and an optional free-text reason.
5. A newly recorded leave request is in the state "pending".
6. A request can later be in the state "approved" or "rejected". For a decided request the system keeps who
   decided, when the decision was made, and an optional comment from the decider.
7. When the app starts with no team present, it creates a ready-made demo team: one manager, three employees
   who report to that manager, and a few example leave requests covering pending, approved and rejected.
   Starting the app again never creates the team or its requests a second time.
8. The app can be checked as running, and its data store is created automatically on first start (engineering defaults).

## Glossary
- **Employee**: any person known to the system, including managers.
- **Manager**: an employee marked as a manager. A manager may decide on leave requests (from LT-002 onward).
- **Reports to**: the link from a non-manager employee to their single manager.
- **Leave request**: one record of an employee asking for leave over a continuous range of calendar days.
- **First day / last day**: calendar dates (no time of day), both included in the leave period.
- **Leave type**: exactly one of `annual`, `sick`, `unpaid`.
- **Pending**: a request that has not yet been decided. **Approved** / **Rejected**: a request that has been decided.
- **Decided by / decided at / decision comment**: the manager who made the decision, the date and time it was
  made, and the optional text they left.
- **Demo team / seed data**: the fixed example employees and requests created automatically for demos and tests.

## Scope
- Persistent records of employees: name, email, manager flag, and the manager they report to.
- Rules on employee data: unique email, non-managers have exactly one manager.
- Persistent records of leave requests: requester, first day, last day, leave type, optional reason, status.
- Persistent decision details on a request: decided by, decided at, optional comment.
- Automatic creation of the data store on first start.
- Idempotent demo team seeding on start-up.
- A health check showing the app is running (engineering default).
- A minimal read-only way to see the stored employees and leave requests, so the ACs can be verified
  without screens (see Q5).

## Acceptance criteria
- AC1 (derived): Given the app has just been started with no existing data, when someone checks whether it is
  running, then it reports that it is healthy, and the data store exists without any manual setup step.
- AC2 (stated): Given a fresh start, when the stored employees are listed, then there are exactly four: one
  manager and three non-managers, each with a name and an email, and each of the three non-managers reports
  to that manager.
- AC3 (stated): Given the demo team exists, when the app is started again (one or more times), then there are
  still exactly four demo employees and the same number of demo leave requests as after the first start.
- AC4 (stated): Given an employee with email `x@example.com` exists, when the system is asked to keep another
  employee with the same email (compared ignoring upper/lower case), then it refuses and keeps only the original,
  with a plain-words message saying the email is already in use.
- AC5 (stated): Given a non-manager employee is to be kept without a manager, or with a "manager" who is not
  marked as a manager, when the system is asked to keep them, then it refuses with a plain-words message
  saying a non-manager must report to exactly one manager.
- AC6 (stated): Given a fresh start, when the stored leave requests are listed, then there is at least one in
  each state (pending, approved, rejected), each showing the requester, first day, last day, leave type
  (annual, sick or unpaid) and reason (which may be empty).
- AC7 (stated): Given a demo request that is approved or rejected, when it is viewed, then it shows which
  manager decided it, when, and the comment (which may be empty). Given a pending request, then it has no
  decider, decision time or comment.
- AC8 (stated): Given a new leave request is recorded, when it is viewed, then its state is pending.
- AC9 (derived): Given a leave request whose last day is before its first day, or whose leave type is not one
  of annual, sick or unpaid, or with no requester, when the system is asked to keep it, then it refuses with a
  plain-words message naming the problem.
- AC10 (derived): Given a request that is for a single day, when the first day equals the last day, then the
  system accepts it.

## Out of scope
- Any screens or pages (stated).
- Employees asking for leave, and managers approving or rejecting it, as user actions (stated; LT-002).
- Adding, editing or deleting employees through the app (only the rules for keeping them are in scope).
- Leave balances, allowances, counting of working days, public holidays, overlapping-request checks.
- Cancelling or withdrawing a request.
- Authentication or real identity; the demo identity mechanism arrives with the requirements that need it.
- Teams with more than one level of management, or multiple teams.

## Dependencies
- None. Matches front matter (`depends_on: []`): yes.
- Note: LT-002 will build on the employee, manager and leave request records defined here.

## Open questions
- Q1: Is email uniqueness case-insensitive (is `Ana@x.com` the same as `ana@x.com`)? **Assumption:** yes,
  emails are compared ignoring case and surrounding spaces.
- Q2: Can a manager have their own manager (e.g. a department head)? **Assumption:** a manager may optionally
  report to another manager, but is not required to. The demo manager reports to no one.
- Q3: Are "first day" and "last day" both included, and are they calendar days rather than working days?
  **Assumption:** both are included; plain calendar dates. Day counting is not part of this requirement.
- Q4: Is the decision time a date and time or just a date, and in which timezone? **Assumption:** date and
  time, recorded in UTC.
- Q5: The requirement says no screens. Should the stored data be viewable at all in LT-001? **Assumption:**
  a minimal read-only listing of employees and leave requests is provided so the ACs can be verified; it
  will be reused by LT-002. Creating/changing data through the app is not included.
- Q6: "A few example requests": how many exactly? **Assumption:** at least three (one pending, one approved,
  one rejected) across different demo employees and leave types; the exact set is fixed in DESIGN.
- Q7: Is the reason length limited? Is the decision comment limited? **Assumption:** both optional, up to
  500 characters.
- Q8: Are name and email required, and must the email look like an email? **Assumption:** both required and
  not blank; email must contain one `@` with text on both sides.
- Q9: What does "restarting must not create the team twice" mean if someone has changed or deleted demo data?
  **Assumption:** seeding only happens when there are no employees at all; otherwise it does nothing.
- Q10: Must the demo data use fictional people? **Assumption:** yes, fictional names and `example.com` emails only.
