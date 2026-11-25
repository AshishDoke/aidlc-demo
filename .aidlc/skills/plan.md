# Stage PLAN: design → executable task plan

**Goal:** break the approved design into small, ordered tasks, and declare the exact files the AI may touch.
**Output:** `plan.md`. Follow `.aidlc/templates/plan.md`.
**Do not** change any repository file in this stage.

## Steps
1. Read `spec.md` (approved at G1) and `design.md` (approved at G2).
2. Write a **Tasks** section in human-readable form: T1, T2, ... Each task is one commit's worth of work
   (roughly 30–200 lines) and states which ACs it covers.
3. Write a **Plan data** section containing exactly one fenced block tagged `aidlc-plan` with valid JSON:

```aidlc-plan
{
  "tasks": [
    {"id": "T1", "title": "Create solution and API project", "covers": ["AC1"]},
    {"id": "T2", "title": "Add entities and DbContext", "covers": ["AC2", "AC3"]}
  ],
  "allowed_files": [
    "app/LeaveTracker.sln",
    "app/src/LeaveTracker.Api/**",
    "app/tests/LeaveTracker.Tests/**"
  ],
  "test_command": "dotnet test app/LeaveTracker.sln"
}
```

## Rules checked by the engine
- Task ids are `T1`, `T2`, ...
- Every `ACn` in spec.md is covered by at least one task.
- `allowed_files` is the **scope fence**. Edits outside it are blocked or rejected. It must match
  design.md "Affected files", plus test files. It must not be `**`, must not use `..`, and must not
  include protected paths such as `.aidlc/`, `tools/`, `.github/`, `.claude/` or `requirements/`.
- `test_command` is optional. When given, it overrides the default in `.aidlc/config.json`.
- At most 12 tasks. If you need more, the requirement is too big: say so in the plan and keep it small.
