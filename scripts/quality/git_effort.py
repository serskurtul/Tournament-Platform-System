#!/usr/bin/env python3
"""ЛР-9, Завдання 3: retrospective effort estimate for a sub-module from Git history ("git-hours" method).

For every author, commits touching the given paths are sorted by time and grouped into work sessions:
consecutive commits closer than --max-gap minutes belong to one session and the gap between them is
counted as work; each session additionally gets --first-commit minutes for the work done before its
first commit. Merge commits are ignored.

Usage:
  git_effort.py --until 2026-11-01 -- <pathspec>...
Prints a per-author table and the total person-hours (JSON with --json).
"""
from __future__ import annotations

import argparse
import json
import subprocess
from collections import defaultdict
from datetime import datetime


def historical_paths(paths: list[str]) -> list[str]:
    """Expand directories to tracked files and add every former name of each file (git log --follow)."""
    files = subprocess.run(["git", "ls-files", "--"] + paths, check=True, capture_output=True, text=True).stdout.split()
    names = set(files)
    for f in files:
        out = subprocess.run(["git", "log", "--follow", "--name-only", "--format=", "--", f],
                             check=True, capture_output=True, text=True).stdout
        names.update(line for line in out.splitlines() if line.strip())
    return sorted(names)


def commits(paths: list[str], since: str | None, until: str | None) -> list[tuple[str, datetime, str]]:
    cmd = ["git", "log", "--no-merges", "--full-history", "--format=%H|%aN|%aI"]
    if since:
        cmd.append(f"--since={since}")
    if until:
        cmd.append(f"--until={until}")
    out = subprocess.run(cmd + ["--"] + paths, check=True, capture_output=True, text=True).stdout
    rows = []
    for line in out.splitlines():
        sha, author, iso = line.split("|", 2)
        rows.append((author, datetime.fromisoformat(iso), sha))
    return rows


def estimate(rows, max_gap_min: int, first_commit_min: int) -> dict:
    by_author: dict[str, list[datetime]] = defaultdict(list)
    for author, ts, _ in rows:
        by_author[author].append(ts)
    result = {}
    for author, stamps in by_author.items():
        stamps.sort()
        minutes, sessions = float(first_commit_min), 1
        for prev, cur in zip(stamps, stamps[1:]):
            gap = (cur - prev).total_seconds() / 60
            if gap <= max_gap_min:
                minutes += gap
            else:
                minutes += first_commit_min
                sessions += 1
        result[author] = {"commits": len(stamps), "sessions": sessions, "hours": round(minutes / 60, 1)}
    return result


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--since")
    ap.add_argument("--until")
    ap.add_argument("--max-gap", type=int, default=120, help="minutes between commits within one session")
    ap.add_argument("--first-commit", type=int, default=60, help="minutes credited before the first commit of a session")
    ap.add_argument("--json", action="store_true")
    ap.add_argument("--follow", action="store_true", help="also count commits made under former file names")
    ap.add_argument("paths", nargs="+")
    args = ap.parse_args()

    paths = historical_paths(args.paths) if args.follow else args.paths
    rows = commits(paths, args.since, args.until)
    per_author = estimate(rows, args.max_gap, args.first_commit)
    total = round(sum(a["hours"] for a in per_author.values()), 1)
    summary = {"commits": len(rows), "authors": len(per_author), "person_hours": total,
               "params": {"max_gap_min": args.max_gap, "first_commit_min": args.first_commit, "follow": args.follow,
                          "paths_counted": len(paths),
                          "since": args.since, "until": args.until},
               "per_author": per_author}
    if args.json:
        print(json.dumps(summary, ensure_ascii=False, indent=2))
        return
    print(f"{'author':28s} {'commits':>7} {'sessions':>8} {'hours':>7}")
    for author, a in sorted(per_author.items(), key=lambda kv: -kv[1]["hours"]):
        print(f"{author:28s} {a['commits']:>7} {a['sessions']:>8} {a['hours']:>7}")
    print(f"{'TOTAL':28s} {len(rows):>7} {'':>8} {total:>7}")
    print(f"params: max gap {args.max_gap} min, first-commit credit {args.first_commit} min, "
          f"window {args.since or 'start'} .. {args.until or 'now'}")


if __name__ == "__main__":
    main()
