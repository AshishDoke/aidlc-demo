# LT-003: Employee screen for requesting leave

## Summary
This adds the app's first screen. Opening `/` shows the employee page, where people pick who they are (remembered in the browser), ask for
leave with a short form, and see "My requests" with dates, type, days, status and the manager's comment. Refusals from the system are
shown beside the field they concern, and a confirmation appears after a successful send. Managers see a link to the manager page (LT-004).
Everything works with the keyboard, and status is always a word, never only a colour. It is plain HTML, CSS and ES modules, with no
framework, build step, CDN, API change or new package.

## Changes
- **Database:** None.
- **API:** None. `Program.cs` adds `UseDefaultFiles()` and `UseStaticFiles()`, so `/` serves `wwwroot/index.html`. The page uses the existing
  `GET /api/employees`, `POST /api/leave-requests` and `GET /api/me/leave-requests`.
- **UI:**
  - `wwwroot/index.html`: the picker, the form with labelled fields, error slots and live regions, the "My requests" table, and a hidden manager link.
  - `wwwroot/css/site.css`: a clean single column, a 3px focus outline, and status styles that differ by word and border style.
  - `wwwroot/js/api.js`: a `fetch` wrapper with one error shape. It can be reused by LT-004.
  - `wwwroot/js/identity.js`: the picker and the remembered choice, with safe `localStorage` access. It can be reused by LT-004.
  - `wwwroot/js/format.js`: "14 Dec 2026", status and type words, and "—" for an empty comment.
  - `wwwroot/js/employee.js`: page behaviour. It inserts text with `textContent` only.
- **Tests:** 17 new `EmployeePageTests` (page served, API unaffected, labels, live regions, table columns, hidden manager link, focus
  style, status as text, guarded storage, asset types, script wiring, no external URLs or `innerHTML`). 114 in total.

## Acceptance criteria
- [x] AC1: `/` shows the employee page, with the picker at the top (`EmployeePageTests.Start_address_serves_the_employee_page`)
- [x] AC2: The picker lists employees; the form is disabled until someone is picked (`Page_has_picker_and_form_disabled_until_picked`, manual M1)
- [x] AC3: The choice is remembered in this browser (`Identity_is_remembered_in_local_storage_safely`, manual M2)
- [x] AC4: An unknown remembered person is forgotten silently (`Identity_is_remembered_in_local_storage_safely`, manual M3)
- [x] AC5: Send → confirmation, form cleared, new request at the top (`Page_script_uses_the_expected_api_routes`, API `AskForLeaveApiTests`, manual M4)
- [x] AC6: "Last day before first day" shown beside Last day, values kept (`Page_has_error_and_status_regions`, manual M5)
- [x] AC7: Other refusals and an unreachable server are shown clearly (`api.js`, manual M6)
- [x] AC8: "My requests" with all columns, newest first, and an empty state in words (`My_requests_table_has_all_columns`, manual M7)
- [x] AC9: Manager link only for managers (`Manager_link_is_hidden_by_default`, manual M8)
- [x] AC10: Keyboard-only use, labelled inputs, visible focus, announced messages (`Every_input_has_a_label`, `Stylesheet_has_visible_focus_style`, manual M9)
- [x] AC11: Status as words; colour is extra (`Status_is_rendered_as_text`, manual M10)
- [x] AC12: Switching person updates everything straight away (manual M11)

**The interactive checks (M1–M11 in `test-report.md`) were not run by the AI**, because the project has no browser test tooling and the guardrails
block browser and HTTP tools. They were left for the human before G3, and G3 was approved. The result of each check isn't recorded in the run.
**Reviewers: please run the checklist (about 3 minutes) if you haven't.**

**Known follow-ups:**
1. The manager link leads to a 404 until LT-004 is merged (spec Q1).
2. The greyed-out form (`fieldset:disabled`, opacity 0.6) has lower contrast. Disabled controls are exempt from WCAG, but this could be raised if needed.
3. The LT-002 follow-up (plain message for an empty or malformed POST body) remains open. The page always sends a proper body.
4. The demo limitation remains: there is no login, and anyone can pick anyone. The page says so.

## Testing
- Automated: `dotnet test app/LeaveTracker.sln` → `Passed! - Failed: 0, Passed: 114, Skipped: 0, Total: 114`
  (re-run independently by the engine in the TEST stage; the output is in `test-output.log`). The JS modules were also checked with `node --check` locally.
- Manual: run `dotnet run --project app/src/LeaveTracker.Api` (optionally delete `leave.db` first) and open the printed URL. Then follow M1–M11
  in `.aidlc/records/LT-003/artifacts/test-report.md`: pick a person, reload, send a valid request and an invalid one, look at Sam's list,
  look for the manager link as Maya and as Alex, repeat with the keyboard only, and use DevTools' achromatopsia emulation.

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
| G1 (Specification) | Ashish Doke | 2026-11-27T10:59:30Z |
| G2 (Design) | Ashish Doke | 2026-11-27T11:05:38Z |
| G3 (Raise MR) | Ashish Doke | 2026-11-27T11:28:06Z (bound to HEAD 88eeb5b) |

Generated with AIDLC. All gates approved by humans. AI did not merge.
Full trace: `.aidlc/records/LT-003/`
