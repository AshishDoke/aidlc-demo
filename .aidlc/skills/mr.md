# Stage MR: prepare the merge request

**Goal:** produce the MR description, then let the engine seal the run.
**Output:** `mr.md`. Follow `.aidlc/templates/mr.md`.
**Do not** create new commits in this stage. HEAD must stay exactly where the human approved it at G3.

## Steps
1. Write `mr.md`:
   - **Summary**: one paragraph a reviewer can read in 20 seconds.
   - **Changes**: grouped by area (database / API / UI / tests).
   - **Acceptance criteria**: a checklist, `- [x] AC1 ... (evidence)`.
   - **Testing**: how it was tested, plus manual verification steps.
   - **AI disclosure**: which assistant(s) and model(s) worked on which stages (see the audit log),
     the gates and approvers (from `approvals.json`), and the statement
     "Generated with AIDLC. All gates approved by humans. AI did not merge."
2. Run `complete`. The engine copies the whole run (state, audit log, approvals, artifacts) into
   `.aidlc/records/<ID>/` and commits it as the final commit of the branch.
3. Relay the engine's final `message` to the user. It says how to push the branch and open the MR.
   - If `.aidlc/config.json` has `mr.ai_may_push_feature_branch: true`, you may run exactly the push
     and `glab mr create` commands from the message. Otherwise the human does it.
   - **Never merge.** Merging is always a human action in GitLab.
