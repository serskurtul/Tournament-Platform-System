#!/usr/bin/env python3
"""ЛР-7, Завдання 3: синергетична матриця Hotspots.

Joins static complexity (Lizard CSV: McCabe CCN per function) with process metrics
(Git churn: number of commits that touched each file) and classifies every file into
four risk zones:

    High Risk (Hotspot)      complexity high, churn high
    Sleeping Danger          complexity high, churn low
    Active Development       complexity low,  churn high
    Stable Code              complexity low,  churn low

"Complexity high" = max function CCN in the file > --cc-threshold (default 10, per the lab).
"Churn high" = commits >= --churn-threshold; if omitted, the 75th percentile of churn
among analysed files is used (data-driven threshold, reported in the output).

Outputs (in --out-dir): complexity.json (per function), hotspots.json, hotspots.csv,
hotspots.md (Markdown table sorted by risk).
"""
from __future__ import annotations

import argparse
import csv
import json
import statistics
import subprocess
from collections import Counter, defaultdict
from pathlib import Path

EXCLUDE_PARTS = ("/Migrations/", "/Tests/", "/IntegrationTests/", "/bin/", "/obj/", "node_modules/", "/dist/")
EXCLUDE_SUFFIXES = (".Designer.cs", "ModelSnapshot.cs")
CODE_SUFFIXES = (".cs", ".ts", ".vue", ".js")

ZONES = {
    (True, True): "High Risk (Hotspot)",
    (True, False): "Sleeping Danger",
    (False, True): "Active Development",
    (False, False): "Stable Code",
}
ZONE_ORDER = ["High Risk (Hotspot)", "Sleeping Danger", "Active Development", "Stable Code"]


def is_code(path: str) -> bool:
    p = "/" + path
    return path.endswith(CODE_SUFFIXES) and not path.endswith(EXCLUDE_SUFFIXES) and not any(x in p for x in EXCLUDE_PARTS)


def read_lizard(csv_path: Path, repo_root: Path) -> list[dict]:
    # Lizard --csv columns: NLOC, CCN, token, PARAM, length, location, file, function, long_name, start, end
    functions = []
    with csv_path.open(encoding="utf-8", errors="replace") as fh:
        for row in csv.reader(fh):
            if len(row) < 11 or not row[1].isdigit():
                continue
            file_path = Path(row[6])
            try:
                rel = file_path.resolve().relative_to(repo_root).as_posix()
            except ValueError:
                rel = row[6].lstrip("./")
            if not is_code(rel):
                continue
            functions.append({
                "file": rel, "function": row[7], "nloc": int(row[0]), "ccn": int(row[1]),
                "tokens": int(row[2]), "params": int(row[3]), "start": int(row[9]), "end": int(row[10]),
            })
    return functions


def git_churn(repo_root: Path, since: str | None) -> Counter:
    cmd = ["git", "-C", str(repo_root), "log", "--format=format:", "--name-only"]
    if since:
        cmd.append(f"--since={since}")
    out = subprocess.run(cmd, check=True, capture_output=True, text=True).stdout
    return Counter(line for line in out.splitlines() if line and is_code(line))


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--lizard-csv", required=True, type=Path)
    ap.add_argument("--repo-root", default=".", type=Path)
    ap.add_argument("--out-dir", default="quality-report", type=Path)
    ap.add_argument("--since", default=None, help="git --since window, e.g. '90 days ago' (default: whole history)")
    ap.add_argument("--cc-threshold", type=int, default=10)
    ap.add_argument("--churn-threshold", type=int, default=None)
    args = ap.parse_args()

    repo_root = args.repo_root.resolve()
    args.out_dir.mkdir(parents=True, exist_ok=True)

    functions = read_lizard(args.lizard_csv, repo_root)
    churn = git_churn(repo_root, args.since)

    per_file: dict[str, dict] = defaultdict(lambda: {"functions": 0, "max_ccn": 0, "sum_ccn": 0, "nloc": 0})
    for f in functions:
        agg = per_file[f["file"]]
        agg["functions"] += 1
        agg["max_ccn"] = max(agg["max_ccn"], f["ccn"])
        agg["sum_ccn"] += f["ccn"]
        agg["nloc"] += f["nloc"]

    analysed = sorted(per_file)
    churn_values = [churn.get(path, 0) for path in analysed]
    if args.churn_threshold is not None:
        churn_threshold = args.churn_threshold
    else:
        churn_threshold = max(1, int(statistics.quantiles(churn_values, n=4)[2])) if len(churn_values) >= 4 else 1

    rows = []
    for path in analysed:
        agg = per_file[path]
        commits = churn.get(path, 0)
        high_cc = agg["max_ccn"] > args.cc_threshold
        high_churn = commits >= churn_threshold
        rows.append({
            "file": path, "max_ccn": agg["max_ccn"], "sum_ccn": agg["sum_ccn"], "functions": agg["functions"],
            "nloc": agg["nloc"], "commits": commits, "zone": ZONES[(high_cc, high_churn)],
            # Simple ranking score: complexity weighted by change frequency.
            "risk_score": agg["max_ccn"] * commits,
        })
    rows.sort(key=lambda r: (ZONE_ORDER.index(r["zone"]), -r["risk_score"], -r["max_ccn"], -r["commits"]))

    meta = {
        "cc_threshold": args.cc_threshold, "churn_threshold": churn_threshold,
        "churn_window": args.since or "whole history", "files_analysed": len(rows),
        "functions_analysed": len(functions),
        "zone_counts": {z: sum(r["zone"] == z for r in rows) for z in ZONE_ORDER},
    }

    (args.out_dir / "complexity.json").write_text(json.dumps(functions, indent=2, ensure_ascii=False), encoding="utf-8")
    (args.out_dir / "hotspots.json").write_text(json.dumps({"meta": meta, "files": rows}, indent=2, ensure_ascii=False), encoding="utf-8")
    with (args.out_dir / "hotspots.csv").open("w", newline="", encoding="utf-8") as fh:
        writer = csv.DictWriter(fh, fieldnames=list(rows[0].keys()) if rows else ["file"])
        writer.writeheader()
        writer.writerows(rows)

    lines = [
        f"Window: {meta['churn_window']}; complexity threshold: max CCN > {args.cc_threshold}; "
        f"churn threshold: >= {churn_threshold} commits; files: {meta['files_analysed']}; functions: {meta['functions_analysed']}.",
        "",
        "| Файл | Max CCN | Σ CCN | Комітів | Категорія ризику |",
        "|------|--------:|------:|--------:|------------------|",
    ]
    lines += [f"| `{r['file']}` | {r['max_ccn']} | {r['sum_ccn']} | {r['commits']} | {r['zone']} |" for r in rows]
    (args.out_dir / "hotspots.md").write_text("\n".join(lines) + "\n", encoding="utf-8")

    print(json.dumps(meta, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
