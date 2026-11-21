#!/usr/bin/env python3
"""ЛР-9, Завдання 1: independent SQALE audit of a SonarCloud measures JSON.

Input: the raw response of
  GET https://sonarcloud.io/api/measures/component?component=<key>&metricKeys=sqale_index,ncloc,sqale_rating

Computes:
  debt hours  = sqale_index / 60
  debt days   = debt hours / 8
  TDD (%)     = debt hours / (ncloc * 0.5) * 100      (SQALE: 30 min to write one line from scratch)
and checks the letter against the SQALE scale (A <= 5%, B <= 10%, C <= 20%, D <= 50%, E > 50%).
SonarCloud returns sqale_rating as a number: 1.0 = A ... 5.0 = E.

Usage: sqale_audit.py report.json [--out SQALE_AUDIT.md]
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path

RATING_LETTERS = {1: "A", 2: "B", 3: "C", 4: "D", 5: "E"}


def measures(report: dict) -> dict[str, str]:
    comp = report.get("component", {})
    return {m["metric"]: m.get("value") for m in comp.get("measures", [])}


def letter_for_tdd(tdd_percent: float) -> str:
    if tdd_percent <= 5:
        return "A"
    if tdd_percent <= 10:
        return "B"
    if tdd_percent <= 20:
        return "C"
    if tdd_percent <= 50:
        return "D"
    return "E"


def audit(report: dict) -> dict:
    m = measures(report)
    missing = [k for k in ("sqale_index", "ncloc") if m.get(k) is None]
    if missing:
        raise SystemExit(f"report.json lacks metrics: {', '.join(missing)}")
    sqale_index = float(m["sqale_index"])
    ncloc = float(m["ncloc"])
    debt_hours = sqale_index / 60
    debt_days = debt_hours / 8
    system_hours = ncloc * 0.5
    tdd = debt_hours / system_hours * 100 if system_hours else 0.0
    cloud_letter = RATING_LETTERS.get(round(float(m["sqale_rating"]))) if m.get("sqale_rating") else None
    computed_letter = letter_for_tdd(tdd)
    return {
        "project": report.get("component", {}).get("key", "?"),
        "name": report.get("component", {}).get("name", "?"),
        "ncloc": int(ncloc),
        "sqale_index_min": int(sqale_index),
        "debt_hours": round(debt_hours, 1),
        "debt_days": round(debt_days, 1),
        "system_hours": round(system_hours, 1),
        "tdd_percent": round(tdd, 2),
        "computed_rating": computed_letter,
        "cloud_rating": cloud_letter,
        "match": cloud_letter == computed_letter if cloud_letter else None,
    }


def to_markdown(a: dict) -> str:
    verdict = ("збігається з хмарним рейтингом" if a["match"]
               else "не збігається з хмарним рейтингом" if a["match"] is False else "хмарний рейтинг відсутній")
    return "\n".join([
        f"# SQALE Audit Ledger: {a['name']} (`{a['project']}`)",
        "",
        "| Масштаб (ncloc) | Борг, хв (sqale_index) | Борг, людино-годин | Борг, робочих днів | Обсяг системи, год (ncloc×0.5) | TDD, % | Рейтинг за TDD | sqale_rating (хмара) |",
        "|---:|---:|---:|---:|---:|---:|:---:|:---:|",
        f"| {a['ncloc']:,} | {a['sqale_index_min']:,} | {a['debt_hours']:,} | {a['debt_days']:,} | {a['system_hours']:,} "
        f"| {a['tdd_percent']} | {a['computed_rating']} | {a['cloud_rating'] or '—'} |",
        "",
        f"Формули: години = sqale_index / 60; дні = години / 8; TDD = години / (ncloc × 0.5) × 100%.",
        f"Висновок: розрахований рейтинг {a['computed_rating']} {verdict}.",
        "",
    ])


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("report", type=Path)
    ap.add_argument("--out", type=Path, default=None)
    args = ap.parse_args()
    result = audit(json.loads(args.report.read_text(encoding="utf-8")))
    print(json.dumps(result, ensure_ascii=False, indent=2))
    if args.out:
        args.out.write_text(to_markdown(result), encoding="utf-8")
        print(f"SQALE audit written to {args.out}")


if __name__ == "__main__":
    main()
