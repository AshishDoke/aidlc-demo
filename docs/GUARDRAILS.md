# Guardrails and where each one is enforced

Legend: **E** = engine at `complete` · **H** = Claude PreToolUse hook (`guard`) · **P** = Claude `permissions.deny` ·
**V** = `verify` (pre-push + GitLab CI) · **S** = skill instruction only

| # | Guardrail | Enforcement | Config |
|---|---|---|---|
| 1 | AI cannot approve, reject, unblock, unlock or halt | engine TTY check + `CLAUDECODE` refusal, **P**, **H** (bash patterns), **V** (approvals must be `human:` events) | `deny_bash_patterns` |
| 2 | Only the engine writes state, logs, approvals and records | **H**, **V** (hash chain + bound hashes) | `protected_paths` |
| 3 | Approved artifacts can't change silently | engine `revalidate()` on every `next`, **V** | n/a |
| 4 | No commits after G3 except the seal | **E** (MR stage), **V** | n/a |
| 5 | Scope fence: only the plan's `allowed_files` | **H** (Claude), **E** (git diff), **V** | plan `allowed_files` |
| 6 | Governance files are protected | **E**, **H**, **V**, CODEOWNERS | `protected_paths` |
| 7 | Plan can't grant `**`, `..`, or protected paths | **E** (PLAN) | n/a |
| 8 | Every AC is planned, tested and reported | **E** (PLAN coverage, TEST report) | n/a |
| 9 | Tests are executed by the engine, not trusted from the AI | **E** (TEST runs `test_command`, logs the output) | `test_command` |
| 10 | Test integrity: no skips, no deleted tests, no `Assert.True(true)` | **E**, **V** | `test_skip_patterns`, `test_file_globs` |
| 11 | Secret scanning on added lines | **E**, **V** | `secret_patterns` |
| 12 | Secrets redacted from the audit log | engine `redact()` | `redact_patterns` |
| 13 | Blast radius: max files / lines / tasks | **E**, **V** | `limits` |
| 14 | Retry budget → BLOCKED, human unblock | **E** | `budgets` |
| 15 | Kill switch | `HALT` file checked by `next` / `complete` | `aidlc.py halt` |
| 16 | One agent per run | lock with expiry | `lock_minutes` |
| 17 | Dependencies must be merged first | engine at intake (`depends_on`) | requirement front matter |
| 18 | Requirement changed after intake → restart at ANALYZE | engine `check_source_drift()` | n/a |
| 19 | Git safety: no merge/rebase/amend/force/config/`--no-verify`, no push to main | **H**, **P**, **S** | `deny_bash_patterns` |
| 20 | No outbound network from the shell (exfiltration) | **H**, **P** | `deny_bash_patterns` |
| 21 | Prompt injection: requirement text is data | **S** (orchestrator, AGENTS.md), intake is verbatim (engine) | n/a |
| 22 | New dependencies need G2 visibility | **S** (design must list them), project.md allow-list | n/a |
| 23 | Tamper-evident trail | SHA-256 hash chain, verified in **V** | n/a |
| 24 | Everything an AI did is attributable | audit `actor` = client/model. Claude tool calls logged via the PostToolUse hook | n/a |

## Changing a guardrail
Guardrail files are protected paths. Change them by hand in a normal (non-AIDLC) MR, reviewed by the CODEOWNERS
`@aidlc-owners`.
