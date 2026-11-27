# LT-003 Employee screen for requesting leave: Implementation notes

## Summary
Added the employee page at `/`: a "Who are you?" picker remembered in the browser, an "Ask for leave" form, and a "My requests"
table. It is plain HTML, CSS and ES modules in `wwwroot/`, with no framework, build step, npm or CDN. The only server change is
`UseDefaultFiles` plus `UseStaticFiles` in `Program.cs`. No API, schema or package changes. 114 tests pass (97 from LT-001/002 plus
17 new page tests). The JS modules were also syntax-checked locally with `node --check` and the formatting helpers smoke-tested.
That was a one-off local check only: no Node tooling was added to the project.

## Tasks
### T1: Serve the employee page: HTML structure, styles and page tests
- Commit: 97c1396
- Changes: `Program.cs` (`UseDefaultFiles`, `UseStaticFiles` before the endpoint mappings), `wwwroot/index.html`, `wwwroot/css/site.css`,
  and `EmployeePageTests` (start address, API alongside static files, picker and disabled form, labels, live regions, table columns,
  hidden manager link, focus style).
- Notes: The form has `novalidate`, so the browser does **not** block an empty send. The design mentioned `required` as a convenience
  block, but with `novalidate` the API's own plain messages ("First day is required.") are always the ones shown, which matches spec
  Q3/AC6–AC7 ("the system's message is the deciding one") more closely. `required` is still in the markup, so assistive technology
  announces the fields as required. Status styles differ by border style (dashed/solid/double) as well as colour.

### T2: Shared front-end helpers: API wrapper, remembered identity, formatting
- Commit: c39e56b
- Changes: `js/api.js` (`api()` and `ApiError {status, message, fieldErrors}`, plus a network message), `js/identity.js` (`loadPicker`,
  `rememberedEmployeeId`/`rememberEmployeeId` guarded with try/catch, and an unknown id forgotten silently), `js/format.js` ("14 Dec 2026",
  status and type words, "—"). `EmployeePageTests` gained asset content types, status as text and guarded storage.
- Notes: Managers appear in the picker as "Maya Manager (manager)", which helps people find the manager-only link. A first version of
  the storage-guard test used a hard-to-read regex, and it was simplified before committing.

### T3: Employee page behaviour: send request, show results and errors, switch person
- Commit: 88eeb5b
- Changes: `js/employee.js`. Nobody picked → disabled fieldset and hint. On pick → remember, toggle the manager link, clear messages and load
  the list. On submit → disable the button ("Sending…"), POST, then a confirmation + reset + reload + focus on the first day, or field errors
  (`aria-invalid`, slot text) + summary + focus on the first invalid field. The table is built with `textContent`, and a list token ignores
  stale responses after a quick switch. `EmployeePageTests` gained no external URLs / no `innerHTML`, and the script wiring.
- Notes: The confirmation stays until the next send or switch (Q8). Errors from fields the form doesn't have (for example `employeeId`) go into the
  summary.
