#!/usr/bin/env python3
"""ЛР-10: Documentation Decay audit from Git history.

For every documentation artifact in REGISTRY:
  * Last Updated Age      git log -1 --format=%cs -- <doc>   -> days before --as-of (90 suspicious, 180 stale)
  * Code-Doc Change Ratio commits to the related code component since the doc was last updated
                          (git log --since=<doc date> --oneline -- <component> | wc -l); > 20 = structural drift
  * Broken links          relative Markdown links / images that point to files that do not exist
  * Inbound references    other tracked text files that link to the doc (proxy for access evidence)
Usage: doc_audit.py [--as-of 2026-11-21] [--json out.json] [--md out.md]
"""
from __future__ import annotations

import argparse
import json
import re
import subprocess
from datetime import date
from pathlib import Path

# doc path -> (type, category, audience, related code component paths)
REGISTRY = {
    "README.md": ("продукт", "експлуатаційна", "новачок, бізнес, DevOps", ["server", "client/src", "docker-compose.yml"]),
    "CONTRIBUTORS.md": ("процес", "—", "команда", []),
    "client/README.md": ("продукт", "технічна", "розробники (фронтенд)", ["client/src"]),
    "docs/ADR_System_Style.md": ("процес", "архітектурна", "розробники", ["server"]),
    "docs/SAD_Internal_View.md": ("продукт", "архітектурна", "розробники", ["server"]),
    "docs/DATA_DICTIONARY.md": ("продукт", "технічна", "розробники, QA", ["server/TournamentPlatformSystemWebApi.Infrastructure/Entities", "db"]),
    "docs/Storage/Data_Integrity.md": ("продукт", "технічна", "розробники", ["server/TournamentPlatformSystemWebApi.Infrastructure/Context", "server/TournamentPlatformSystemWebApi.Infrastructure/Migrations"]),
    "docs/Data_Flow_Specification.md": ("продукт", "архітектурна", "розробники, QA", ["server/TournamentPlatformSystemWebApi.API/Controllers", "client/src/services"]),
    "docs/ErrorCatalog.md": ("продукт", "технічна", "розробники, QA", ["server/TournamentPlatformSystemWebApi.Common/Exceptions", "server/TournamentPlatformSystemWebApi.API/Middleware"]),
    "docs/NAMING_CONVENTIONS.md": ("процес", "технічна", "розробники", []),
    "docs/TestPlan.md": ("процес", "технічна", "QA", ["server/Tests", "server/IntegrationTests"]),
    "k8s/README.md": ("продукт", "експлуатаційна", "DevOps", ["k8s"]),
    "charts/tournament-platform/README.md": ("продукт", "експлуатаційна", "DevOps", ["charts/tournament-platform/templates", "charts/tournament-platform/values.yaml"]),
    "docs/labs/README.md": ("процес", "експлуатаційна", "DevOps, викладач", [".github/workflows", "argocd-apps", "bootstrap"]),
    ".github/PULL_REQUEST_TEMPLATE.md": ("процес", "—", "розробники", []),
}
# Generated-by-design or process records are listed but their age is informational only.
LINK_RE = re.compile(r"!?\[[^\]]*\]\(([^)\s]+)\)")


def git(*args: str) -> str:
    return subprocess.run(["git", *args], check=True, capture_output=True, text=True).stdout.strip()


def broken_links(doc: Path) -> list[str]:
    broken = []
    for target in LINK_RE.findall(doc.read_text(encoding="utf-8", errors="replace")):
        if re.match(r"^(https?:|mailto:|#)", target):
            continue
        path = (doc.parent / target.split("#")[0]).resolve()
        if target.split("#")[0] and not path.exists():
            broken.append(target)
    return broken


def inbound_refs(doc: str, tracked: list[str]) -> int:
    name = Path(doc).name
    count = 0
    for f in tracked:
        if f == doc or not f.endswith((".md", ".yml", ".yaml", ".vue", ".ts", ".cs")):
            continue
        try:
            if name in Path(f).read_text(encoding="utf-8", errors="ignore"):
                count += 1
        except OSError:
            pass
    return count


def audit(as_of: date) -> list[dict]:
    tracked = git("ls-files").splitlines()
    rows = []
    for doc, (dtype, category, audience, components) in REGISTRY.items():
        if not Path(doc).exists():
            continue
        last = git("log", "-1", "--format=%cs", "--", doc)
        age = (as_of - date.fromisoformat(last)).days if last else None
        doc_commits = int(git("rev-list", "--count", "HEAD", "--", doc) or 0)
        code_since = (len(git("log", f"--since={last}", "--oneline", "--", *components).splitlines())
                      if components and last else None)
        flags = []
        if age is not None and age > 180:
            flags.append("stale>180d")
        elif age is not None and age > 90:
            flags.append("suspicious>90d")
        if code_since is not None and code_since > 20:
            flags.append("drift>20")
        links = broken_links(Path(doc))
        if links:
            flags.append(f"broken-links:{len(links)}")
        rows.append({"doc": doc, "type": dtype, "category": category, "audience": audience,
                     "last_updated": last, "age_days": age, "doc_commits": doc_commits,
                     "code_commits_since_doc": code_since, "broken_links": links,
                     "inbound_refs": inbound_refs(doc, tracked), "flags": flags})
    return rows


def to_md(rows: list[dict], as_of: date) -> str:
    out = [f"Audit date: {as_of}. Thresholds: age > 90 d suspicious, > 180 d stale; > 20 code commits since last doc update = drift.",
           "", "| Документ | Тип | Категорія | Оновлено | Вік, дн | Комітів коду після оновлення | Битих посилань | Вхідних посилань | Сигнали |",
           "|---|---|---|---|---:|---:|---:|---:|---|"]
    for r in rows:
        out.append(f"| `{r['doc']}` | {r['type']} | {r['category']} | {r['last_updated']} | {r['age_days']} | "
                   f"{'—' if r['code_commits_since_doc'] is None else r['code_commits_since_doc']} | {len(r['broken_links'])} | "
                   f"{r['inbound_refs']} | {', '.join(r['flags']) or 'ok'} |")
    return "\n".join(out) + "\n"


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--as-of", type=date.fromisoformat, default=date.today())
    ap.add_argument("--json", type=Path)
    ap.add_argument("--md", type=Path)
    args = ap.parse_args()
    rows = audit(args.as_of)
    md = to_md(rows, args.as_of)
    print(md)
    if args.json:
        args.json.write_text(json.dumps(rows, ensure_ascii=False, indent=2), encoding="utf-8")
    if args.md:
        args.md.write_text(md, encoding="utf-8")


if __name__ == "__main__":
    main()
