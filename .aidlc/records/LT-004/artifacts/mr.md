# LT-004: Manager screen for deciding on leave

## Summary
This adds the manager page at `/manager.html`, where the employee page's "Manager page" link already pointed (the link no longer gives
a 404).
- **Picking who you are:** managers use the same "Who are you?" picker as the employee page, and the choice is shared between both pages.
- **What a manager sees:** how many team requests are waiting, and a list of them (name, dates, type, days, reason). Non-managers get a
  friendly "this page is for managers" message instead.
- **Deciding:** each request can be approved (optional comment) or rejected (reason required) in a small inline panel with Confirm
  and Cancel. Afterwards the row disappears, the count drops, and a confirmation is announced.
- **Accessibility:** everything works with the keyboard, and nothing relies on colour alone.

It is plain HTML, CSS and an ES module, reusing LT-003's helpers unchanged. There is no API, database or package change.
**One known minor issue is accepted** (see "Known issues").

## Changes
- **Database:** None.
- **API:** None. The page uses the existing LT-002 routes `GET /api/team/leave-requests/pending`,
  `POST /api/leave-requests/{id}/approve` and `POST /api/leave-requests/{id}/reject`.
- **UI:**
  - `wwwroot/manager.html`:
    - the shared picker and an "Employee page" link
    - the pick-first and not-a-manager messages
    - the count heading, status and alert regions, and the team table (Employee, First day, Last day, Type, Days, Reason, Decide)
    - a `<template>` for the decision panel: a labelled textarea, error slot, Confirm and Cancel
  - `wwwroot/js/manager.js`: page behaviour.
    - Uses `api.js`, `identity.js` and `format.js` from LT-003, and inserts text with `textContent` only.
    - One decision panel open at a time, with Escape to cancel and a double-send guard.
    - Focus moves to the next row after a decision.
    - On 403, 404 or 409 ("already decided") it shows the message and reloads.
  - `wwwroot/css/site.css`: manager page rules appended only (an outlined Reject/Cancel style and the decision panel). The employee page
    is unchanged.
- **Tests:** 18 new `ManagerPageTests`.
  - **Structural (15):** page served at the LT-003 link, back link, shared picker and identity, hidden states, table columns, labelled
    panel, live regions, words not colour, script wiring, no external URLs or `innerHTML`.
  - **API flow (3):** approve removes from the pending list, reject needs a reason, deciding twice gives "already approved by Maya Manager".
  - **Total:** 132 tests.

## Acceptance criteria
- [x] AC1: the manager page is shown from the employee page's link, with a way back (`Manager_page_is_served_where_the_employee_page_links`, `Manager_page_links_back_to_the_employee_page`)
- [x] AC2: same picker, choice shared with the employee page (`Manager_page_has_the_shared_picker`, `Manager_script_shares_the_remembered_choice`, manual M1–M2)
- [x] AC3: nobody picked → asks to pick, no list (`Manager_page_shows_pick_first_and_hides_team_section_by_default`, manual M3)
- [x] AC4: non-manager → friendly message, no list or actions (`Not_manager_message_is_present_and_hidden_by_default`, API `Non_manager_cannot_see_team_requests`, manual M4)
- [x] AC5: waiting count in words, and "Nothing is waiting" (`Team_table_has_all_columns`, `Decide_buttons_are_words`, manual M5)
- [x] AC6: team pending list with name, dates, type, days, reason, earliest first (`Approving_removes_the_request_from_the_team_list`, API `Team_pending_shows_only_own_direct_reports`, manual M6)
- [x] AC7: approve (optional comment) → row gone, count down, confirmation (`Approving_removes_the_request_from_the_team_list`, manual M7)
- [x] AC8: reject needs a reason, shown beside the field, then row gone (`Rejecting_needs_a_reason_and_then_removes_the_request`, `Decision_panel_has_labelled_text_and_error_slot`, manual M8)
- [x] AC9: already decided elsewhere → system message + refreshed list (`Deciding_twice_gives_the_already_decided_message`, `Page_script_uses_the_expected_api_routes`, manual M9)
- [x] AC10: cancel decides nothing, and the same decision can't be sent twice (`Page_script_uses_the_expected_api_routes`, manual M10). **See known issue 1:** opening *another* row while sending loses the first result on screen.
- [x] AC11: keyboard-only use, labels, visible focus, announced messages, sensible focus after a decision (`Every_input_has_a_label`, `Page_has_status_and_alert_regions`, `Stylesheet_has_manager_styles_and_visible_focus`, manual M11)
- [x] AC12: count, messages and Approve vs Reject readable as words (`Decide_buttons_are_words`, manual M12)
- [x] AC13: switching person updates everything straight away (manual M13)

**The interactive checks (M1–M13 in `test-report.md`) were not run by the AI**, because the project has no browser test tooling and the
guardrails block driving a browser or calling the running app. Steps for running them were given to the human before G3, and G3 was
approved. Whether each check was run, and its result, isn't recorded in the run. **Reviewers: please run the checklist (about 4 minutes)
if you haven't.**

**Known issues and follow-ups:**
1. **Accepted at G3 (review finding 1).** If the manager opens another row's Approve or Reject while a decision is still being sent,
   the first decision is saved but its row stays on screen with no confirmation. The data is never wrong: deciding that row again
   shows "already approved" and reloads.
   - **The fix:** add `if (sending) { return; }` at the start of `openDecision` in `manager.js`, plus a test assertion.
   - **When:** in LT-005 or a small follow-up.
   - **Why it isn't fixed here:** the G3 rework returned to REVIEW, where the guardrails don't allow code changes.
2. **Engine gap (for the AIDLC maintainers):** rejecting G3 returns to REVIEW, where code cannot be changed, so review findings can't be
   fixed in a rework pass.
3. **Process note:** this run's branch was first created from a local `main` without LT-003. The human fast-forwarded it to `eae01c5`
   and set the run's recorded `base_sha` to match, so the scope scan counts only this run's 4 files. The original base is still visible
   in the audit log.
4. **Requirement front matter:** `requirements/LT-004.md` still lists only LT-002 under `depends_on`, though the page relies on LT-003.
   A human should update it.
5. **Still open:**
   - The `show`, `setText` and `el` helpers are duplicated (about 10 lines) between `employee.js` and `manager.js`.
   - The LT-003 follow-ups remain: the greyed-out form contrast, and a plain message for an empty or malformed POST body.
   - The demo identity is not a login. Anyone can pick a manager; the API enforces every rule.

## Testing
- Automated: `dotnet test app/LeaveTracker.sln` → `Passed! - Failed: 0, Passed: 132, Skipped: 0, Total: 132`. It was re-run independently
  by the engine in the TEST stage (output in `test-output.log`). `manager.js` was also checked with `node --check` locally.
- Manual:
  1. Run `dotnet run --project app/src/LeaveTracker.Api` (optionally delete `leave.db` first) and open the printed URL plus
     `/manager.html`.
  2. Follow M1–M13 in `.aidlc/records/LT-004/artifacts/test-report.md`. In short:
     - shared picker, nobody picked, Alex as a non-manager, Maya's count and list
     - approve with a comment and check it on Alex's employee page, reject without and then with a reason
     - decide in two tabs, Cancel and Escape, a double-click on Confirm
     - keyboard only, achromatopsia emulation, switching person mid-flow

## AI disclosure
| Stage | Performed by |
|---|---|
| INTAKE | engine |
| ANALYZE | ai:claude-code (claude-opus-5-5) |
| DESIGN | ai:claude-code (claude-opus-5-5) |
| PLAN | ai:claude-code (claude-opus-5-5) |
| IMPLEMENT | ai:claude-code (claude-opus-5-5) |
| TEST | ai:claude-code (claude-opus-5-5) |
| REVIEW (and G3 rework) | ai:claude-code (claude-opus-5-5) |
| MR | ai:claude-code (claude-opus-5-5) |

| Gate | Approved by | When |
|---|---|---|
| G1 (Specification) | Ashish Doke | 2026-11-28T14:02:52Z |
| G2 (Design) | Ashish Doke | 2026-11-28T14:26:29Z |
| G3 (Raise MR) | Ashish Doke | 2026-11-28T14:55:43Z (bound to HEAD a4e596e). It was rejected once before ("Fix review finding 1"), then approved with the finding accepted as a known issue. |

Generated with AIDLC. All gates approved by humans. AI did not merge.
Full trace: `.aidlc/records/LT-004/`

🤖 Generated with [Claude Code](https://claude.com/claude-code)
