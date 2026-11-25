# Stage DESIGN: specification → technical design

**Goal:** decide *how* to satisfy the approved spec, for a tech lead to approve at G2.
**Output:** `design.md`. Follow `.aidlc/templates/design.md`.
**Do not** change any repository file in this stage.

## Steps
1. Read the approved `spec.md`, which is bound to G1. Do not edit it: editing revokes G1.
2. Read `.aidlc/context/project.md` and explore the existing code under `app/`.
3. Write `design.md`:
   - **Context**: what exists today that this change touches.
   - **Approach**: the chosen solution, plus 1–2 alternatives you rejected and why.
   - **Affected files**: an exact list of files to create or modify (paths relative to the repo root).
     This becomes the scope fence in PLAN.
   - **Data**: schema/entity changes, migrations and seed data. Write "None" if not applicable.
   - **API**: endpoints, request/response shapes and status codes. Write "None" if not applicable.
   - **Risks**: technical, security and data risks, each with a mitigation.
   - **Test strategy**: which tests prove which ACs (unit / integration / manual UI check).
4. If `feedback.md` exists, address every point.

## Quality bar
- Follow the conventions in project.md. Do not introduce new frameworks or packages unless the spec
  needs them. If you must add one, list it under Risks as **"New dependency: <name> <version>, reason"**
  so the human sees it at G2.
- Keep the change as small as possible. Do not refactor unrelated code.
