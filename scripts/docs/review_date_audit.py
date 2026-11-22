#!/usr/bin/env python3
"""ЛР-11: front matter audit for the documentation site.

Every Markdown page that mkdocs builds (docs/ minus exclude_docs) must start with front matter that has
owner, status (draft|in-review|published|deprecated), review_date (YYYY-MM-DD) and tags.
A page whose review_date is in the past is overdue: the owner must re-check it and move the date.
Exits 1 on a missing/invalid field or an overdue page.
Usage: review_date_audit.py [--as-of 2026-11-21] [--warn-days 14]
"""
from __future__ import annotations

import argparse
import sys
from datetime import date
from pathlib import Path

import yaml

EXCLUDED = ("labs/", "quality-labs/", "marketing/")
REQUIRED = ("owner", "status", "review_date", "tags")
STATUSES = {"draft", "in-review", "published", "deprecated"}


def front_matter(path: Path) -> dict | None:
    text = path.read_text(encoding="utf-8")
    if not text.startswith("---"):
        return None
    end = text.find("\n---", 3)
    return yaml.safe_load(text[3:end]) if end != -1 else None


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--docs", type=Path, default=Path("docs"))
    ap.add_argument("--as-of", type=date.fromisoformat, default=date.today())
    ap.add_argument("--warn-days", type=int, default=14)
    args = ap.parse_args()

    errors, warnings, rows = [], [], []
    for page in sorted(args.docs.rglob("*.md")):
        rel = page.relative_to(args.docs).as_posix()
        if rel.startswith(EXCLUDED):
            continue
        fm = front_matter(page)
        if fm is None:
            errors.append(f"{rel}: no front matter")
            continue
        missing = [k for k in REQUIRED if not fm.get(k)]
        if missing:
            errors.append(f"{rel}: missing {', '.join(missing)}")
            continue
        if fm["status"] not in STATUSES:
            errors.append(f"{rel}: status '{fm['status']}' not in {sorted(STATUSES)}")
        review = fm["review_date"] if isinstance(fm["review_date"], date) else date.fromisoformat(str(fm["review_date"]))
        left = (review - args.as_of).days
        if left < 0 and fm["status"] != "deprecated":
            errors.append(f"{rel}: review overdue by {-left} d (owner: {fm['owner']})")
        elif left <= args.warn_days:
            warnings.append(f"{rel}: review due in {left} d (owner: {fm['owner']})")
        rows.append((rel, fm["owner"], fm["status"], review, left))

    print(f"{'page':58} {'owner':18} {'status':11} review_date  days left")
    for rel, owner, status, review, left in rows:
        print(f"{rel:58} {owner:18} {status:11} {review}  {left}")
    for w in warnings:
        print(f"WARN   {w}")
    for e in errors:
        print(f"ERROR  {e}")
    print(f"RESULT: {'FAIL' if errors else 'PASS'} ({len(rows)} pages, {len(errors)} errors, {len(warnings)} warnings)")
    sys.exit(1 if errors else 0)


if __name__ == "__main__":
    main()
