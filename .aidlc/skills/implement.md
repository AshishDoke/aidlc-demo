# Stage IMPLEMENT: write the code, one task per commit

**Goal:** implement every task in `plan.md`, staying inside `allowed_files`.
**Output:** code commits on the run branch, plus `implementation-notes.md`.

## Steps
1. Use the `tasks_remaining` list from the engine's `next` output. Tasks in `tasks_done` are finished
   (resume-safe), so do not redo them.
2. For each remaining task, in order:
   a. Implement it. Touch only files that match `allowed_files`.
   b. Build locally if you can (`dotnet build app/LeaveTracker.sln`) and fix compile errors.
   c. Commit exactly that task:
      `git add <files>` then `git commit -m "[<ID>][T<n>] <short summary>"`.
      The `[<ID>][T<n>]` tag is how the engine tracks progress and resumes.
   d. Append an entry to `implementation-notes.md`: the task id, what changed, and anything surprising.
3. When every task is committed, run `complete`.

## Rules
- Write tests alongside the code where the design says so. The TEST stage adds the rest.
- No new NuGet/npm packages unless listed in the approved design.md.
- No secrets, connection strings with passwords, or real personal data. Use config and placeholders.
- Do not edit `spec.md`, `design.md` or `plan.md`. Editing them revokes approvals.
- If a task turns out to be impossible within the approved plan, stop and tell the user. Do not
  silently expand scope. The human may reject and re-plan.
- Never amend or rewrite earlier commits.
