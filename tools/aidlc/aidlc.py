#!/usr/bin/env python3
"""
aidlc.py - deterministic engine for the AI-Driven Life Cycle (AIDLC).

The engine is the ONLY component allowed to:
  * write workflow state (.aidlc/runs/<ID>/state.json)
  * decide stage transitions
  * record approvals (human-only commands)
  * append to the hash-chained audit log
  * seal a finished run into .aidlc/records/<ID>/

AI assistants (Claude Code, GitHub Copilot) call it from the terminal and do the
creative work in between. The engine never calls an LLM.

Zero third-party dependencies. Python 3.9+. Works on Windows, macOS, Linux.

AI-facing commands (print JSON):   next, complete, check, log, status --json
Human-only commands (need a TTY):  approve, reject, unblock, unlock, halt, unhalt
Hook / CI commands:                guard, hook-log, verify
"""
from __future__ import annotations

import argparse
import datetime as dt
import fnmatch
import getpass
import hashlib
import json
import os
import re
import shutil
import socket
import subprocess
import sys
import time
from pathlib import Path
from typing import Callable, Optional

ENGINE_VERSION = "1.0.0"

# ----------------------------------------------------------------------------
# Lifecycle definition
# ----------------------------------------------------------------------------
STAGES = ["ANALYZE", "G1", "DESIGN", "G2", "PLAN", "IMPLEMENT", "TEST",
          "REVIEW", "G3", "MR", "DONE"]
GATES = {"G1": "ANALYZE", "G2": "DESIGN", "G3": "REVIEW"}  # gate -> stage it approves
GATE_NAMES = {"G1": "Specification approval", "G2": "Design approval",
              "G3": "Raise-MR approval"}
OUTPUTS = {"ANALYZE": "spec.md", "DESIGN": "design.md", "PLAN": "plan.md",
           "IMPLEMENT": "implementation-notes.md", "TEST": "test-report.md",
           "REVIEW": "review.md", "MR": "mr.md"}
INPUTS = {
    "ANALYZE": ["requirement.md"],
    "DESIGN": ["requirement.md", "spec.md"],
    "PLAN": ["spec.md", "design.md"],
    "IMPLEMENT": ["spec.md", "design.md", "plan.md"],
    "TEST": ["spec.md", "plan.md", "implementation-notes.md"],
    "REVIEW": ["spec.md", "design.md", "plan.md", "implementation-notes.md",
               "test-report.md"],
    "MR": ["spec.md", "design.md", "plan.md", "test-report.md", "review.md"],
}
OPEN_STATUSES = {"PENDING", "IN_PROGRESS", "REWORK"}


# ----------------------------------------------------------------------------
# Small utilities
# ----------------------------------------------------------------------------
class AidlcError(Exception):
    """Expected, user-facing error."""


def root() -> Path:
    env = os.environ.get("AIDLC_ROOT")
    return Path(env).resolve() if env else Path(__file__).resolve().parents[2]


def aidlc_dir() -> Path:
    return root() / ".aidlc"


def runs_dir() -> Path:
    return aidlc_dir() / "runs"


def run_dir(req: str) -> Path:
    return runs_dir() / req


def records_dir(req: str) -> Path:
    return aidlc_dir() / "records" / req


def now() -> str:
    return dt.datetime.now(dt.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def sha_text(text: str) -> str:
    # Normalise line endings so hashes survive git autocrlf on Windows.
    return "sha256:" + hashlib.sha256(text.replace("\r\n", "\n").encode("utf-8")).hexdigest()


def sha_file(path: Path) -> Optional[str]:
    if not path.exists():
        return None
    return sha_text(path.read_text(encoding="utf-8", errors="replace"))


def read_json(path: Path, default=None):
    if not path.exists():
        return default
    return json.loads(path.read_text(encoding="utf-8"))


def write_json_atomic(path: Path, data) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    tmp = path.with_suffix(path.suffix + ".tmp")
    tmp.write_text(json.dumps(data, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    os.replace(tmp, path)


def emit(obj: dict, code: int = 0) -> int:
    print(json.dumps(obj, indent=2, ensure_ascii=False))
    return code


def rel(path: Path) -> str:
    return path.resolve().relative_to(root()).as_posix()


def norm_repo_path(p: str) -> Optional[str]:
    """Absolute or relative path -> repo-relative posix path (None if outside repo)."""
    if not p:
        return None
    candidate = Path(p)
    if not candidate.is_absolute():
        candidate = root() / candidate
    try:
        r = os.path.relpath(os.path.abspath(candidate), os.path.abspath(root()))
    except ValueError:  # different drive on Windows
        return None
    r = r.replace("\\", "/")
    if r.startswith(".."):
        return None
    return r


def path_match(path: str, patterns: list[str]) -> bool:
    p = path.lower() if os.name == "nt" else path
    for pat in patterns:
        q = pat.lower() if os.name == "nt" else pat
        if fnmatch.fnmatchcase(p, q) or (q.endswith("/**") and p == q[:-3]):
            return True
    return False


# ----------------------------------------------------------------------------
# Configuration and policy
# ----------------------------------------------------------------------------
def config() -> dict:
    cfg = read_json(aidlc_dir() / "config.json")
    if cfg is None:
        raise AidlcError(f"Missing {aidlc_dir() / 'config.json'}")
    return cfg


def policy() -> dict:
    pol = read_json(aidlc_dir() / "policies" / "guardrails.json")
    if pol is None:
        raise AidlcError("Missing .aidlc/policies/guardrails.json")
    return pol


def redact(value):
    pats = policy().get("redact_patterns", [])
    if isinstance(value, str):
        for pat in pats:
            value = re.sub(pat, "[REDACTED]", value)
        return value
    if isinstance(value, dict):
        return {k: redact(v) for k, v in value.items()}
    if isinstance(value, list):
        return [redact(v) for v in value]
    return value


# ----------------------------------------------------------------------------
# Git helpers
# ----------------------------------------------------------------------------
def git(*args: str, check: bool = True) -> str:
    proc = subprocess.run(["git", *args], cwd=root(), capture_output=True, text=True,
                          encoding="utf-8", errors="replace")
    if check and proc.returncode != 0:
        raise AidlcError(f"git {' '.join(args)} failed: {proc.stderr.strip()}")
    return proc.stdout.strip()


def git_ok(*args: str) -> bool:
    return subprocess.run(["git", *args], cwd=root(), capture_output=True).returncode == 0


def current_branch() -> str:
    return git("rev-parse", "--abbrev-ref", "HEAD")


def head_sha() -> str:
    return git("rev-parse", "HEAD")


def tree_is_clean() -> bool:
    return git("status", "--porcelain") == ""


def git_user() -> str:
    name = git("config", "user.name", check=False) or getpass.getuser()
    email = git("config", "user.email", check=False)
    return f"{name} <{email}>" if email else name


def changed_files(base: str, head: str = "HEAD") -> list[str]:
    out = git("diff", "--name-only", base, head)
    return [l for l in out.splitlines() if l]


def deleted_files(base: str, head: str = "HEAD") -> list[str]:
    out = git("diff", "--name-status", "--diff-filter=D", base, head)
    return [l.split("\t", 1)[1] for l in out.splitlines() if "\t" in l]


def lines_changed(base: str, head: str = "HEAD") -> int:
    total = 0
    for line in git("diff", "--numstat", base, head).splitlines():
        parts = line.split("\t")
        if len(parts) >= 2 and parts[0].isdigit() and parts[1].isdigit():
            total += int(parts[0]) + int(parts[1])
    return total


def added_lines(base: str, head: str = "HEAD") -> list[tuple[str, str]]:
    """[(file, added_line_text)] for the diff."""
    result, current = [], None
    for line in git("diff", "-U0", base, head).splitlines():
        if line.startswith("+++ "):
            current = line[6:] if line.startswith("+++ b/") else None
        elif line.startswith("+") and not line.startswith("+++") and current:
            result.append((current, line[1:]))
    return result


def commit_subjects(base: str, head: str = "HEAD") -> list[str]:
    out = git("log", "--format=%s", f"{base}..{head}")
    return [l for l in out.splitlines() if l]


# ----------------------------------------------------------------------------
# Audit log (append-only, hash-chained)
# ----------------------------------------------------------------------------
def _last_audit(path: Path) -> tuple[int, str]:
    if not path.exists():
        return 0, "GENESIS"
    lines = [l for l in path.read_text(encoding="utf-8").splitlines() if l.strip()]
    if not lines:
        return 0, "GENESIS"
    last = json.loads(lines[-1])
    return last["seq"], last["hash"]


def audit(req: str, actor: str, action: str, **data) -> dict:
    path = run_dir(req) / "audit.jsonl"
    path.parent.mkdir(parents=True, exist_ok=True)
    seq, prev = _last_audit(path)
    event = {"seq": seq + 1, "ts": now(), "req": req, "actor": actor,
             "action": action, **redact(data), "prev_hash": prev}
    event["hash"] = sha_text(json.dumps(event, sort_keys=True, ensure_ascii=False))
    with open(path, "a", encoding="utf-8") as fh:
        fh.write(json.dumps(event, ensure_ascii=False) + "\n")
    return event


def verify_chain(path: Path) -> list[str]:
    errors, prev, expected_seq = [], "GENESIS", 1
    if not path.exists():
        return [f"audit log missing: {path}"]
    for n, line in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
        if not line.strip():
            continue
        try:
            ev = json.loads(line)
        except json.JSONDecodeError:
            errors.append(f"line {n}: not valid JSON")
            break
        claimed = ev.pop("hash", None)
        if ev.get("prev_hash") != prev:
            errors.append(f"line {n}: prev_hash mismatch (chain broken)")
        if ev.get("seq") != expected_seq:
            errors.append(f"line {n}: seq {ev.get('seq')} expected {expected_seq}")
        if sha_text(json.dumps(ev, sort_keys=True, ensure_ascii=False)) != claimed:
            errors.append(f"line {n}: hash mismatch (event modified)")
        prev, expected_seq = claimed, expected_seq + 1
    return errors


# ----------------------------------------------------------------------------
# State, lock, active run, halt
# ----------------------------------------------------------------------------
def load_state(req: str) -> dict:
    st = read_json(run_dir(req) / "state.json")
    if st is None:
        raise AidlcError(f"No run for {req}. Start it with: aidlc.py next {req}")
    return st


def save_state(st: dict) -> None:
    st["updated_at"] = now()
    write_json_atomic(run_dir(st["req_id"]) / "state.json", st)


def load_approvals(req: str) -> dict:
    return read_json(run_dir(req) / "approvals.json", {}) or {}


def save_approvals(req: str, approvals: dict) -> None:
    write_json_atomic(run_dir(req) / "approvals.json", approvals)


def acquire_lock(req: str, owner: str) -> None:
    path = run_dir(req) / ".lock"
    minutes = config().get("lock_minutes", 120)
    existing = read_json(path)
    if existing and existing.get("owner") != owner:
        if dt.datetime.fromisoformat(existing["expires"].replace("Z", "+00:00")) > dt.datetime.now(dt.timezone.utc):
            raise AidlcError(f"Run {req} is locked by {existing['owner']} until {existing['expires']}. "
                             f"A human can release it with: aidlc.py unlock {req}")
    expires = (dt.datetime.now(dt.timezone.utc) + dt.timedelta(minutes=minutes)).strftime("%Y-%m-%dT%H:%M:%SZ")
    write_json_atomic(path, {"owner": owner, "expires": expires})


def set_active(req: Optional[str]) -> None:
    p = runs_dir() / ".active"
    if req:
        p.parent.mkdir(parents=True, exist_ok=True)
        p.write_text(req, encoding="utf-8")
    elif p.exists():
        p.unlink()


def get_active() -> Optional[str]:
    p = runs_dir() / ".active"
    return p.read_text(encoding="utf-8").strip() if p.exists() else None


def halted() -> Optional[str]:
    p = aidlc_dir() / "HALT"
    return p.read_text(encoding="utf-8").strip() or "halted" if p.exists() else None


def ai_actor(args) -> str:
    client = getattr(args, "client", None) or os.environ.get("AIDLC_CLIENT") or "unknown-ai"
    model = getattr(args, "model", None) or os.environ.get("AIDLC_MODEL")
    return f"ai:{client}" + (f"/{model}" if model else "")


def owner_id(args) -> str:
    return f"{ai_actor(args)}@{socket.gethostname()}"


# ----------------------------------------------------------------------------
# Requirement intake
# ----------------------------------------------------------------------------
FM_RE = re.compile(r"^---\s*\n(.*?)\n---\s*\n", re.S)


def parse_front_matter(text: str) -> dict:
    m = FM_RE.match(text.replace("\r\n", "\n"))
    meta = {}
    if not m:
        return meta
    for line in m.group(1).splitlines():
        if ":" not in line:
            continue
        k, v = line.split(":", 1)
        v = v.strip()
        if v.startswith("[") and v.endswith("]"):
            meta[k.strip()] = [x.strip().strip("'\"") for x in v[1:-1].split(",") if x.strip()]
        else:
            meta[k.strip()] = v.strip("'\"")
    return meta


def fetch_jira(req: str) -> tuple[str, dict]:
    import base64
    import urllib.request
    jc = config().get("jira", {})
    url = os.environ.get(jc.get("url_env", "JIRA_URL"))
    user = os.environ.get(jc.get("user_env", "JIRA_USER"))
    token = os.environ.get(jc.get("token_env", "JIRA_TOKEN"))
    if not url or not token:
        raise AidlcError("Jira intake enabled but JIRA_URL / JIRA_TOKEN env vars are not set")
    request = urllib.request.Request(f"{url.rstrip('/')}/rest/api/2/issue/{req}")
    if user:
        request.add_header("Authorization", "Basic " + base64.b64encode(f"{user}:{token}".encode()).decode())
    else:
        request.add_header("Authorization", f"Bearer {token}")
    request.add_header("Accept", "application/json")
    with urllib.request.urlopen(request, timeout=30) as resp:  # noqa: S310 (trusted, configured URL)
        issue = json.loads(resp.read().decode("utf-8"))
    f = issue.get("fields", {})
    body = (f"# {f.get('summary', req)}\n\n"
            f"- Type: {(f.get('issuetype') or {}).get('name', '')}\n"
            f"- Status: {(f.get('status') or {}).get('name', '')}\n\n"
            f"{f.get('description') or ''}\n")
    return body, {"type": "jira", "key": req, "title": f.get("summary", req), "depends_on": []}


def resolve_source(req: str) -> tuple[str, dict]:
    cfg = config()
    path = root() / cfg.get("requirements_dir", "requirements") / f"{req}.md"
    if path.exists():
        text = path.read_text(encoding="utf-8")
        meta = parse_front_matter(text)
        return text, {"type": "file", "path": rel(path), "title": meta.get("title", req),
                      "depends_on": meta.get("depends_on", []) or []}
    if cfg.get("jira", {}).get("enabled"):
        return fetch_jira(req)
    raise AidlcError(f"No requirement found for {req}: expected {rel(path)} "
                     f"(or enable Jira intake in .aidlc/config.json)")


def write_requirement(req: str, text: str, source: dict) -> str:
    header = (f"<!-- AIDLC intake: verbatim copy. Do not edit. id={req} "
              f"source={source.get('path') or 'jira:' + req} fetched={now()} -->\n")
    out = run_dir(req) / "artifacts" / "requirement.md"
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(header + text, encoding="utf-8")
    return sha_text(text)


def dependency_merged(dep: str, base: str) -> bool:
    return git_ok("cat-file", "-e", f"{base}:.aidlc/records/{dep}/state.json")


# ----------------------------------------------------------------------------
# Artifact parsing helpers
# ----------------------------------------------------------------------------
def artifact(req: str, name: str) -> Path:
    return run_dir(req) / "artifacts" / name


def headings(text: str) -> set[str]:
    return {m.group(1).strip().lower() for m in re.finditer(r"^#{1,4}\s+(.+?)\s*$", text, re.M)}


def missing_sections(path: Path, required: list[str]) -> list[str]:
    if not path.exists():
        return [f"{path.name} does not exist"]
    hs = headings(path.read_text(encoding="utf-8"))
    return [f"{path.name}: missing section '## {s}'" for s in required
            if not any(h == s.lower() or h.startswith(s.lower()) for h in hs)]


def ac_ids(spec_text: str) -> list[str]:
    return sorted(set(re.findall(r"\bAC\d+\b", spec_text)), key=lambda x: int(x[2:]))


def load_plan(req: str, base: Optional[Path] = None) -> dict:
    path = (base or run_dir(req) / "artifacts") / "plan.md"
    if not path.exists():
        raise AidlcError("plan.md not found")
    m = re.search(r"```aidlc-plan\s*\n(.*?)\n```", path.read_text(encoding="utf-8"), re.S)
    if not m:
        raise AidlcError("plan.md has no ```aidlc-plan JSON block")
    try:
        return json.loads(m.group(1))
    except json.JSONDecodeError as exc:
        raise AidlcError(f"aidlc-plan block is not valid JSON: {exc}") from exc


def task_commit_tag(req: str, task_id: str) -> str:
    return f"[{req}][{task_id}]"


def tasks_status(st: dict, plan: dict) -> tuple[list[str], list[str]]:
    subjects = commit_subjects(st["base_sha"])
    done, remaining = [], []
    for t in plan.get("tasks", []):
        tag = task_commit_tag(st["req_id"], t["id"])
        (done if any(tag in s for s in subjects) else remaining).append(t["id"])
    return done, remaining


# ----------------------------------------------------------------------------
# Guardrail scan (shared by complete, check, verify)
# ----------------------------------------------------------------------------
def guardrail_scan(req: str, base: str, allowed: list[str], head: str = "HEAD",
                   require_clean: bool = True) -> dict:
    pol, cfg = policy(), config()
    record_glob = [f".aidlc/records/{req}/**"]
    files = changed_files(base, head)
    violations = []

    if require_clean and not tree_is_clean():
        violations.append({"rule": "clean-tree", "detail": "uncommitted changes present; commit or discard them"})
    for f in files:
        if path_match(f, record_glob):
            continue
        if path_match(f, pol["protected_paths"]):
            violations.append({"rule": "protected-path", "file": f,
                               "detail": "governance/protected file changed by AI run"})
        elif not path_match(f, allowed):
            violations.append({"rule": "scope", "file": f, "detail": "file not in plan allowed_files"})
    limits = cfg.get("limits", {})
    loc = lines_changed(base, head)
    scoped = [f for f in files if not path_match(f, record_glob)]
    if len(scoped) > limits.get("max_files_changed", 10 ** 9):
        violations.append({"rule": "max-files", "detail": f"{len(scoped)} files > {limits['max_files_changed']}"})
    if loc > limits.get("max_lines_changed", 10 ** 9):
        violations.append({"rule": "max-lines", "detail": f"{loc} lines > {limits['max_lines_changed']}"})
    for f, line in added_lines(base, head):
        if path_match(f, record_glob):
            continue
        for pat in pol.get("secret_patterns", []):
            if re.search(pat, line):
                violations.append({"rule": "secret", "file": f, "detail": "possible secret in added line"})
                break
        if path_match(f, pol.get("test_file_globs", [])):
            for pat in pol.get("test_skip_patterns", []):
                if re.search(pat, line):
                    violations.append({"rule": "test-integrity", "file": f,
                                       "detail": f"skipped/hollow test pattern: {pat}"})
                    break
    for f in deleted_files(base, head):
        if path_match(f, pol.get("test_file_globs", [])):
            violations.append({"rule": "test-integrity", "file": f, "detail": "test file deleted"})
    return {"files_changed": len(scoped), "lines_changed": loc, "violations": violations}


# ----------------------------------------------------------------------------
# Approval binding / revalidation
# ----------------------------------------------------------------------------
def gate_binding(st: dict, gate: str) -> dict:
    req = st["req_id"]
    if gate == "G1":
        return {"artifact": "spec.md", "artifact_sha256": sha_file(artifact(req, "spec.md"))}
    if gate == "G2":
        return {"artifact": "design.md", "artifact_sha256": sha_file(artifact(req, "design.md"))}
    return {"artifact": "review.md", "artifact_sha256": sha_file(artifact(req, "review.md")),
            "head": head_sha()}


def revalidate(st: dict) -> Optional[str]:
    """Revoke approvals whose bound artifact/commit changed. Returns reason or None."""
    req = st["req_id"]
    if st["stage"] == "DONE":
        return None
    approvals = load_approvals(req)
    for gate in ("G1", "G2", "G3"):
        ap = approvals.get(gate)
        if not ap or ap.get("decision") != "APPROVED":
            continue
        current = gate_binding(st, gate)
        changed = current["artifact_sha256"] != ap["binding"]["artifact_sha256"]
        if gate == "G3" and current.get("head") != ap["binding"].get("head"):
            changed = True
        if changed:
            for g in ("G1", "G2", "G3")[("G1", "G2", "G3").index(gate):]:
                approvals.pop(g, None)
            save_approvals(req, approvals)
            st["stage"], st["status"] = GATES[gate], "REWORK"
            save_state(st)
            reason = f"{gate} revoked: approved {ap['binding']['artifact']} or commit changed after approval"
            audit(req, "engine", "approval.revoked", gate=gate, reason=reason)
            return reason
    return None


def check_source_drift(st: dict) -> Optional[str]:
    src = st["source"]
    if src.get("type") != "file" or st["stage"] == "DONE":
        return None
    path = root() / src["path"]
    if not path.exists():
        return None
    text = path.read_text(encoding="utf-8")
    if sha_text(text) == src["sha256"]:
        return None
    src["sha256"] = write_requirement(st["req_id"], text, src)
    save_approvals(st["req_id"], {})
    st["stage"], st["status"] = "ANALYZE", "REWORK"
    save_state(st)
    audit(st["req_id"], "engine", "source.changed", path=src["path"], new_sha256=src["sha256"])
    return "Requirement source changed since intake: approvals cleared, back to ANALYZE"


# ----------------------------------------------------------------------------
# Command: next
# ----------------------------------------------------------------------------
def init_run(req: str, args) -> dict:
    cfg = config()
    if not re.match(cfg.get("id_pattern", r"^[A-Za-z0-9-]+$"), req):
        raise AidlcError(f"Invalid requirement id '{req}' (pattern {cfg.get('id_pattern')})")
    text, source = resolve_source(req)
    base = cfg.get("base_branch", "main")
    for dep in source.get("depends_on", []):
        if not dependency_merged(dep, base):
            raise AidlcError(f"Dependency {dep} is not merged into {base} yet. "
                             f"Finish and merge {dep} first.")
    if not tree_is_clean():
        raise AidlcError("Working tree has uncommitted changes. Commit or stash before starting a run.")
    if not git_ok("rev-parse", "--verify", base):
        raise AidlcError(f"Base branch '{base}' not found")
    branch = cfg.get("branch_prefix", "aidlc/") + req
    if git_ok("rev-parse", "--verify", branch):
        git("checkout", branch)
    else:
        git("checkout", "-b", branch, base)
    base_sha = git("merge-base", base, "HEAD")
    run_dir(req).mkdir(parents=True, exist_ok=True)
    source["sha256"] = write_requirement(req, text, source)
    st = {"schema": 1, "engine": ENGINE_VERSION, "req_id": req, "title": source.get("title", req),
          "source": source, "depends_on": source.get("depends_on", []),
          "branch": branch, "base_branch": base, "base_sha": base_sha,
          "stage": "ANALYZE", "status": "PENDING", "created_at": now(),
          "attempts": {}, "artifacts": {"INTAKE": {"path": "requirement.md",
                                                    "sha256": sha_file(artifact(req, "requirement.md"))}}}
    save_state(st)
    save_approvals(req, {})
    audit(req, "engine", "run.created", branch=branch, base_sha=base_sha, source=source)
    audit(req, "engine", "stage.complete", stage="INTAKE", output="requirement.md",
          sha256=st["artifacts"]["INTAKE"]["sha256"], initiated_by=ai_actor(args))
    return st


def stop(st: dict, message: str, **extra) -> dict:
    return {"ok": True, "action": "STOP", "req": st["req_id"], "stage": st["stage"],
            "status": st["status"], "message": message, **extra}


def cmd_next(args) -> int:
    req = args.req
    reason = halted()
    if reason:
        return emit({"ok": False, "action": "STOP", "status": "HALTED",
                     "message": f"AIDLC is halted by a human: {reason}"}, 3)
    if not (run_dir(req) / "state.json").exists():
        st = init_run(req, args)
    else:
        st = load_state(req)
    acquire_lock(req, owner_id(args))

    if st["stage"] == "DONE":
        set_active(None)
        return emit(stop(st, "Run is DONE and sealed. " + mr_instructions(st)))

    if st["branch"] != current_branch():
        if not tree_is_clean():
            raise AidlcError(f"Run is on branch {st['branch']} but you are on {current_branch()} "
                             f"with uncommitted changes.")
        git("checkout", st["branch"])

    notices = [n for n in (check_source_drift(st), revalidate(st)) if n]

    if st["status"] == "AWAITING_APPROVAL":
        set_active(req)
        gate = st["stage"]
        return emit(stop(st, f"Waiting for human {gate} ({GATE_NAMES[gate]}). Ask the user to review "
                             f"{rel(artifact(req, OUTPUTS[GATES[gate]]))} and run in THEIR OWN terminal: "
                             f"python tools/aidlc/aidlc.py approve {req} {gate}   (or: reject {req} {gate} "
                             f"--comment \"...\"). Then run /aidlc-run {req} again.", notices=notices))
    if st["status"] == "BLOCKED":
        return emit(stop(st, f"Run is BLOCKED after repeated failed checks in {st['stage']}. "
                             f"A human must inspect and run: python tools/aidlc/aidlc.py unblock {req}",
                         notices=notices))

    stage = st["stage"]
    if st["status"] == "PENDING":
        audit(req, ai_actor(args), "stage.start", stage=stage)
    st["status"] = "REWORK" if st["status"] == "REWORK" else "IN_PROGRESS"
    save_state(st)
    set_active(req)

    inputs = [rel(artifact(req, n)) for n in INPUTS[stage] if artifact(req, n).exists()]
    ctx = aidlc_dir() / "context" / "project.md"
    if ctx.exists():
        inputs.append(rel(ctx))
    fb = artifact(req, "feedback.md")
    if fb.exists():
        inputs.append(rel(fb))
    allowed = [rel(artifact(req, OUTPUTS[stage]))]
    out = {"ok": True, "action": "DO_STAGE", "req": req, "title": st["title"], "stage": stage,
           "status": st["status"], "skill": f".aidlc/skills/{stage.lower()}.md",
           "template": f".aidlc/templates/{OUTPUTS[stage]}" if (aidlc_dir() / "templates" / OUTPUTS[stage]).exists() else None,
           "inputs": inputs, "output": rel(artifact(req, OUTPUTS[stage])),
           "branch": st["branch"], "notices": notices,
           "then": f"python tools/aidlc/aidlc.py complete {req} --stage {stage} --client <claude-code|copilot>"}
    if stage in ("IMPLEMENT", "TEST"):
        plan = load_plan(req)
        done, remaining = tasks_status(st, plan)
        allowed += plan.get("allowed_files", [])
        out.update({"tasks_done": done, "tasks_remaining": remaining,
                    "commit_format": f"[{req}][T<n>] <summary>",
                    "test_command": plan.get("test_command") or config().get("test_command")})
    if stage == "REVIEW":
        out["self_check"] = f"python tools/aidlc/aidlc.py check {req}"
    out["allowed_paths"] = allowed
    if st["status"] == "REWORK" and fb.exists():
        out["message"] = "REWORK: address the human feedback in feedback.md before completing."
    return emit(out)


def mr_instructions(st: dict) -> str:
    rec = f".aidlc/records/{st['req_id']}/artifacts/mr.md"
    if config().get("mr", {}).get("ai_may_push_feature_branch"):
        return (f"Push the feature branch and open the MR: git push -u origin {st['branch']} && "
                f"glab mr create --source-branch {st['branch']} --target-branch {st['base_branch']} "
                f"--title \"{st['req_id']}: {st['title']}\" --description-file {rec}. "
                f"A HUMAN reviews and merges.")
    return f"A human pushes {st['branch']} and opens the MR using {rec}. AI never merges."


# ----------------------------------------------------------------------------
# Command: complete
# ----------------------------------------------------------------------------
def checks_for(st: dict, stage: str) -> list[str]:
    req, cfg = st["req_id"], config()
    sections = cfg.get("required_sections", {})
    out_path = artifact(req, OUTPUTS[stage])
    errors = missing_sections(out_path, sections.get(stage, [])) if stage in OUTPUTS else []
    if errors and not out_path.exists():
        return errors

    if stage == "ANALYZE":
        acs = ac_ids(out_path.read_text(encoding="utf-8"))
        if not acs:
            errors.append("spec.md: acceptance criteria must be numbered AC1, AC2, ...")
        if not tree_is_clean():
            errors.append("ANALYZE must not change repository files (only the spec artifact)")

    elif stage in ("DESIGN",):
        if not tree_is_clean():
            errors.append(f"{stage} must not change repository files")

    elif stage == "PLAN":
        if not tree_is_clean():
            errors.append("PLAN must not change repository files")
        try:
            plan = load_plan(req)
        except AidlcError as exc:
            return errors + [str(exc)]
        tasks = plan.get("tasks") or []
        allowed = plan.get("allowed_files") or []
        if not tasks:
            errors.append("plan: 'tasks' must be a non-empty list")
        for t in tasks:
            if not re.match(r"^T\d+$", str(t.get("id", ""))):
                errors.append(f"plan: task id '{t.get('id')}' must look like T1, T2 ...")
        if not allowed:
            errors.append("plan: 'allowed_files' must be a non-empty list")
        for pat in allowed:
            if pat.strip() in ("*", "**", "**/*", "./**") or pat.startswith("/") or ".." in pat:
                errors.append(f"plan: allowed_files pattern '{pat}' is too broad or escapes the repo")
            if path_match(pat.rstrip("*/") or pat, policy()["protected_paths"]) or path_match(pat, policy()["protected_paths"]):
                errors.append(f"plan: allowed_files pattern '{pat}' touches a protected path")
        spec_acs = set(ac_ids(artifact(req, "spec.md").read_text(encoding="utf-8")))
        covered = {a for t in tasks for a in (t.get("covers") or [])}
        if spec_acs - covered:
            errors.append(f"plan: acceptance criteria not covered by any task: {sorted(spec_acs - covered)}")
        if len(tasks) > cfg.get("limits", {}).get("max_tasks", 12):
            errors.append("plan: too many tasks; split the requirement")

    elif stage == "IMPLEMENT":
        plan = load_plan(req)
        _, remaining = tasks_status(st, plan)
        if remaining:
            errors.append(f"tasks without a commit tagged {task_commit_tag(req, 'Tn')}: {remaining}")
        notes = out_path.read_text(encoding="utf-8") if out_path.exists() else ""
        for t in plan.get("tasks", []):
            if t["id"] not in notes:
                errors.append(f"implementation-notes.md: no entry for {t['id']}")
        scan = guardrail_scan(req, st["base_sha"], plan.get("allowed_files", []))
        errors += [f"guardrail {v['rule']}: {v.get('file', '')} {v['detail']}".strip() for v in scan["violations"]]

    elif stage == "TEST":
        plan = load_plan(req)
        scan = guardrail_scan(req, st["base_sha"], plan.get("allowed_files", []))
        errors += [f"guardrail {v['rule']}: {v.get('file', '')} {v['detail']}".strip() for v in scan["violations"]]
        cmd = plan.get("test_command") or cfg.get("test_command")
        log_path = artifact(req, "test-output.log")
        try:
            proc = subprocess.run(cmd, shell=True, cwd=root(), capture_output=True, text=True,
                                  encoding="utf-8", errors="replace",
                                  timeout=cfg.get("test_timeout_seconds", 900))
            log_path.write_text(f"$ {cmd}\nexit={proc.returncode}\n\n{proc.stdout}\n{proc.stderr}", encoding="utf-8")
            st["last_test"] = {"command": cmd, "exit_code": proc.returncode, "at": now(),
                               "log_sha256": sha_file(log_path)}
            if proc.returncode != 0:
                errors.append(f"tests failed (exit {proc.returncode}); see {rel(log_path)}")
        except subprocess.TimeoutExpired:
            errors.append(f"tests timed out after {cfg.get('test_timeout_seconds', 900)}s")
        report = out_path.read_text(encoding="utf-8") if out_path.exists() else ""
        spec_acs = ac_ids(artifact(req, "spec.md").read_text(encoding="utf-8"))
        missing = [a for a in spec_acs if a not in report]
        if missing:
            errors.append(f"test-report.md: no evidence listed for {missing}")

    elif stage == "REVIEW":
        plan = load_plan(req)
        scan = guardrail_scan(req, st["base_sha"], plan.get("allowed_files", []))
        write_json_atomic(artifact(req, "guardrail-report.json"), {"at": now(), "head": head_sha(), **scan})
        errors += [f"guardrail {v['rule']}: {v.get('file', '')} {v['detail']}".strip() for v in scan["violations"]]
        if not st.get("last_test") or st["last_test"].get("exit_code") != 0:
            errors.append("no passing engine-run test result recorded")

    elif stage == "MR":
        ap = load_approvals(req).get("G3")
        if not ap or ap["binding"].get("head") != head_sha():
            errors.append("HEAD moved after G3 approval; new commits are not allowed in MR stage")
        if not tree_is_clean():
            errors.append("MR stage must not change repository files")
    return errors


def advance(st: dict) -> None:
    nxt = STAGES[STAGES.index(st["stage"]) + 1]
    st["stage"] = nxt
    st["status"] = "AWAITING_APPROVAL" if nxt in GATES else "PENDING"


def seal(st: dict, args) -> str:
    """Copy the run into .aidlc/records/<ID>/ and commit it on the feature branch."""
    req = st["req_id"]
    dest = records_dir(req)
    if dest.exists():
        shutil.rmtree(dest)
    (dest / "artifacts").mkdir(parents=True)
    for name in ("state.json", "approvals.json", "audit.jsonl"):
        shutil.copy2(run_dir(req) / name, dest / name)
    for f in (run_dir(req) / "artifacts").iterdir():
        if f.is_file():
            shutil.copy2(f, dest / "artifacts" / f.name)
    git("add", "-f", rel(dest))
    git("commit", "-m", f"[{req}] AIDLC sealed record\n\nGenerated by aidlc.py {ENGINE_VERSION}. "
                        f"Initiated by {ai_actor(args)}.")
    return head_sha()


def cmd_complete(args) -> int:
    req, stage = args.req, args.stage.upper()
    if halted():
        return emit({"ok": False, "status": "HALTED", "message": "AIDLC is halted by a human."}, 3)
    st = load_state(req)
    acquire_lock(req, owner_id(args))
    if st["stage"] != stage:
        raise AidlcError(f"Run is at {st['stage']}, not {stage}. Call 'next' to see what to do.")
    if st["status"] not in OPEN_STATUSES:
        raise AidlcError(f"Run status is {st['status']}; cannot complete {stage}.")
    if stage in GATES:
        raise AidlcError("Gates are completed only by a human via approve/reject.")

    errors = checks_for(st, stage)
    if errors:
        st["attempts"][stage] = st["attempts"].get(stage, 0) + 1
        maxn = config().get("budgets", {}).get("max_failed_completes_per_stage", 5)
        blocked = st["attempts"][stage] >= maxn
        if blocked:
            st["status"] = "BLOCKED"
        save_state(st)
        audit(req, ai_actor(args), "stage.check_failed", stage=stage, errors=errors,
              attempt=st["attempts"][stage])
        if blocked:
            audit(req, "engine", "run.blocked", stage=stage, reason=f"{maxn} failed completion attempts")
        return emit({"ok": False, "req": req, "stage": stage,
                     "status": st["status"], "attempt": st["attempts"][stage], "max_attempts": maxn,
                     "errors": errors,
                     "message": "BLOCKED: stop and tell the user." if blocked else
                     "Fix the errors and run complete again."}, 1)

    out_path = artifact(req, OUTPUTS[stage])
    st["artifacts"][stage] = {"path": OUTPUTS[stage], "sha256": sha_file(out_path)}
    if stage in ("IMPLEMENT", "TEST", "REVIEW"):
        st["artifacts"][stage]["head"] = head_sha()
    st["attempts"].pop(stage, None)
    audit(req, ai_actor(args), "stage.complete", stage=stage, output=OUTPUTS[stage],
          sha256=st["artifacts"][stage]["sha256"], head=st["artifacts"][stage].get("head"))
    advance(st)

    if st["stage"] == "DONE":
        st["status"] = "DONE"
        st["completed_at"] = now()
        save_state(st)
        audit(req, "engine", "run.sealed", records=f".aidlc/records/{req}")
        commit = seal(st, args)
        audit(req, "engine", "run.sealed_commit", commit=commit)
        set_active(None)
        return emit({"ok": True, "req": req, "stage": "DONE", "status": "DONE",
                     "sealed_commit": commit, "message": "Run sealed. " + mr_instructions(st)})

    save_state(st)
    if st["status"] == "AWAITING_APPROVAL":
        audit(req, "engine", "gate.requested", gate=st["stage"])
        return emit(stop(st, f"STOP. Human approval {st['stage']} ({GATE_NAMES[st['stage']]}) required. "
                             f"Tell the user to review {rel(out_path)} and run in their own terminal: "
                             f"python tools/aidlc/aidlc.py approve {req} {st['stage']}"))
    return emit({"ok": True, "req": req, "completed": stage, "next_stage": st["stage"],
                 "status": st["status"], "message": f"Run: python tools/aidlc/aidlc.py next {req} --client <you>"})


# ----------------------------------------------------------------------------
# Human-only commands
# ----------------------------------------------------------------------------
def require_human(cmd: str) -> None:
    """Best-effort barrier against an AI agent running human-only commands."""
    if os.environ.get("CLAUDECODE") or os.environ.get("AIDLC_AGENT"):
        raise AidlcError(f"'{cmd}' is human-only and cannot run inside an AI agent session.")
    if not (sys.stdin.isatty() and sys.stdout.isatty()):
        raise AidlcError(f"'{cmd}' must be run interactively by a human in a terminal.")


def show_gate_summary(st: dict, gate: str) -> None:
    req = st["req_id"]
    path = artifact(req, OUTPUTS[GATES[gate]])
    print(f"\n=== {req} - {gate}: {GATE_NAMES[gate]} ===")
    print(f"Artifact : {rel(path)}")
    print(f"SHA-256  : {sha_file(path)}")
    if gate == "G3":
        print(f"Branch   : {st['branch']} @ {head_sha()[:12]}")
        print(git("diff", "--stat", st["base_sha"], "HEAD"))
        rep = read_json(artifact(req, "guardrail-report.json"), {})
        print(f"Guardrails: {len(rep.get('violations', []))} violations, "
              f"{rep.get('files_changed')} files, {rep.get('lines_changed')} lines")
    print("-" * 70)
    lines = path.read_text(encoding="utf-8").splitlines()
    print("\n".join(lines[:60]) + ("\n... (open the file for the rest)" if len(lines) > 60 else ""))
    print("-" * 70)


def do_decision(req: str, gate: str, decision: str, comment: str = "",
                ask: Callable[[str], str] = input, interactive: bool = True) -> dict:
    gate = gate.upper()
    if gate not in GATES:
        raise AidlcError(f"Unknown gate {gate}. Gates: {', '.join(GATES)}")
    if interactive:
        require_human("approve" if decision == "APPROVED" else "reject")
    st = load_state(req)
    if st["stage"] != gate or st["status"] != "AWAITING_APPROVAL":
        raise AidlcError(f"{req} is at {st['stage']}/{st['status']}, not awaiting {gate}.")
    if interactive:
        show_gate_summary(st, gate)
    word = "APPROVE" if decision == "APPROVED" else "REJECT"
    if ask(f"Type {word} to confirm: ").strip() != word:
        raise AidlcError("Not confirmed. Nothing recorded.")
    actor = f"human:{git_user()}"
    approvals = load_approvals(req)
    entry = {"decision": decision, "by": git_user(), "os_user": getpass.getuser(),
             "host": socket.gethostname(), "at": now(), "binding": gate_binding(st, gate),
             "comment": comment}
    if decision == "APPROVED":
        approvals[gate] = entry
        advance(st)
        st["status"] = "PENDING"
    else:
        approvals.pop(gate, None)
        st["stage"], st["status"] = GATES[gate], "REWORK"
        fb = artifact(req, "feedback.md")
        with open(fb, "a", encoding="utf-8") as fh:
            fh.write(f"\n## {gate} rejected by {git_user()} at {now()}\n\n{comment or '(no comment)'}\n")
    save_approvals(req, approvals)
    save_state(st)
    audit(req, actor, "gate.approve" if decision == "APPROVED" else "gate.reject", gate=gate,
          binding=entry["binding"], comment=comment)
    return st


def cmd_approve(args) -> int:
    st = do_decision(args.req, args.gate, "APPROVED", args.comment or "")
    print(f"\nApproved {args.gate.upper()}. {args.req} is now at {st['stage']}. "
          f"Resume with: /aidlc-run {args.req}")
    return 0


def cmd_reject(args) -> int:
    if not args.comment:
        raise AidlcError("--comment is required when rejecting (it is fed back to the AI).")
    st = do_decision(args.req, args.gate, "REJECTED", args.comment)
    print(f"\nRejected {args.gate.upper()}. {args.req} is back at {st['stage']} (REWORK). "
          f"Resume with: /aidlc-run {args.req}")
    return 0


def cmd_unblock(args) -> int:
    require_human("unblock")
    st = load_state(args.req)
    if st["status"] != "BLOCKED":
        raise AidlcError("Run is not BLOCKED.")
    st["status"], st["attempts"] = "REWORK", {}
    save_state(st)
    audit(args.req, f"human:{git_user()}", "run.unblocked", stage=st["stage"], comment=args.comment or "")
    print(f"Unblocked {args.req} at {st['stage']}.")
    return 0


def cmd_unlock(args) -> int:
    require_human("unlock")
    p = run_dir(args.req) / ".lock"
    if p.exists():
        p.unlink()
    audit(args.req, f"human:{git_user()}", "run.unlocked")
    print(f"Lock released for {args.req}.")
    return 0


def cmd_halt(args) -> int:
    require_human("halt")
    (aidlc_dir() / "HALT").write_text(args.reason or f"halted by {git_user()} at {now()}", encoding="utf-8")
    print("AIDLC halted. All AI commands will STOP until: aidlc.py unhalt")
    return 0


def cmd_unhalt(args) -> int:
    require_human("unhalt")
    p = aidlc_dir() / "HALT"
    if p.exists():
        p.unlink()
    print("AIDLC resumed.")
    return 0


# ----------------------------------------------------------------------------
# Read-only / AI helper commands
# ----------------------------------------------------------------------------
def cmd_check(args) -> int:
    st = load_state(args.req)
    plan = load_plan(args.req)
    scan = guardrail_scan(args.req, st["base_sha"], plan.get("allowed_files", []))
    return emit({"ok": not scan["violations"], "req": args.req, **scan}, 0 if not scan["violations"] else 1)


def cmd_log(args) -> int:
    load_state(args.req)
    ev = audit(args.req, ai_actor(args), "note", stage=load_state(args.req)["stage"], note=args.note[:2000])
    return emit({"ok": True, "seq": ev["seq"]})


def cmd_status(args) -> int:
    reqs = [args.req] if args.req else sorted(
        p.name for p in runs_dir().iterdir() if p.is_dir() and (p / "state.json").exists()) if runs_dir().exists() else []
    rows = []
    for r in reqs:
        st = load_state(r)
        ap = load_approvals(r)
        rows.append({"req": r, "title": st["title"], "stage": st["stage"], "status": st["status"],
                     "branch": st["branch"], "approved": sorted(k for k, v in ap.items() if v.get("decision") == "APPROVED"),
                     "updated_at": st["updated_at"]})
    if args.json:
        return emit({"ok": True, "halted": halted(), "active": get_active(), "runs": rows})
    if halted():
        print(f"!! HALTED: {halted()}")
    if not rows:
        print("No AIDLC runs yet.")
    for row in rows:
        print(f"{row['req']:<10} {row['stage']:<10} {row['status']:<18} gates={','.join(row['approved']) or '-':<9} "
              f"{row['title']}")
    return 0


# ----------------------------------------------------------------------------
# Claude Code hooks: guard (PreToolUse) and hook-log (PostToolUse)
# ----------------------------------------------------------------------------
WRITE_TOOLS = {"Edit", "Write", "MultiEdit", "NotebookEdit"}


def guard_decision(payload: dict) -> Optional[str]:
    """Return a denial reason, or None to allow."""
    pol = policy()
    tool = payload.get("tool_name", "")
    tin = payload.get("tool_input", {}) or {}

    if tool == "Bash":
        cmd = tin.get("command", "")
        for pat in pol.get("deny_bash_patterns", []):
            if re.search(pat, cmd):
                return f"Command blocked by AIDLC guardrail (pattern: {pat})."
        return None
    if tool not in WRITE_TOOLS:
        return None

    target = norm_repo_path(tin.get("file_path") or tin.get("notebook_path") or "")
    if target is None:
        return "Writes outside the repository are not allowed."
    active = get_active()
    own_artifact, st = None, None
    if active and (run_dir(active) / "state.json").exists():
        st = load_state(active)
        if st["stage"] in OUTPUTS:
            own_artifact = rel(artifact(active, OUTPUTS[st["stage"]]))
    if st and target == own_artifact and st.get("status") in OPEN_STATUSES:
        return None
    if path_match(target, [".aidlc/runs/**", ".aidlc/records/**", ".aidlc/HALT"]):
        return "AIDLC state, logs, approvals and records are written only by aidlc.py."
    if path_match(target, pol["protected_paths"]):
        return f"{target} is a protected governance path."
    if st:
        if st["status"] not in OPEN_STATUSES:
            return (f"Run {active} is {st['status']} at {st['stage']}. No edits until a human acts "
                    f"(run 'aidlc.py next {active}' for instructions).")
        if st["stage"] in ("IMPLEMENT", "TEST"):
            allowed = load_plan(active).get("allowed_files", [])
            if not path_match(target, allowed):
                return f"{target} is not in the approved plan's allowed_files for {active}."
            return None
        return f"In stage {st['stage']} you may only write {own_artifact}."
    return None


def cmd_guard(args) -> int:
    try:
        payload = json.loads(sys.stdin.read() or "{}")
    except json.JSONDecodeError:
        return 0
    try:
        reason = guard_decision(payload)
    except AidlcError as exc:
        reason = f"AIDLC guard error: {exc}"
    if reason:
        print(reason, file=sys.stderr)
        return 2  # Claude Code: exit 2 blocks the tool call and shows stderr to the model
    return 0


def cmd_hook_log(args) -> int:
    try:
        payload = json.loads(sys.stdin.read() or "{}")
        active = get_active()
        if not active or not (run_dir(active) / "state.json").exists():
            return 0
        tin = payload.get("tool_input", {}) or {}
        target = tin.get("file_path") or tin.get("notebook_path")
        data = {"tool": payload.get("tool_name"), "stage": load_state(active)["stage"]}
        if target:
            data["target"] = norm_repo_path(target) or target
        if "command" in tin:
            data["command"] = str(tin["command"])[:300]
        audit(active, f"ai:{args.client}", "tool.use", **data)
    except Exception:  # never break the agent because logging failed
        pass
    return 0


# ----------------------------------------------------------------------------
# verify (pre-push hook and CI)
# ----------------------------------------------------------------------------
def verify_record(req: str, base_ref: str) -> list[str]:
    rec = records_dir(req)
    errors = [f"record audit: {e}" for e in verify_chain(rec / "audit.jsonl")]
    approvals = read_json(rec / "approvals.json", {}) or {}
    st = read_json(rec / "state.json", {}) or {}
    if st.get("stage") != "DONE":
        errors.append("sealed state is not DONE")
    for gate in ("G1", "G2", "G3"):
        ap = approvals.get(gate)
        if not ap or ap.get("decision") != "APPROVED":
            errors.append(f"{gate} approval missing")
            continue
        b = ap["binding"]
        actual = sha_file(rec / "artifacts" / b["artifact"])
        if actual != b["artifact_sha256"]:
            errors.append(f"{gate}: {b['artifact']} differs from what was approved")
    events = [json.loads(l) for l in (rec / "audit.jsonl").read_text(encoding="utf-8").splitlines() if l.strip()] \
        if (rec / "audit.jsonl").exists() else []
    for gate in ("G1", "G2", "G3"):
        if not any(e["action"] == "gate.approve" and e.get("gate") == gate and e["actor"].startswith("human:")
                   for e in events):
            errors.append(f"{gate}: no human gate.approve event in audit log")
    g3 = approvals.get("G3", {}).get("binding", {}).get("head")
    if g3:
        if not git_ok("merge-base", "--is-ancestor", g3, "HEAD"):
            errors.append("G3-approved commit is not an ancestor of HEAD")
        else:
            after = changed_files(g3, "HEAD")
            extra = [f for f in after if not f.startswith(f".aidlc/records/{req}/")]
            if extra:
                errors.append(f"files changed after G3 approval: {extra}")
    try:
        plan = load_plan(req, rec / "artifacts")
        base = git("merge-base", base_ref, "HEAD")
        scan = guardrail_scan(req, base, plan.get("allowed_files", []), require_clean=False)
        errors += [f"guardrail {v['rule']}: {v.get('file', '')} {v['detail']}".strip() for v in scan["violations"]]
    except AidlcError as exc:
        errors.append(str(exc))
    return errors


def cmd_verify(args) -> int:
    cfg = config()
    branch = args.branch or current_branch()
    prefix = cfg.get("branch_prefix", "aidlc/")
    base_ref = args.base or cfg.get("base_branch", "main")
    if not branch.startswith(prefix):
        print(f"verify: {branch} is not an AIDLC branch - nothing to verify.")
        return 0
    req = branch[len(prefix):]
    if records_dir(req).exists():
        errors = verify_record(req, base_ref)
        scope = "sealed record"
    elif args.ci:
        errors, scope = [f"no sealed record at .aidlc/records/{req}; the run did not finish"], "ci"
    elif (run_dir(req) / "audit.jsonl").exists():
        errors, scope = [f"audit: {e}" for e in verify_chain(run_dir(req) / "audit.jsonl")], "live run"
        st = load_state(req)
        if st["stage"] in ("TEST", "REVIEW", "G3", "MR"):
            scan = guardrail_scan(req, st["base_sha"], load_plan(req).get("allowed_files", []), require_clean=False)
            errors += [f"guardrail {v['rule']}: {v.get('file', '')} {v['detail']}".strip() for v in scan["violations"]]
    else:
        errors, scope = [f"AIDLC branch {branch} has no run or record"], "none"
    if errors:
        print(f"AIDLC verify FAILED for {req} ({scope}):")
        for e in errors:
            print(f"  - {e}")
        return 1
    print(f"AIDLC verify OK for {req} ({scope}).")
    return 0


# ----------------------------------------------------------------------------
# CLI
# ----------------------------------------------------------------------------
def build_parser() -> argparse.ArgumentParser:
    p = argparse.ArgumentParser(prog="aidlc.py", description="AIDLC deterministic workflow engine")
    p.add_argument("--version", action="version", version=ENGINE_VERSION)
    sub = p.add_subparsers(dest="cmd", required=True)

    def ai(sp):
        sp.add_argument("--client", default=None, help="claude-code | copilot | other")
        sp.add_argument("--model", default=None, help="model name, for the audit log")
        return sp

    s = ai(sub.add_parser("next", help="[AI] start or resume a run; prints what to do"))
    s.add_argument("req")
    s.set_defaults(fn=cmd_next)
    s = ai(sub.add_parser("complete", help="[AI] finish the current stage; runs checks"))
    s.add_argument("req")
    s.add_argument("--stage", required=True)
    s.set_defaults(fn=cmd_complete)
    s = sub.add_parser("check", help="[AI/human] read-only guardrail scan of the branch")
    s.add_argument("req")
    s.set_defaults(fn=cmd_check)
    s = ai(sub.add_parser("log", help="[AI] add a decision note to the audit log"))
    s.add_argument("req")
    s.add_argument("--note", required=True)
    s.set_defaults(fn=cmd_log)
    s = sub.add_parser("status", help="show runs")
    s.add_argument("req", nargs="?")
    s.add_argument("--json", action="store_true")
    s.set_defaults(fn=cmd_status)

    for name, fn in (("approve", cmd_approve), ("reject", cmd_reject)):
        s = sub.add_parser(name, help=f"[HUMAN] {name} a gate (interactive)")
        s.add_argument("req")
        s.add_argument("gate", help="G1 | G2 | G3")
        s.add_argument("--comment", default="")
        s.set_defaults(fn=fn)
    s = sub.add_parser("unblock", help="[HUMAN] clear BLOCKED status")
    s.add_argument("req")
    s.add_argument("--comment", default="")
    s.set_defaults(fn=cmd_unblock)
    s = sub.add_parser("unlock", help="[HUMAN] release a run lock")
    s.add_argument("req")
    s.set_defaults(fn=cmd_unlock)
    s = sub.add_parser("halt", help="[HUMAN] kill switch: stop all AI runs")
    s.add_argument("--reason", default="")
    s.set_defaults(fn=cmd_halt)
    s = sub.add_parser("unhalt", help="[HUMAN] lift the kill switch")
    s.set_defaults(fn=cmd_unhalt)

    s = sub.add_parser("guard", help="[hook] Claude PreToolUse guard (reads JSON on stdin)")
    s.set_defaults(fn=cmd_guard)
    s = sub.add_parser("hook-log", help="[hook] Claude PostToolUse logger (reads JSON on stdin)")
    s.add_argument("--client", default="claude-code")
    s.set_defaults(fn=cmd_hook_log)
    s = sub.add_parser("verify", help="[hook/CI] verify audit chain, approvals and scope")
    s.add_argument("--branch", default=None)
    s.add_argument("--base", default=None)
    s.add_argument("--ci", action="store_true", help="require a sealed record")
    s.set_defaults(fn=cmd_verify)
    return p


def main(argv: Optional[list[str]] = None) -> int:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    args = build_parser().parse_args(argv)
    try:
        return args.fn(args)
    except AidlcError as exc:
        if args.cmd in ("next", "complete", "check", "log") or getattr(args, "json", False):
            return emit({"ok": False, "error": str(exc)}, 1)
        print(f"aidlc: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
