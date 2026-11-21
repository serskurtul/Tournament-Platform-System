#!/usr/bin/env python3
"""ЛР-9, Завдання 2: Software Economics simulator -> FINANCIAL_LEDGER.md.

Reads sqale_index and ncloc from the SonarCloud report.json (Завдання 1) and environment variables:
  HOURLY_RATE     developer hourly rate, $ (lab range 35..50)
  OVERLOAD_COEFF  company overhead coefficient (lab: 1.3)
  DISCOUNT_RATE   monthly discount rate (lab: 0.01)

Model (formulas from the lab):
  Principal (remediation cost) = sqale_index / 60 * HOURLY_RATE * OVERLOAD_COEFF
  Cost of Delay per month      = ncloc / 100 * HOURLY_RATE       (1 engineer-hour lost per 100 LOC per month)
  NPV_t = sum_{k=1..t} CoD / (1 + DISCOUNT_RATE)^k  -  Principal,   t = 1..6
Payback month = first t with NPV_t > 0. Quality gate: PASS if payback happens within 6 months, else FAIL.

Usage: HOURLY_RATE=40 OVERLOAD_COEFF=1.3 DISCOUNT_RATE=0.01 financial_simulator.py report.json [--out FINANCIAL_LEDGER.md]
"""
from __future__ import annotations

import argparse
import json
import os
import sys
from dataclasses import dataclass
from pathlib import Path

HORIZON_MONTHS = 6


@dataclass(frozen=True)
class Month:
    t: int
    discount_factor: float
    discounted_benefit: float
    cumulative_npv: float


@dataclass(frozen=True)
class Simulation:
    project: str
    ncloc: int
    sqale_index: int
    hourly_rate: float
    overload_coeff: float
    discount_rate: float
    principal: float
    cost_of_delay: float
    months: tuple[Month, ...]
    payback_month: int | None

    @property
    def gate(self) -> str:
        return "PASS" if self.payback_month is not None else "FAIL"


def read_measures(report: dict) -> tuple[str, int, int]:
    comp = report.get("component", {})
    m = {x["metric"]: x.get("value") for x in comp.get("measures", [])}
    try:
        return comp.get("key", "?"), int(float(m["ncloc"])), int(float(m["sqale_index"]))
    except KeyError as missing:
        raise SystemExit(f"report.json has no metric {missing}") from None


def env_float(name: str, default: float, low: float | None = None, high: float | None = None) -> float:
    raw = os.environ.get(name)
    value = float(raw) if raw not in (None, "") else default
    if (low is not None and value < low) or (high is not None and value > high):
        raise SystemExit(f"{name}={value} is outside the allowed range [{low}, {high}]")
    return value


def simulate(project: str, ncloc: int, sqale_index: int,
             hourly_rate: float, overload_coeff: float, discount_rate: float) -> Simulation:
    principal = sqale_index / 60 * hourly_rate * overload_coeff
    cost_of_delay = ncloc / 100 * hourly_rate
    months, cumulative, payback = [], 0.0, None
    for t in range(1, HORIZON_MONTHS + 1):
        factor = 1 / (1 + discount_rate) ** t
        benefit = cost_of_delay * factor
        cumulative += benefit
        npv = cumulative - principal
        months.append(Month(t, factor, benefit, npv))
        if npv > 0 and payback is None:
            payback = t
    return Simulation(project, ncloc, sqale_index, hourly_rate, overload_coeff, discount_rate,
                      principal, cost_of_delay, tuple(months), payback)


def money(x: float) -> str:
    return f"{'-' if x < 0 else ''}${abs(x):,.2f}"


def ledger_markdown(s: Simulation) -> str:
    rows = "\n".join(f"| {m.t} | {m.discount_factor:.4f} | {money(m.discounted_benefit)} | {money(m.cumulative_npv)} |"
                     for m in s.months)
    if s.payback_month is not None:
        verdict = f"Точка виходу в нуль (Payback Period): окупність настає на **{s.payback_month}-му місяці**."
    else:
        verdict = ("Окупність **не настає** за 6 місяців: тіло боргу перевищує дисконтовану вигоду. "
                   "Архітектурне попередження: борг завеликий для одного циклу рефакторингу, "
                   "його треба гасити поетапно або зменшувати sqale_index до запуску інвестиції.")
    return f"""# SOFTWARE ECONOMICS & FINANCIAL QUALITY LEDGER

_Згенеровано `scripts/quality/financial_simulator.py` з `report.json` (SonarCloud API)._

## 1. Вхідні параметри аудиту (System Baseline)

| Параметр | Значення |
|----------|---------:|
| Проєкт (key) | `{s.project}` |
| Масштаб системи (NCLOC) | {s.ncloc:,} |
| Початковий технічний борг (SQALE Index), хв | {s.sqale_index:,} |
| HOURLY_RATE | ${s.hourly_rate:,.2f} |
| OVERLOAD_COEFF | {s.overload_coeff} |
| DISCOUNT_RATE (на місяць) | {s.discount_rate} |

## 2. Капіталізація інвестиційних маркерів

| Маркер | Формула | Значення |
|--------|---------|---------:|
| Тіло боргу (вартість рефакторингу) | sqale_index / 60 × HOURLY_RATE × OVERLOAD_COEFF | {money(s.principal)} |
| Щомісячні збитки від бездіяльності (Cost of Delay) | ncloc / 100 × HOURLY_RATE | {money(s.cost_of_delay)} |

## 3. Динаміка окупності інвестицій (6 ітерацій)

NPV_t = Σ(k=1..t) CoD / (1 + DISCOUNT_RATE)^k − Тіло боргу

| Місяць | Коефіцієнт дисконтування | Щомісячний прибуток (дисконтований CoD) | Кумулятивний NPV |
|---:|---:|---:|---:|
{rows}

## 4. Фінальний інженерно-економічний вердикт

{verdict}

## 5. СТАТУС ВОРІТ ЯКОСТІ (Quality Gate): **{s.gate}**
"""


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("report", type=Path)
    ap.add_argument("--out", type=Path, default=Path("FINANCIAL_LEDGER.md"))
    args = ap.parse_args()

    project, ncloc, sqale_index = read_measures(json.loads(args.report.read_text(encoding="utf-8")))
    sim = simulate(project, ncloc, sqale_index,
                   hourly_rate=env_float("HOURLY_RATE", 40.0, 35.0, 50.0),
                   overload_coeff=env_float("OVERLOAD_COEFF", 1.3, 1.0),
                   discount_rate=env_float("DISCOUNT_RATE", 0.01, 0.0, 1.0))
    args.out.write_text(ledger_markdown(sim), encoding="utf-8")

    print(f"Project {project}: ncloc={ncloc:,}, sqale_index={sqale_index:,} min")
    print(f"Principal = {money(sim.principal)}; Cost of Delay = {money(sim.cost_of_delay)}/month")
    for m in sim.months:
        print(f"  month {m.t}: factor={m.discount_factor:.4f} benefit={money(m.discounted_benefit)} NPV={money(m.cumulative_npv)}")
    print(f"Payback month: {sim.payback_month or 'none within 6 months'}; Quality gate: {sim.gate}")
    print(f"Financial ledger written to {args.out}")
    sys.exit(0)


if __name__ == "__main__":
    main()
