# Review notes and known limitations

Written for reviewers (Codex, Copilot, humans). These are deliberate trade-offs or open gaps. Please challenge them.

## Trust boundary
1. **Local approvals are deterrents, not proof.** The agent runs as the same OS user as the human. The engine
   refuses human-only commands inside Claude Code (`CLAUDECODE` env) and without a TTY, and Claude's permissions deny
   them. But a determined process on the same machine could forge `approvals.json` and recompute the hash chain.
   **Enterprise fix:** source approvals from GitLab MR approvals or Jira transitions (server-side identity), and have
   CI `verify` check those, not the local file. The `do_decision()` function is the single place to swap in.
2. **The hash chain is tamper-evident, not tamper-proof.** Anyone with write access can rebuild the whole chain.
   Fix: anchor the last hash externally (a signed commit, a CI artifact, or a GitLab MR note) at every gate.
3. **Copilot has no pre-edit hook here.** Scope violations are caught at `complete` and in CI, not prevented. If
   your VS Code version supports agent hooks, wire them to `aidlc.py guard` the same way as Claude.
4. **The Bash deny patterns are best-effort regexes.** Obfuscated commands can evade them. They are defence in depth,
   and `verify` in CI is the authoritative check.

## Functional gaps
5. Jira intake is implemented but disabled and untested against a live instance (no `depends_on` from issue links).
6. MR creation (`glab mr create`) is left to the human by default (`mr.ai_may_push_feature_branch: false`).
7. There are no token or cost budgets. Clients don't report usage to the engine. Only failed-attempt budgets exist.
8. The separation of duties check (requester ≠ approver) isn't enforced locally, because both are the same person
   in the demo.
9. `.vscode/settings.json` terminal auto-approve keys follow the current Copilot feature. Verify them against your
   VS Code version. The prompt file front matter uses `agent: agent` (older versions use `mode: agent`).
10. Claude hook commands use `python "$CLAUDE_PROJECT_DIR/..."`. On Windows, if hooks fail to start, change `python`
    to `py`, or use an absolute path.
11. Sealed records include `test-output.log`. On large suites, consider truncating it or storing it as a CI artifact.

## How to verify the engine
```
python -m unittest discover -s tools/aidlc/tests -v
```
13 tests cover: the full lifecycle to a sealed record plus CI verify, approval revocation on edit, rejection
feedback, the BLOCKED budget, scope violation, secret and skipped-test detection, failing tests, audit tampering,
guard decisions, refusing approval inside an agent session, the kill switch, the dependency gate, and requirement
source drift.
