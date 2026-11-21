#!/usr/bin/env python3
"""ЛР-10/11: "code change = documentation change" check.

1. Every HTTP route declared in the ASP.NET controllers must appear in docs/architecture/sdd.md as `METHOD /path`.
2. Every service start-up dependency in docker-compose.yml (depends_on + condition) must appear in
   docs/architecture/isd.md as `service -> dependency (condition)`.
Exits 1 and lists the drift if anything is undocumented (or documented but no longer exists).
Usage: doc_contract_check.py [--repo .]
"""
from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

import yaml

HTTP_RE = re.compile(r'\[Http(Get|Post|Put|Patch|Delete)(?:\("([^"]*)"\))?\]')
ROUTE_RE = re.compile(r'\[Route\("([^"]*)"\)\]')
DOC_ROUTE_RE = re.compile(r"`(GET|POST|PUT|PATCH|DELETE) (/[^`\s]*)`")
DOC_DEP_RE = re.compile(r"`([\w-]+) -> ([\w-]+) \(([\w_]+)\)`")


def normalize(path: str) -> str:
    return re.sub(r"\{(\w+):[^}]+\}", r"{\1}", path.rstrip("/"))


def controller_routes(repo: Path) -> set[str]:
    routes = set()
    for f in sorted((repo / "server").glob("*.API/Controllers/*.cs")):
        text = f.read_text(encoding="utf-8-sig")
        base = (ROUTE_RE.search(text).group(1) if ROUTE_RE.search(text) else "").strip("/")
        for method, sub in HTTP_RE.findall(text):
            if sub.startswith("/"):
                path = sub
            elif sub:
                path = f"/{base}/{sub}"
            else:
                path = f"/{base}"
            routes.add(f"{method.upper()} {normalize(path)}")
    return routes


def compose_deps(repo: Path) -> set[str]:
    data = yaml.safe_load((repo / "docker-compose.yml").read_text(encoding="utf-8"))
    deps = set()
    for name, svc in (data.get("services") or {}).items():
        dep = svc.get("depends_on") or {}
        items = dep.items() if isinstance(dep, dict) else ((d, {}) for d in dep)
        for target, cfg in items:
            deps.add(f"{name} -> {target} ({(cfg or {}).get('condition', 'service_started')})")
    return deps


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--repo", type=Path, default=Path("."))
    args = ap.parse_args()
    repo = args.repo.resolve()

    sdd = (repo / "docs/architecture/sdd.md").read_text(encoding="utf-8") if (repo / "docs/architecture/sdd.md").exists() else ""
    isd = (repo / "docs/architecture/isd.md").read_text(encoding="utf-8") if (repo / "docs/architecture/isd.md").exists() else ""
    documented_routes = {f"{m} {normalize(p)}" for m, p in DOC_ROUTE_RE.findall(sdd)}
    documented_deps = {f"{a} -> {b} ({c})" for a, b, c in DOC_DEP_RE.findall(isd)}

    code_routes, deps = controller_routes(repo), compose_deps(repo)
    problems = {
        "SDD: routes in code but not documented": sorted(code_routes - documented_routes),
        "SDD: documented routes that no longer exist": sorted(documented_routes - code_routes),
        "ISD: start-up dependencies in compose but not documented": sorted(deps - documented_deps),
        "ISD: documented dependencies that no longer exist": sorted(documented_deps - deps),
    }
    print(f"routes in code: {len(code_routes)}, documented: {len(documented_routes)}; "
          f"compose dependencies: {len(deps)}, documented: {len(documented_deps)}")
    drift = False
    for title, items in problems.items():
        if items:
            drift = True
            print(f"DRIFT  {title}:")
            for item in items:
                print(f"         {item}")
    print("RESULT: FAIL (documentation drift)" if drift else "RESULT: PASS (documentation matches code)")
    sys.exit(1 if drift else 0)


if __name__ == "__main__":
    main()
