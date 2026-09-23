#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
release_compliance_scan.py -- Release content-compliance scanner.

Turns the "clean comments / keep the public repo pure" convention into a
mechanical gate. It walks a release working tree (by default this repository)
and flags content that must never ship in an open-source build:

  * edition gating / paid-feature references
  * internal version markers and review/audit finding tags
  * provenance markers leaking that a file was copied from another tree
  * copyright headers not using the project's canonical studio name
  * non-English (CJK) comments left in source / markup
  * commercial / anti-piracy wording, internal numbered-doc references
  * UTF-8 / BOM encoding violations

Findings not covered by the allowlist make the process exit 1, so it can be
wired into a pre-commit hook or a CI step.

Rules and allowlist live in a sibling JSON file so the policy is auditable
without touching code. This file and its PUBLIC ruleset contain only generic
detection patterns -- no private identities or internal paths. A stricter
ruleset (e.g. matching specific maintainer emails) is injected in CI via a
repository secret and is never committed.

Usage:
  python release_compliance_scan.py                      # scan this repo (auto-located)
  python release_compliance_scan.py --path <dir>         # scan a specific target
  python release_compliance_scan.py --rules <json>       # point at another ruleset
  python release_compliance_scan.py --files-from -       # scan only paths on stdin (pre-commit)
  python release_compliance_scan.py --self-test          # verify every rule still fires
Standard library only.
"""
from __future__ import annotations

import argparse
import fnmatch
import json
import re
import sys
from pathlib import Path

TEXT_EXTS = {
    ".cs", ".xaml", ".json", ".md", ".py", ".nsi", ".nsh", ".yml", ".yaml",
    ".props", ".xml", ".txt", ".sln", ".csproj", ".config", ".html", ".css",
    ".js", ".ps1", ".bat", ".editorconfig",
}
ALWAYS_SCAN_NAMES = {"LICENSE", "NOTICE", ".gitattributes", ".gitignore", ".editorconfig"}
SKIP_DIRS = {
    ".git", "bin", "obj", "publish", "__pycache__", "node_modules", ".vs",
    ".vscode", "dist", "build", ".venv",
}
# VS-generated project/solution files legitimately carry a UTF-8 BOM.
BOM_OK_EXTS = {".sln", ".vcxproj", ".csproj"}
# The gate's own files necessarily contain banned sample strings; skip them to
# avoid self-matches.
SELF_EXCLUDE = ("script/verify/release-compliance",
                "script/verify/release_compliance_scan",
                ".githooks/")


def find_default_target() -> Path:
    """script/verify/<thisfile> -> repository root."""
    return Path(__file__).resolve().parents[2]


def load_rules(path: Path) -> dict:
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def compile_rules(cfg: dict) -> list[dict]:
    out = []
    for r in cfg.get("rules", []):
        flags = 0 if r.get("case_sensitive", False) else re.IGNORECASE
        entry = dict(r)
        entry["_re"] = re.compile(r["pattern"], flags)
        ex = r.get("exclude_if_line_matches")
        entry["_ex_re"] = re.compile(ex, re.IGNORECASE) if ex else None
        out.append(entry)
    return out


def is_scannable(p: Path) -> bool:
    if p.name in ALWAYS_SCAN_NAMES:
        return True
    return p.suffix.lower() in TEXT_EXTS


def iter_files(target: Path):
    for p in sorted(target.rglob("*")):
        if not p.is_file():
            continue
        if any(part in SKIP_DIRS for part in p.relative_to(target).parts):
            continue
        if is_scannable(p):
            yield p


def glob_hit(rel: str, patterns: list[str]) -> bool:
    base = rel.rsplit("/", 1)[-1]
    return any(fnmatch.fnmatch(rel, pat) or fnmatch.fnmatch(base, pat) for pat in patterns)


def rule_applies_to_file(rule: dict, rel: str) -> bool:
    exts = rule.get("scope_exts", "all")
    if exts != "all":
        suffix = ("." + rel.rsplit(".", 1)[-1].lower()) if "." in rel else ""
        if suffix not in [e.lower() for e in exts]:
            return False
    prefixes = rule.get("path_prefixes")
    if prefixes:
        matched = any(rel.startswith(pref) or ("/" + rel).find("/" + pref) >= 0 for pref in prefixes)
        mode = rule.get("path_prefix_mode", "only")
        if mode == "exclude" and matched:
            return False
        if mode == "only" and not matched:
            return False
    ex_files = rule.get("exclude_files")
    if ex_files and glob_hit(rel, ex_files):
        return False
    return True


def in_allowlist(allow: list[dict], rel: str, rule_id: str, line_text: str) -> bool:
    for a in allow:
        if a.get("file") and a["file"].replace("\\", "/") != rel:
            continue
        if a.get("rule") and a["rule"] not in (rule_id, "*"):
            continue
        pat = a.get("pattern")
        if pat and pat not in line_text:
            continue
        return True
    return False


def scan(target: Path, rules: list[dict], allow: list[dict], only=None):
    findings: list[dict] = []
    enc_findings: list[dict] = []
    for f in iter_files(target):
        rel = str(f.relative_to(target)).replace("\\", "/")
        if rel.startswith(SELF_EXCLUDE):
            continue
        if only is not None and rel not in only:
            continue
        raw = f.read_bytes()
        if raw.startswith(b"\xef\xbb\xbf"):
            if f.suffix.lower() not in BOM_OK_EXTS:
                enc_findings.append({"file": rel, "line": 0, "rule": "file-bom",
                                     "text": "file has a UTF-8 BOM (expected: no BOM)"})
            body = raw[3:]
        else:
            body = raw
        try:
            text = body.decode("utf-8")
        except UnicodeDecodeError:
            enc_findings.append({"file": rel, "line": 0, "rule": "file-not-utf8",
                                 "text": "file is not valid UTF-8"})
            continue
        lines = text.splitlines()
        for rule in rules:
            if not rule_applies_to_file(rule, rel):
                continue
            for i, line in enumerate(lines, 1):
                if not rule["_re"].search(line):
                    continue
                if rule["_ex_re"] and rule["_ex_re"].search(line):
                    continue
                if in_allowlist(allow, rel, rule["id"], line):
                    continue
                findings.append({"file": rel, "line": i, "rule": rule["id"],
                                 "text": line.strip()[:160]})
    return findings, enc_findings


def report(findings: list[dict], enc: list[dict], target: Path, allow_count: int) -> int:
    total = len(findings) + len(enc)
    print(f"[release-scan] target: {target}")
    print(f"[release-scan] {len(findings)} content hit(s), {len(enc)} encoding hit(s), "
          f"allowlist entries: {allow_count}")
    if total == 0:
        print("[release-scan] PASS - no forbidden content found; target is safe to publish.")
        return 0
    by_rule: dict[str, list[dict]] = {}
    for x in findings:
        by_rule.setdefault(x["rule"], []).append(x)
    for rid in sorted(by_rule):
        print(f"\n  HIT [{rid}]  {len(by_rule[rid])}")
        for x in by_rule[rid]:
            print(f"     {x['file']}:{x['line']}  {x['text']}")
    if enc:
        print(f"\n  WARN [encoding]  {len(enc)}")
        for x in enc:
            print(f"     {x['file']}  {x['text']}")
    print(f"\n[release-scan] FAIL - {total} unwaived hit(s). Clean them or extend the allowlist.")
    return 1


# -------------------------------------------------------------- self test
_SELF_SAMPLES = [
    ("commercial-gate", '#if COMMERCIAL_EDITION\n public bool X => true;'),
    ("paid-feature", "// applies Formant correction on this preset"),
    ("fix-version-trace", "// Fix 2.3: harden parsing"),
    ("internal-review-id", "// L-6 robustness guard here"),
    ("oss-port-provenance", "// Ported from the OSS test suite (DSP/Foo.cs)"),
    ("review-finding-ref", "// Regression guard (review finding M-5)."),
    ("copyright-author", "// Copyright (c) 2099 Example Corp"),
    ("chinese-in-source", "// \u8fd9\u662f\u4e00\u6bb5\u4e2d\u6587\u6ce8\u91ca"),
    ("commercial-wording", "// this watermark variant is commercial-only"),
    ("internal-doc-ref", "<!-- see docs/24 for the breakdown -->"),
]


def self_test(cfg: dict) -> int:
    rules = compile_rules(cfg)
    allow = cfg.get("allowlist", [])
    print("[self-test] probing each rule against a built-in sample ...")
    failures = 0
    tmp = Path(__file__).resolve().parent / "_selftest_tmp"
    tmp.mkdir(exist_ok=True)
    try:
        for rid, content in _SELF_SAMPLES:
            f = tmp / f"probe_{rid}.cs"
            f.write_text(content, encoding="utf-8")
            found, _ = scan(tmp, rules, allow)
            f.unlink(missing_ok=True)
            hit = any(x["rule"] == rid for x in found)
            print(f"  {'PASS' if hit else 'FAIL'}  {rid}")
            if not hit:
                failures += 1
    finally:
        try:
            tmp.rmdir()
        except OSError:
            pass
    if failures:
        print(f"[self-test] FAIL - {failures} rule(s) did not fire; policy may be broken.")
        return 1
    print("[self-test] PASS - all rules fire as expected.")
    return 0


def main():
    here = Path(__file__).resolve().parent
    ap = argparse.ArgumentParser(description="Release content-compliance scanner")
    ap.add_argument("--path", help="scan target dir (default: this repo, auto-located)")
    ap.add_argument("--rules", default=str(here / "release-compliance-rules.json"),
                    help="rules + allowlist JSON path")
    ap.add_argument("--files-from", metavar="PATH",
                    help="scan only the listed paths (one per line; '-' = stdin). For pre-commit.")
    ap.add_argument("--self-test", action="store_true", help="verify rules then exit")
    args = ap.parse_args()

    cfg_path = Path(args.rules)
    if not cfg_path.is_file():
        sys.stderr.write(f"[ERR] rules file not found: {cfg_path}\n")
        return 2
    cfg = load_rules(cfg_path)

    if args.self_test:
        return self_test(cfg)

    target = Path(args.path).resolve() if args.path else find_default_target()
    if not target.is_dir():
        sys.stderr.write(f"[ERR] scan target not found: {target}\n      pass --path <dir>.\n")
        return 2

    rules = compile_rules(cfg)
    allow = cfg.get("allowlist", [])
    only = None
    if args.files_from:
        src = sys.stdin.read() if args.files_from == "-" else Path(args.files_from).read_text(encoding="utf-8")
        only = {l.strip().replace("\\", "/") for l in src.splitlines() if l.strip()}
    findings, enc = scan(target, rules, allow, only)
    return report(findings, enc, target, len(allow))


if __name__ == "__main__":
    sys.exit(main())
