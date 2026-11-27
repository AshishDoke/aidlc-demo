# LT-003 Employee screen for requesting leave: Design

## Context
- The API from LT-001 and LT-002 is merged and is everything this page needs:
  - `GET /api/employees` (anonymous: `id`, `name`, `email`, `isManager`, `managerId`, `managerName`)
  - `POST /api/leave-requests` (with `X-Employee-Id`, body `{firstDay, lastDay, type, reason}`) → 201 `LeaveRequestResponse`,
    400 `ValidationProblem` (`errors` per field, `detail`), 400 "Pick who you are …", 403 `detail`
  - `GET /api/me/leave-requests` (with `X-Employee-Id`) → `LeaveRequestResponse[]` newest first, with `daysCovered`, `status`
    (`pending`/`approved`/`rejected`) and `decisionComment`
- **There is no frontend yet.** `wwwroot/` doesn't exist, and `Program.cs` serves no static files. Opening `/` gives a 404 today.
- Tests: 97 xUnit tests through `TestAppFactory` (WebApplicationFactory, SQLite in-memory). No JavaScript tooling (project.md:
  "No npm, no build step").
- **No API change is needed.** LT-003 adds no business rules (spec Q3/Q7).

## Approach
1. **Serving the page:** in `Program.cs`, add `app.UseDefaultFiles()` and `app.UseStaticFiles()` before the endpoint mappings, so
   `/` serves `wwwroot/index.html` (AC1). API routes are unaffected (`/api/...` never matches a static file).
2. **One static page, `wwwroot/index.html`,** with plain semantic HTML. In order:
   - **Header:** app name; a labelled `<select id="who">` ("Who are you?") with a first option "— Pick who you are —"; and
     `<a id="manager-link" href="manager.html" hidden>Manager page</a>` (AC2, AC9, Q1).
   - **"Ask for leave" form:** a `<fieldset>` with labelled `<input type="date">` for first and last day, a `<select>` for type (first
     option "Choose a type", empty value, Q4), a `<textarea>` for an optional reason (`maxlength=500`), and a submit button. Each field
     has an error slot (`<p id="…-error">`) linked with `aria-describedby`. A summary `<div role="alert">` holds errors and a
     `<div role="status" aria-live="polite">` holds the confirmation (AC5–AC7, AC10, Q8).
   - **"My requests" section:** a `<table>` with a `<caption>` and column headers First day, Last day, Type, Days, Status, Manager's comment.
     An empty state "You have no leave requests yet." is shown in words (AC8).
3. **JavaScript (ES modules, no framework, no build step):**
   - `js/api.js` is a small `fetch` wrapper. It takes an optional `employeeId` (sets `X-Employee-Id`), parses JSON, and turns failures into
     one shape `{ status, message, fieldErrors }`: from `errors`/`detail` in ProblemDetails, or a network error → "Could not reach the
     leave tracker. Check that it is running and try again." LT-004 can reuse it.
   - `js/identity.js` loads employees, fills the picker, and remembers the choice in `localStorage` under the key
     `leaveTracker.employeeId`, with access wrapped in `try/catch` so a blocked storage still works without remembering.
     When the remembered id is not in the list, it clears it silently and nobody is picked (AC3, AC4, Q6). LT-004 can reuse it.
   - `js/format.js` turns `yyyy-MM-dd` into "14 Dec 2026", parsing the parts by hand to avoid time-zone shifts. It also gives a status word
     (Pending/Approved/Rejected), a type word, and "—" for an empty comment (Q2, Q5).
   - `js/employee.js` holds the page logic:
     - **Nobody picked:** the fieldset is disabled, with a note "Pick who you are to ask for leave" (AC2).
     - **On pick:** it saves the choice, toggles the manager link from `isManager`, clears messages, and loads "My requests" (AC9, AC12).
     - **On submit:** it disables the button while sending, so a double-click doesn't send twice, and POSTs.
       - On 201: it shows the confirmation ("Request sent: 14 Dec 2026 – 16 Dec 2026, annual, 3 days. It is pending until your
         manager decides."), resets the form, and reloads the list. The new request is first (newest first) (AC5).
       - On error: it shows each field's message next to that field (`aria-invalid="true"`), shows `detail` (or the network message) in the
         summary, keeps the typed values, and moves focus to the first invalid field, or else to the summary (AC6, AC7).
     - **The table** is built with `textContent` only, never `innerHTML`, so reasons and comments can never inject markup.
4. **Styling, `wwwroot/css/site.css`:** simple and clean, with a single column at most about 60rem wide, system fonts and dark text on light
   backgrounds (WCAG AA contrast). There is a visible `:focus-visible` outline (3px) on every interactive element (AC10). Status has a text
   label **and** a light background tint with a different border style per status. The colour only adds to the word (AC11). There is no
   media-query phone layout (stated as out of scope), but the table scrolls rather than breaking a narrow window.
5. **Quick checks in the browser are only for convenience** (Q3). The form uses `required` on the dates and type, so the browser blocks an
   obviously empty send. The API's message stays the deciding one. The page does **not** re-implement "last day before first day",
   so AC6 always shows the system's wording.

**Alternatives considered**
- *Testing the JavaScript with Node's built-in test runner:* rejected. project.md fixes the tests to `dotnet test` and allows no npm
  or other toolchain. The JS is kept thin, and its behaviour is covered by a short manual checklist.
- *Rendering the page on the server (Razor):* rejected. project.md fixes the frontend to static HTML, CSS and vanilla JS in `wwwroot/`.
- *A cookie instead of `localStorage` for the remembered choice:* rejected. A cookie would be sent to the server on every request for no
  reason, and the API reads identity only from the header.
- *Repeating the "last day before first day" check in JS:* rejected. It would duplicate a business rule and risk a different message
  from the system's (spec AC6).

## Affected files
| Path | Change |
|---|---|
| app/src/LeaveTracker.Api/Program.cs | modify (`UseDefaultFiles`, `UseStaticFiles`) |
| app/src/LeaveTracker.Api/wwwroot/index.html | create |
| app/src/LeaveTracker.Api/wwwroot/css/site.css | create |
| app/src/LeaveTracker.Api/wwwroot/js/api.js | create |
| app/src/LeaveTracker.Api/wwwroot/js/identity.js | create |
| app/src/LeaveTracker.Api/wwwroot/js/format.js | create |
| app/src/LeaveTracker.Api/wwwroot/js/employee.js | create |
| app/tests/LeaveTracker.Tests/EmployeePageTests.cs | create |

## Data
None. No schema, entity or seed change. The remembered choice lives only in the browser's `localStorage` (one key holding an employee id).

## API
No new or changed endpoints. The page uses:

| Method | Route | Request | Response | Codes |
|---|---|---|---|---|
| GET | / | — | `wwwroot/index.html` (text/html) | 200 |
| GET | /css/site.css, /js/*.js | — | static files (text/css, text/javascript) | 200 |
| GET | /api/employees | — | employee list (existing) | 200 |
| POST | /api/leave-requests | `X-Employee-Id` + `{firstDay, lastDay, type, reason}` | `LeaveRequestResponse` (existing) | 201, 400, 403 |
| GET | /api/me/leave-requests | `X-Employee-Id` | `LeaveRequestResponse[]` (existing) | 200, 400 |

The manager link points to `manager.html`, which returns 404 until LT-004 delivers it (spec Q1).

## Risks
| Risk | Mitigation |
|---|---|
| A stored reason or comment containing HTML or script (XSS) | All dynamic text is set with `textContent`. There is no `innerHTML` with data. A test asserts that the JS files never use `innerHTML`. |
| The remembered id now points to another person after a database reset (ids reused) | This is accepted for the demo. Ids are only stable per database. AC4 covers the id being gone. Re-seeding produces the same people in the same order, so the id usually maps to the same name. |
| `localStorage` blocked (private mode, browser policy) | Access is wrapped in `try/catch`. The page works and only forgets the choice. |
| Anyone can pick anyone (no login) | This is the known demo limitation (project.md, LT-002). The picker label says "Who are you?" and the README and MR repeat the limitation. |
| Static files shadowing API routes | Static files are only served from `wwwroot`, and nothing there is under `api/`. A test checks that `/api/health` still returns JSON. |
| The UI is not covered by automated browser tests | The tests check that the page and assets are served, that every input has a label, that there are no CDN or external URLs, the ARIA live regions, the hidden manager link and no `innerHTML`. Behaviour is covered by the manual checklist below, which a reviewer can do in about 3 minutes. |
| The browser date picker shows a different local format | Only the input display differs. The value sent is always ISO `yyyy-MM-dd`, and the list always shows "14 Dec 2026" (Q2). |
| New dependency | None. No NuGet change, no npm, no CDN, no web fonts. |

## Test strategy
Automated tests (`EmployeePageTests`, WebApplicationFactory) check the served page and assets. The API behaviour the page relies on
is already covered by LT-002's tests (`AskForLeaveApiTests`, `MyAndTeamRequestsApiTests`, `IdentityApiTests`). Interactive behaviour
is a manual check, because project.md rules out JS test tooling.

| AC | Test type | Test |
|---|---|---|
| AC1 | Integration | `EmployeePageTests.Start_address_serves_the_employee_page` (GET `/` → 200 text/html, contains the "Who are you?" picker); `Api_routes_still_work_alongside_static_files` |
| AC2 | Integration + manual | `EmployeePageTests.Page_has_picker_and_form_disabled_until_picked` (the picker has a placeholder option; the form fieldset starts `disabled`). Manual 1: the picker lists all 4 demo employees by name. |
| AC3 | Manual | Manual 2: pick Alex, reload the page, and Alex is still picked with his requests shown. |
| AC4 | Manual | Manual 3: in DevTools set `localStorage['leaveTracker.employeeId']='9999'`, then reload. Nobody is picked, the "Pick who you are" note shows, and there is no error message. |
| AC5 | Manual (API covered) | Manual 4: as Alex send 2026-12-14 → 2026-12-16, annual, "Trip". The confirmation shows, the form clears, and the top row reads 14 Dec 2026, 16 Dec 2026, Annual, 3, Pending. API: `AskForLeaveApiTests.Employee_asks_for_leave_and_it_is_pending`. |
| AC6 | Manual (API covered) | Manual 5: send last day before first day. The system's message appears beside the last-day field, the values are kept and no row is added. API: `AskForLeaveApiTests.Last_day_before_first_day_is_refused`. |
| AC7 | Integration + manual | `EmployeePageTests.Page_has_error_and_status_regions` (`role="alert"` and `role="status"` present, and each field has an error slot linked by `aria-describedby`). Manual 6: stop the server and send. The "Could not reach the leave tracker" message shows. |
| AC8 | Integration + manual | `EmployeePageTests.My_requests_table_has_all_columns` (caption and headers First day, Last day, Type, Days, Status, Manager's comment). Manual 7: as Sam, 2 rows (Rejected with the reason, Approved with "—"). As Maya, "You have no leave requests yet." |
| AC9 | Integration + manual | `EmployeePageTests.Manager_link_is_hidden_by_default` (`id="manager-link"` with `hidden`, `href="manager.html"`). Manual 8: Maya shows the link, Alex hides it, and switching updates it straight away. |
| AC10 | Integration + manual | `EmployeePageTests.Every_input_has_a_label` (every `input`/`select`/`textarea` id has a matching `<label for>`); `Stylesheet_has_visible_focus_style` (`:focus-visible` present). Manual 9: complete steps 2, 4 and 8 using only the keyboard. |
| AC11 | Integration + manual | `EmployeePageTests.Status_is_rendered_as_text` (`format.js` maps every status to a word). Manual 10: check that statuses read as words, and in DevTools "Emulate vision deficiencies: achromatopsia" they are still distinguishable. |
| AC12 | Manual | Manual 11: switch from Sam to Alex. The list, the link and the form messages all change straight away. |
| Safety | Integration | `EmployeePageTests.Frontend_uses_no_external_urls_or_innerHTML` (no `http(s)://` in the HTML, CSS or JS; no `innerHTML` in the JS); `Static_assets_are_served_with_correct_types` |

The manual checklist (steps 1–11 above) will be repeated in the test report, so the human can run it in about 3 minutes at G3.
