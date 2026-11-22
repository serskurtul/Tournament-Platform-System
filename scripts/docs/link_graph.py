#!/usr/bin/env python3
"""ЛР-11: knowledge-graph connectivity of the documentation site (the same graph Obsidian draws from links).

Nodes are the site pages (docs/ minus excluded folders), edges are relative Markdown links between them
(direction ignored). Reports connected components and pages with no links at all ("islands").
Exits 1 if the graph is not a single component.
Usage: link_graph.py [--mermaid out.md]
"""
from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

DOCS = Path("docs")
EXCLUDED = ("labs/", "quality-labs/", "marketing/")
LINK_RE = re.compile(r"\]\(([^)\s#]+\.md)(?:#[^)]*)?\)")


def pages() -> list[str]:
    return sorted(p.relative_to(DOCS).as_posix() for p in DOCS.rglob("*.md")
                  if not p.relative_to(DOCS).as_posix().startswith(EXCLUDED))


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--mermaid", type=Path)
    args = ap.parse_args()
    nodes = pages()
    known = set(nodes)
    edges = set()
    for page in nodes:
        for target in LINK_RE.findall((DOCS / page).read_text(encoding="utf-8")):
            if target.startswith("http"):
                continue
            resolved = (DOCS / page).parent.joinpath(target).resolve().relative_to(DOCS.resolve()).as_posix()
            if resolved in known and resolved != page:
                edges.add(tuple(sorted((page, resolved))))
    adj = {n: set() for n in nodes}
    for a, b in edges:
        adj[a].add(b)
        adj[b].add(a)
    seen, components = set(), []
    for n in nodes:
        if n in seen:
            continue
        stack, comp = [n], []
        while stack:
            cur = stack.pop()
            if cur in seen:
                continue
            seen.add(cur)
            comp.append(cur)
            stack.extend(adj[cur] - seen)
        components.append(sorted(comp))
    islands = [n for n in nodes if not adj[n]]
    print(f"pages: {len(nodes)}, links: {len(edges)}, components: {len(components)}, islands: {len(islands)}")
    for n in nodes:
        print(f"  {len(adj[n]):2d}  {n}")
    for i, comp in enumerate(components[1:], 2):
        print(f"DETACHED component {i}: {', '.join(comp)}")
    if args.mermaid:
        ids = {n: f"n{i}" for i, n in enumerate(nodes)}
        lines = ["```mermaid", "graph LR"] + [f'  {ids[n]}["{n}"]' for n in nodes] + [f"  {ids[a]} --- {ids[b]}" for a, b in sorted(edges)] + ["```", ""]
        args.mermaid.write_text("\n".join(lines), encoding="utf-8")
    ok = len(components) == 1
    print(f"RESULT: {'PASS (single connected knowledge graph)' if ok else 'FAIL (isolated pages)'}")
    sys.exit(0 if ok else 1)


if __name__ == "__main__":
    main()
