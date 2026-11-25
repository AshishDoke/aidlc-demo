# Stage TEST: prove every acceptance criterion

**Goal:** make sure every `ACn` has automated evidence, and that the whole suite passes.
**Output:** test commits (if needed) plus `test-report.md`.

## Steps
1. Map each AC in `spec.md` to existing tests. For any AC without a test, add one, inside
   `allowed_files`. Commit with `[<ID>][TEST] <summary>`.
2. Run the test command yourself (`test_command` from the engine output) and fix failures in the
   **code**, not by weakening tests.
3. Write `test-report.md`:
   - **Summary**: number of tests and the pass/fail result.
   - **Acceptance criteria evidence**: a table with one row per AC: `| AC1 | TestClass.TestName | pass |`.
     For UI-only behaviour, give a manual check the human can do in under a minute.
   - **Test results**: paste the final summary line(s) from the test run.
4. Run `complete`. **The engine runs the test command again itself**, saves the output to
   `test-output.log`, and fails the stage on a non-zero exit. Your report is not trusted on its own.

## Forbidden (detected automatically)
`[Fact(Skip=...)]`, `[Ignore]`, `Assert.True(true)`, `it.skip`, `xit`, deleting test files, or
lowering assertions just to make tests pass.
