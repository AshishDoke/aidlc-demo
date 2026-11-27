# LT-003 Employee screen for requesting leave: Plan

## Tasks
- **T1**: Serve the employee page. Add `UseDefaultFiles`/`UseStaticFiles` in `Program.cs`, plus `wwwroot/index.html` (picker, hidden
  manager link, form with labelled fields, error slots and live regions, and the "My requests" table) and `wwwroot/css/site.css` (clean
  layout, visible focus, status styles that are not colour-only). Add `EmployeePageTests` for the start address, API coexistence,
  the picker and disabled form, labels, live regions, table columns, the hidden manager link and the focus style. (covers AC1, AC2, AC8, AC9, AC10)
- **T2**: Shared front-end helpers. Add `wwwroot/js/api.js` (fetch wrapper with `X-Employee-Id` and one error shape),
  `wwwroot/js/identity.js` (load employees, fill the picker, remember the choice in `localStorage`, and clear an unknown id silently) and
  `wwwroot/js/format.js` (dates as "14 Dec 2026", status and type words, "—" for an empty comment). Extend
  `EmployeePageTests` with the asset-type and status-as-text checks. (covers AC3, AC4, AC11)
- **T3**: Page behaviour. Add `wwwroot/js/employee.js`: the disabled form until someone is picked; on pick, save the choice, toggle the
  manager link, clear messages and load "My requests"; on submit, disable the button, POST, then show the confirmation, reset the form and
  reload, or show field and summary errors and focus the first invalid field. The table is built with `textContent` only. Extend
  `EmployeePageTests` with the no-external-URLs / no-`innerHTML` safety check. (covers AC5, AC6, AC7, AC8, AC9, AC12)

`allowed_files` lists exactly the design's "Affected files". No other repository file is touched.

## Plan data
```aidlc-plan
{
  "tasks": [
    {"id": "T1", "title": "Serve the employee page: HTML structure, styles and page tests", "covers": ["AC1", "AC2", "AC8", "AC9", "AC10"]},
    {"id": "T2", "title": "Shared front-end helpers: API wrapper, remembered identity, formatting", "covers": ["AC3", "AC4", "AC11"]},
    {"id": "T3", "title": "Employee page behaviour: send request, show results and errors, switch person", "covers": ["AC5", "AC6", "AC7", "AC8", "AC9", "AC12"]}
  ],
  "allowed_files": [
    "app/src/LeaveTracker.Api/Program.cs",
    "app/src/LeaveTracker.Api/wwwroot/index.html",
    "app/src/LeaveTracker.Api/wwwroot/css/site.css",
    "app/src/LeaveTracker.Api/wwwroot/js/api.js",
    "app/src/LeaveTracker.Api/wwwroot/js/identity.js",
    "app/src/LeaveTracker.Api/wwwroot/js/format.js",
    "app/src/LeaveTracker.Api/wwwroot/js/employee.js",
    "app/tests/LeaveTracker.Tests/EmployeePageTests.cs"
  ],
  "test_command": "dotnet test app/LeaveTracker.sln"
}
```
