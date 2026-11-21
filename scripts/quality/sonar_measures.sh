#!/usr/bin/env bash
# ЛР-8, Завдання 1: read object-design metrics from the SonarQube/SonarCloud Web API and keep the raw JSON.
# Usage: SONAR_TOKEN=... SONAR_PROJECT=org_project [SONAR_HOST=https://sonarcloud.io] scripts/quality/sonar_measures.sh [out-dir]
set -euo pipefail
: "${SONAR_TOKEN:?set SONAR_TOKEN}"
: "${SONAR_PROJECT:?set SONAR_PROJECT (project key)}"
HOST="${SONAR_HOST:-https://sonarcloud.io}"
OUT="${1:-sonar-report}"
mkdir -p "$OUT"

# Three files with different architectural roles (Core Domain, Infrastructure, Utility/DTO).
FILES=(
  "server/TournamentPlatformSystemWebApi.Core/Logic/SingleEliminationBracketGenerator.cs"
  "server/TournamentPlatformSystemWebApi.Infrastructure/Repositories/TournamentRepository.cs"
  "server/TournamentPlatformSystemWebApi.Application/DTOs/TournamentCreateDto.cs"
)
LAB_KEYS="class_complexity,coupling_between_objects,lcom4"            # keys requested by the lab
AVAILABLE_KEYS="complexity,cognitive_complexity,functions,classes,ncloc,code_smells"

query() { # $1 = component key, $2 = metric keys, $3 = output file
  local url="$HOST/api/measures/component?component=$1&metricKeys=$2"
  echo "curl -u \$SONAR_TOKEN: \"$url\"" | tee -a "$OUT/requests.log"
  curl -sS -u "$SONAR_TOKEN:" "$url" -o "$3" -w "  -> HTTP %{http_code}\n" | tee -a "$OUT/requests.log"
}

i=0
for f in "${FILES[@]}"; do
  i=$((i + 1))
  query "$SONAR_PROJECT:$f" "$LAB_KEYS" "$OUT/file$i-lab-keys.json"
  query "$SONAR_PROJECT:$f" "$AVAILABLE_KEYS" "$OUT/file$i-available.json"
done
query "$SONAR_PROJECT" "$AVAILABLE_KEYS,duplicated_lines_density,sqale_index,sqale_debt_ratio" "$OUT/project.json"

# Compact table from the raw JSON (errors are shown as returned by the API).
python3 - "$OUT" <<'PY'
import json, pathlib, sys
out = pathlib.Path(sys.argv[1])
for p in sorted(out.glob("*.json")):
    d = json.loads(p.read_text() or "{}")
    if "errors" in d:
        print(f"{p.name:28s} API error: {'; '.join(e.get('msg', '') for e in d['errors'])}")
        continue
    comp = d.get("component", {})
    measures = {m["metric"]: m.get("value") for m in comp.get("measures", [])}
    print(f"{p.name:28s} {comp.get('path', comp.get('key', ''))}: {measures}")
PY
