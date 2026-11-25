"""
End-to-end tests for aidlc.py. Standard library only:

    python -m unittest discover -s tools/aidlc/tests -v

Each test builds a throwaway git repo, copies the real config/policies, and
drives a requirement through the lifecycle exactly as an AI assistant would,
with human approvals simulated through the engine's internal API.
"""
import importlib.util
import io
import json
import os
import shutil
import subprocess
import sys
import tempfile
import unittest
from contextlib import redirect_stdout
from pathlib import Path

REPO = Path(__file__).resolve().parents[3]
spec = importlib.util.spec_from_file_location("aidlc", REPO / "tools" / "aidlc" / "aidlc.py")
aidlc = importlib.util.module_from_spec(spec)
spec.loader.exec_module(aidlc)

REQ = "LT-901"
REQUIREMENT = """---
id: LT-901
title: Hello endpoint
depends_on: []
---
# Hello endpoint
Add a hello file.
"""

SPEC = """# LT-901 Hello
## Overview
x
## Requirement
x
## Scope
x
## Acceptance criteria
- AC1: hello.txt exists
- AC2: hello.txt says hello
## Out of scope
x
## Open questions
none
"""
DESIGN = "## Context\n## Approach\n## Affected files\nsrc/hello.txt\n## Data\n## Risks\n## Test strategy\n"
PLAN = """## Tasks
- T1 create file
## Plan data
```aidlc-plan
{"tasks": [{"id": "T1", "title": "create hello", "covers": ["AC1", "AC2"]}],
 "allowed_files": ["src/**"],
 "test_command": "python -c \\"import sys; sys.exit(0)\\""}
```
"""


def git(cwd, *args):
    return subprocess.run(["git", *args], cwd=cwd, check=True, capture_output=True, text=True).stdout.strip()


class Base(unittest.TestCase):
    def setUp(self):
        self.tmp = Path(tempfile.mkdtemp(prefix="aidlc-test-"))
        for rel in (".aidlc/config.json", ".aidlc/policies/guardrails.json", ".gitignore", ".gitattributes"):
            dst = self.tmp / rel
            dst.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy(REPO / rel, dst)
        (self.tmp / ".aidlc/runs").mkdir(parents=True, exist_ok=True)
        (self.tmp / ".aidlc/runs/.gitkeep").write_text("")
        (self.tmp / "requirements").mkdir()
        (self.tmp / "requirements" / f"{REQ}.md").write_text(REQUIREMENT, encoding="utf-8")
        git(self.tmp, "init", "-q", "-b", "main")
        git(self.tmp, "config", "user.name", "Tester")
        git(self.tmp, "config", "user.email", "t@example.com")
        git(self.tmp, "add", "-A")
        git(self.tmp, "commit", "-q", "-m", "init")
        os.environ["AIDLC_ROOT"] = str(self.tmp)
        os.environ.pop("CLAUDECODE", None)

    def tearDown(self):
        os.environ.pop("AIDLC_ROOT", None)
        shutil.rmtree(self.tmp, ignore_errors=True)

    # --- helpers -----------------------------------------------------------
    def run_cli(self, *argv):
        buf = io.StringIO()
        with redirect_stdout(buf):
            code = aidlc.main(list(argv))
        out = buf.getvalue()
        try:
            return code, json.loads(out)
        except json.JSONDecodeError:
            return code, out

    def art(self, name):
        return self.tmp / ".aidlc" / "runs" / REQ / "artifacts" / name

    def write_art(self, name, text):
        self.art(name).write_text(text, encoding="utf-8")

    def approve(self, gate):
        aidlc.do_decision(REQ, gate, "APPROVED", ask=lambda _: "APPROVE", interactive=False)

    def reject(self, gate, comment):
        aidlc.do_decision(REQ, gate, "REJECTED", comment, ask=lambda _: "REJECT", interactive=False)

    def complete(self, stage):
        return self.run_cli("complete", REQ, "--stage", stage, "--client", "test")

    def to_implement(self):
        self.run_cli("next", REQ, "--client", "test")
        self.write_art("spec.md", SPEC)
        self.complete("ANALYZE")
        self.approve("G1")
        self.run_cli("next", REQ, "--client", "test")
        self.write_art("design.md", DESIGN)
        self.complete("DESIGN")
        self.approve("G2")
        self.run_cli("next", REQ, "--client", "test")
        self.write_art("plan.md", PLAN)
        code, out = self.complete("PLAN")
        self.assertEqual(code, 0, out)
        self.run_cli("next", REQ, "--client", "test")

    def implement(self):
        (self.tmp / "src").mkdir(exist_ok=True)
        (self.tmp / "src" / "hello.txt").write_text("hello\n")
        git(self.tmp, "add", "src/hello.txt")
        git(self.tmp, "commit", "-q", "-m", f"[{REQ}][T1] create hello")
        self.write_art("implementation-notes.md", "## Summary\nok\n## Tasks\n- T1 done\n")


class HappyPath(Base):
    def test_full_lifecycle_to_sealed_record_and_ci_verify(self):
        code, out = self.run_cli("next", REQ, "--client", "test")
        self.assertEqual(out["action"], "DO_STAGE")
        self.assertEqual(out["stage"], "ANALYZE")
        self.assertEqual(git(self.tmp, "rev-parse", "--abbrev-ref", "HEAD"), f"aidlc/{REQ}")
        self.assertTrue(self.art("requirement.md").exists())

        self.write_art("spec.md", SPEC)
        code, out = self.complete("ANALYZE")
        self.assertEqual(out["action"], "STOP")
        self.assertEqual(out["status"], "AWAITING_APPROVAL")

        code, out = self.run_cli("next", REQ, "--client", "test")
        self.assertEqual(out["action"], "STOP")  # cannot pass the gate without a human
        self.approve("G1")

        code, out = self.run_cli("next", REQ, "--client", "test")
        self.assertEqual(out["stage"], "DESIGN")
        self.write_art("design.md", DESIGN)
        self.complete("DESIGN")
        self.approve("G2")

        self.run_cli("next", REQ, "--client", "test")
        self.write_art("plan.md", PLAN)
        code, out = self.complete("PLAN")
        self.assertEqual(out["next_stage"], "IMPLEMENT")

        code, out = self.run_cli("next", REQ, "--client", "test")
        self.assertEqual(out["tasks_remaining"], ["T1"])
        self.implement()
        code, out = self.complete("IMPLEMENT")
        self.assertEqual(code, 0, out)

        self.run_cli("next", REQ, "--client", "test")
        self.write_art("test-report.md", "## Summary\n## Acceptance criteria evidence\nAC1 ok AC2 ok\n## Test results\npass\n")
        code, out = self.complete("TEST")
        self.assertEqual(code, 0, out)

        self.run_cli("next", REQ, "--client", "test")
        self.write_art("review.md", "## Summary\n## Acceptance criteria verification\n## Code quality\n## Security\n"
                                    "## Guardrail report\n## Recommendation\n")
        code, out = self.complete("REVIEW")
        self.assertEqual(out["status"], "AWAITING_APPROVAL")
        self.approve("G3")

        self.run_cli("next", REQ, "--client", "test")
        self.write_art("mr.md", "## Summary\n## Changes\n## Acceptance criteria\n## Testing\n## AI disclosure\n")
        code, out = self.complete("MR")
        self.assertEqual(out["status"], "DONE", out)
        self.assertTrue((self.tmp / ".aidlc" / "records" / REQ / "audit.jsonl").exists())

        self.assertEqual(aidlc.verify_chain(self.tmp / ".aidlc" / "records" / REQ / "audit.jsonl"), [])
        self.assertEqual(aidlc.verify_record(REQ, "main"), [])
        code, _ = self.run_cli("verify", "--ci")
        self.assertEqual(code, 0)


class Guardrails(Base):
    def test_edit_after_approval_revokes_gate(self):
        self.run_cli("next", REQ, "--client", "test")
        self.write_art("spec.md", SPEC)
        self.complete("ANALYZE")
        self.approve("G1")
        self.write_art("spec.md", SPEC + "\n- AC3: sneaky\n")
        code, out = self.run_cli("next", REQ, "--client", "test")
        self.assertEqual(out["stage"], "ANALYZE")
        self.assertEqual(out["status"], "REWORK")
        self.assertTrue(any("revoked" in n for n in out["notices"]))

    def test_reject_feeds_back_comment(self):
        self.run_cli("next", REQ, "--client", "test")
        self.write_art("spec.md", SPEC)
        self.complete("ANALYZE")
        self.reject("G1", "Please add a max length rule")
        code, out = self.run_cli("next", REQ, "--client", "test")
        self.assertEqual(out["stage"], "ANALYZE")
        self.assertTrue(any(p.endswith("feedback.md") for p in out["inputs"]))

    def test_missing_sections_fail_and_budget_blocks(self):
        self.run_cli("next", REQ, "--client", "test")
        self.write_art("spec.md", "# nothing\n")
        for _ in range(5):
            code, out = self.complete("ANALYZE")
            self.assertEqual(code, 1)
        self.assertEqual(out["status"], "BLOCKED")
        code, out = self.run_cli("next", REQ, "--client", "test")
        self.assertEqual(out["action"], "STOP")

    def test_out_of_scope_file_fails_implement(self):
        self.to_implement()
        self.implement()
        (self.tmp / "other.txt").write_text("x")
        git(self.tmp, "add", "other.txt")
        git(self.tmp, "commit", "-q", "-m", f"[{REQ}][T1] extra")
        code, out = self.complete("IMPLEMENT")
        self.assertEqual(code, 1)
        self.assertTrue(any("scope" in e and "other.txt" in e for e in out["errors"]))

    def test_skipped_test_and_secret_detected(self):
        self.to_implement()
        self.implement()
        (self.tmp / "src" / "Tests").mkdir()
        (self.tmp / "src" / "Tests" / "HelloTests.cs").write_text('[Fact(Skip = "later")]\nvar pwd = "Sup3rSecret!";\n')
        git(self.tmp, "add", "-A")
        git(self.tmp, "commit", "-q", "-m", f"[{REQ}][T1] tests")
        code, out = self.complete("IMPLEMENT")
        rules = " ".join(out["errors"])
        self.assertIn("test-integrity", rules)
        self.assertIn("secret", rules)

    def test_failing_tests_block_test_stage(self):
        self.to_implement()
        self.implement()
        self.complete("IMPLEMENT")
        plan = PLAN.replace("sys.exit(0)", "sys.exit(3)")
        # plan is approved-free (no gate), but tampering after PLAN is caught by scope? simulate failing command
        self.write_art("plan.md", plan)
        self.run_cli("next", REQ, "--client", "test")
        self.write_art("test-report.md", "## Summary\n## Acceptance criteria evidence\nAC1 AC2\n## Test results\n")
        code, out = self.complete("TEST")
        self.assertEqual(code, 1)
        self.assertTrue(any("tests failed" in e for e in out["errors"]))

    def test_tampered_audit_log_detected(self):
        self.run_cli("next", REQ, "--client", "test")
        log = self.tmp / ".aidlc" / "runs" / REQ / "audit.jsonl"
        lines = log.read_text().splitlines()
        ev = json.loads(lines[0])
        ev["actor"] = "human:mallory"
        lines[0] = json.dumps(ev)
        log.write_text("\n".join(lines) + "\n")
        self.assertTrue(aidlc.verify_chain(log))

    def test_guard_blocks_state_edits_and_out_of_scope_writes(self):
        self.to_implement()
        deny = aidlc.guard_decision({"tool_name": "Write", "tool_input": {
            "file_path": str(self.tmp / ".aidlc" / "runs" / REQ / "state.json")}})
        self.assertIsNotNone(deny)
        deny = aidlc.guard_decision({"tool_name": "Edit", "tool_input": {"file_path": str(self.tmp / "README.md")}})
        self.assertIsNotNone(deny)
        allow = aidlc.guard_decision({"tool_name": "Write", "tool_input": {"file_path": str(self.tmp / "src" / "a.cs")}})
        self.assertIsNone(allow)
        deny = aidlc.guard_decision({"tool_name": "Bash", "tool_input": {
            "command": f"python tools/aidlc/aidlc.py approve {REQ} G1"}})
        self.assertIsNotNone(deny)
        deny = aidlc.guard_decision({"tool_name": "Bash", "tool_input": {"command": "git push origin main"}})
        self.assertIsNotNone(deny)
        allow = aidlc.guard_decision({"tool_name": "Bash", "tool_input": {"command": "dotnet test app/LeaveTracker.sln"}})
        self.assertIsNone(allow)

    def test_human_commands_refuse_agent_session(self):
        self.run_cli("next", REQ, "--client", "test")
        self.write_art("spec.md", SPEC)
        self.complete("ANALYZE")
        os.environ["CLAUDECODE"] = "1"
        try:
            with self.assertRaises(aidlc.AidlcError):
                aidlc.do_decision(REQ, "G1", "APPROVED", ask=lambda _: "APPROVE", interactive=True)
        finally:
            os.environ.pop("CLAUDECODE", None)

    def test_halt_stops_ai(self):
        (self.tmp / ".aidlc" / "HALT").write_text("incident")
        code, out = self.run_cli("next", REQ, "--client", "test")
        self.assertEqual(out["status"], "HALTED")

    def test_dependency_must_be_merged(self):
        req = self.tmp / "requirements" / "LT-902.md"
        req.write_text("---\nid: LT-902\ntitle: x\ndepends_on: [LT-901]\n---\nx\n")
        git(self.tmp, "add", "-A")
        git(self.tmp, "commit", "-q", "-m", "req")
        code, out = self.run_cli("next", "LT-902", "--client", "test")
        self.assertFalse(out["ok"])
        self.assertIn("LT-901", out["error"])

    def test_source_change_resets_run(self):
        self.run_cli("next", REQ, "--client", "test")
        self.write_art("spec.md", SPEC)
        self.complete("ANALYZE")
        self.approve("G1")
        git(self.tmp, "checkout", "-q", "main")
        p = self.tmp / "requirements" / f"{REQ}.md"
        p.write_text(REQUIREMENT + "\nAlso say goodbye.\n")
        git(self.tmp, "commit", "-qam", "change req")
        git(self.tmp, "checkout", "-q", f"aidlc/{REQ}")
        git(self.tmp, "merge", "-q", "main")
        code, out = self.run_cli("next", REQ, "--client", "test")
        self.assertEqual(out["stage"], "ANALYZE")
        self.assertTrue(any("source changed" in n.lower() for n in out["notices"]))


if __name__ == "__main__":
    unittest.main()
