# Stage REVIEW: AI self-review before the human decides on the MR

**Goal:** review the branch critically, as a senior reviewer would, and give the human an honest summary for G3.
**Output:** `review.md`. Follow `.aidlc/templates/review.md`. You may fix real defects you find
(inside `allowed_files`, commit `[<ID>][REVIEW] <fix>`). The engine re-scans after your fixes.

## Steps
1. Run `python tools/aidlc/aidlc.py check <ID>` (the `self_check` command). It prints the guardrail
   scan: scope, protected paths, size limits, secrets and test integrity. Fix any violation.
2. Review the diff: `git diff <base>..HEAD` (the base is the commit the run started from; see
   `git merge-base main HEAD`).
3. Write `review.md`:
   - **Summary**: what was built, in 3–5 bullets.
   - **Acceptance criteria verification**: for each AC, whether it's met, with the evidence (file, test).
   - **Code quality**: naming, duplication, error handling, conventions from project.md.
   - **Security**: input validation, injection, authz assumptions, secrets, logging of personal data.
   - **Guardrail report**: paste the counts from `check` (files, lines, violations).
   - **Recommendation**: `APPROVE` or `CHANGES NEEDED`, with reasons. Be honest. The human decides.
4. Run `complete`. The engine writes `guardrail-report.json` and stops at gate G3.

Do not hide problems to get approval. Listing known issues is expected and valued.
