# LT-003 Employee screen for requesting leave: AI review

## Summary
- The app's start address (`/`) now shows the employee page. It is plain HTML, CSS and ES modules from `wwwroot/`, and the only server change is `UseDefaultFiles` plus `UseStaticFiles`.
- There is a "Who are you?" picker filled from `/api/employees`, remembered in `localStorage`. An unknown remembered id is forgotten silently, and blocked storage still works.
- The "Ask for leave" form (first and last day, type, optional reason) posts to the LT-002 API. On success it shows a confirmation, clears the form and refreshes the list. On refusal it shows the system's message beside the field it names, plus a summary, and moves focus to the first invalid field.
- The "My requests" table shows dates ("14 Dec 2026"), type, days, status as a word, and the manager's comment ("—" if none). The manager link is visible only for managers.
- Accessibility: every control is labelled, there are `role="alert"` / `role="status"` live regions and `aria-invalid` with `aria-describedby`, a 3px `:focus-visible` outline, and statuses are words with distinct border styles. Colour is never the only sign.

## Acceptance criteria verification
| AC | Met? | Evidence |
|---|---|---|
| AC1 | Yes | `Program.cs` static files. `EmployeePageTests.Start_address_serves_the_employee_page`, `Api_routes_still_work_alongside_static_files` |
| AC2 | Yes (markup automated; listing is manual M1) | `index.html` placeholder option and `fieldset disabled`. `EmployeePageTests.Page_has_picker_and_form_disabled_until_picked` |
| AC3 | Yes by design (manual M2 pending) | `identity.js` `rememberEmployeeId`/`loadPicker`. `EmployeePageTests.Identity_is_remembered_in_local_storage_safely` |
| AC4 | Yes by design (manual M3 pending) | `loadPicker` clears an unknown id with no error output |
| AC5 | Yes by design (manual M4 pending) | `employee.js` `onSubmit` (confirmation, `form.reset()`, `loadMyRequests()`). API: `AskForLeaveApiTests.Employee_asks_for_leave_and_it_is_pending` |
| AC6 | Yes by design (manual M5 pending) | `showErrors` maps `errors.lastDay` to the `#last-day-error` slot and keeps the values. API message from `AskForLeaveApiTests.Last_day_before_first_day_is_refused` |
| AC7 | Yes by design (manual M6 pending) | `api.js` (ProblemDetails `detail`/`errors`, network message) and the summary in `#form-errors` |
| AC8 | Yes (columns automated; rows manual M7) | `renderRows`. `EmployeePageTests.My_requests_table_has_all_columns`. API newest-first order from `MyAndTeamRequestsApiTests` |
| AC9 | Yes (markup automated; toggle manual M8) | `applyPerson` uses `person.isManager`. `EmployeePageTests.Manager_link_is_hidden_by_default` |
| AC10 | Yes (markup automated; keyboard run manual M9) | Native controls only, labels and focus style. `EmployeePageTests.Every_input_has_a_label`, `Stylesheet_has_visible_focus_style`, `Page_has_error_and_status_regions` |
| AC11 | Yes | `format.js` status words, and CSS dashed/solid/double borders. `EmployeePageTests.Status_is_rendered_as_text` |
| AC12 | Yes by design (manual M11 pending) | `onPick` → `applyPerson` (clear messages, toggle link, reload list) plus `listToken` against stale responses |

**Important:** AC3–AC9 and AC12 depend on browser behaviour that **no automated test runs**. The project has no JS or browser test
tooling, and my guardrails block running a browser or calling the running app. I checked the code by reading it, ran `node --check` on
every module, and smoke-tested `format.js`. **Please run the 11-step manual checklist in `test-report.md` before approving G3.**

## Code quality
1. **The JS is small and split by job:** `api.js` (HTTP plus one error shape), `identity.js` (picker plus remembered choice), `format.js`
   (display) and `employee.js` (page). `api.js`, `identity.js` and `format.js` have no page-specific code, so LT-004 can reuse them.
2. **No business rules in the UI.** The form uses `novalidate`, so even empty fields go to the API and its plain message is shown. This
   deviates slightly from the design's "browser blocks an empty send" convenience. It is noted in implementation-notes, and it keeps the
   messages consistent with the system's (spec Q3).
3. **Tests are structural.** `EmployeePageTests` uses regexes over the served HTML, CSS and JS. That is practical without a DOM library, but it is
   tied to markup details (for example the exact `<label for="…">` form). A markup refactor may need test updates. This is a known trade-off from
   having no JS tooling.
4. **Minor:** `fieldset:disabled { opacity: 0.6 }` lowers the contrast of the greyed-out form. WCAG exempts disabled controls, and the hint
   text outside the fieldset stays full contrast, but it could be raised if HR finds it hard to read.
5. **Minor:** the field-error prefix "Error: " comes from CSS `::before`. Most screen readers read it, but not all. The message text
   itself is always plain words, so nothing is lost if the prefix is skipped.
6. **Minor:** the manager link goes to a 404 until LT-004 is merged (accepted in spec Q1).

## Security
- **No injection from user data:** every reason, comment and name is inserted with `textContent` and `createElement`. A test fails if any
  JS file uses `innerHTML`, `outerHTML`, `insertAdjacentHTML` or `document.write`.
- **No third-party code:** no CDN, web fonts or external URLs. A test fails on any `http(s)://` in the page, CSS or JS.
- **Identity is still not authentication.** The picker lets anyone choose anyone, including a manager. This is the documented demo
  limitation, and the page itself says "Demo: there is no login." All permission rules (self-only asking, managers only) stay
  enforced by the API from LT-002. The UI only hides the manager link for convenience.
- **Stored in the browser:** only the employee id, in `localStorage`. No personal data, tokens or secrets.
- **No API surface added.** Static files are served only from `wwwroot`. API routes are unaffected (tested).

## Guardrail report
- Files changed: 8 · Lines changed: 892 · Violations: 0
- Tests: 114 passed, 0 failed, 0 skipped (re-run by the engine in TEST).

## Recommendation
**APPROVE, subject to the manual checklist.** The automated evidence (structure, accessibility markup, safety and the API behaviour underneath)
is complete, the guardrail scan is clean, and there are no new dependencies or API changes. The interactive acceptance criteria have only been
verified by code reading, so the human should run the 11 manual steps in `test-report.md` (about 3 minutes) before approving G3. If any step
fails, reject G3 with the step number and I'll fix it in a rework pass.
