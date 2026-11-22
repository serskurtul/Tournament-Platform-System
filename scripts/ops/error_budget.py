#!/usr/bin/env python3
"""ЛР-12: validate docs/slo.yaml and compute error budgets.

Error Budget = (100% - SLO) x window. For request-based SLIs the minutes are an equivalent of full
downtime at uniform traffic; the request budget (allowed bad requests) is shown when --metrics is used.
--metrics <url|file> evaluates the SLIs from a Prometheus text exposition of the API (/metrics): counters
since process start, so it is a point-in-time SLI over the scrape, not the 30-day window.
Exits 1 if the spec is invalid (missing fields, SLA not stricter than 100, SLA >= SLO).
Usage: error_budget.py [--slo docs/slo.yaml] [--metrics http://localhost:5050/metrics]
"""
from __future__ import annotations

import argparse
import re
import sys
import urllib.request
from pathlib import Path

import yaml

# Label values may contain braces (route="api/v1/tournaments/{id}/image"), so match quoted strings explicitly.
SAMPLE_RE = re.compile(r'^(\w+)\{((?:[^"}]|"(?:[^"\\]|\\.)*")*)\}\s+([0-9.eE+-]+)$')
LABEL_RE = re.compile(r'(\w+)="((?:[^"\\]|\\.)*)"')


def validate(spec: dict) -> list[str]:
    errors = []
    for k in ("service", "owner", "window", "slis", "error_budget_policy"):
        if k not in spec:
            errors.append(f"missing '{k}'")
    for s in spec.get("slis", []):
        for k in ("id", "definition", "good", "total", "slo", "sla"):
            if k not in s:
                errors.append(f"{s.get('id', '?')}: missing '{k}'")
        if "slo" in s and "sla" in s and not (0 < s["sla"] < s["slo"] < 100):
            errors.append(f"{s['id']}: need 0 < SLA < SLO < 100 (SLA is the looser external promise)")
    if len(spec.get("slis", [])) < 3:
        errors.append("at least three SLIs are required")
    return errors


def load_samples(source: str) -> list[tuple[str, dict, float]]:
    text = (urllib.request.urlopen(source, timeout=10).read().decode() if source.startswith("http")
            else Path(source).read_text(encoding="utf-8"))
    out = []
    for line in text.splitlines():
        m = SAMPLE_RE.match(line)
        if m:
            out.append((m.group(1), dict(LABEL_RE.findall(m.group(2))), float(m.group(3))))
    return out


def evaluate(expr: str, samples) -> float:
    """Evaluate sum(increase(metric{matchers}[w])) on cumulative counters (= sum of current values)."""
    m = re.search(r'increase\((\w+)\{(.*)\}\[', expr)
    name, matchers = m.group(1), m.group(2)
    conds = re.findall(r'(\w+)(=~|!~|!=|=)"((?:[^"\\]|\\.)*)"', matchers)

    def ok(labels):
        for key, op, val in conds:
            v = labels.get(key, "")
            full = re.fullmatch(val, v) is not None
            if (op == "=" and v != val) or (op == "!=" and v == val) or (op == "=~" and not full) or (op == "!~" and full):
                return False
        return True
    return sum(v for n, labels, v in samples if n == name and ok(labels))


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--slo", type=Path, default=Path("docs/slo.yaml"))
    ap.add_argument("--metrics")
    args = ap.parse_args()
    spec = yaml.safe_load(args.slo.read_text(encoding="utf-8"))
    errors = validate(spec)
    if errors:
        print("\n".join(f"ERROR  {e}" for e in errors))
        sys.exit(1)
    minutes = spec["window"]["days"] * 24 * 60
    print(f"service {spec['service']}, window {spec['window']['days']} d = {minutes} min\n")
    print(f"{'SLI':14} {'SLO %':>6} {'SLA %':>6} {'buffer pp':>9} {'budget(SLO) min':>15} {'budget(SLA) min':>15}")
    for s in spec["slis"]:
        b_slo = (100 - s["slo"]) / 100 * minutes
        b_sla = (100 - s["sla"]) / 100 * minutes
        print(f"{s['id']:14} {s['slo']:6.2f} {s['sla']:6.2f} {s['slo'] - s['sla']:9.2f} {b_slo:15.1f} {b_sla:15.1f}")
    if args.metrics:
        samples = load_samples(args.metrics)
        print(f"\nSLI from {args.metrics} (counters since process start):")
        print(f"{'SLI':14} {'good':>8} {'total':>8} {'SLI %':>8} {'bad':>6} {'allowed bad':>11} {'budget used %':>13}  status")
        for s in spec["slis"]:
            good, total = evaluate(s["good"], samples), evaluate(s["total"], samples)
            if total == 0:
                print(f"{s['id']:14} {'-':>8} {'0':>8} {'n/a':>8}")
                continue
            sli = good / total * 100
            bad, allowed = total - good, (100 - s["slo"]) / 100 * total
            used = bad / allowed * 100 if allowed else float("inf")
            status = "OK" if sli >= s["slo"] else ("SLO BREACH" if sli >= s["sla"] else "SLA BREACH")
            print(f"{s['id']:14} {good:8.0f} {total:8.0f} {sli:8.2f} {bad:6.0f} {allowed:11.2f} {used:13.0f}  {status}")


if __name__ == "__main__":
    main()
