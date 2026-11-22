#!/usr/bin/env bash
# ЛР-12 INC-001: controlled outage of the banner storage (Google Cloud Storage) dependency.
# Precondition: the API runs with storage configured, but Google rejects the service-account key
# (revoked / rotated key: run the API with a throwaway key, GoogleCloud__ServiceAccountJson). A blocked egress
# to *.googleapis.com goes through the same failure path (exception -> HTTP 500).
# The script plays the traffic of one "evening": ordinary catalogue browsing plus organizers uploading
# banners, then prints what the alerting and the SLOs saw.
# Usage: scripts/ops/simulate_storage_outage.sh [base_url] [browse_requests] [uploads]
set -euo pipefail
BASE="${1:-http://localhost:5050}"; BROWSE="${2:-400}"; UPLOADS="${3:-12}"
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
utc() { date -u +%H:%M:%S; }
json() { python3 -c "import sys,json; d=json.load(sys.stdin); print(eval(sys.argv[1]))" "$1"; }

ORG="incident.organizer@example.com"; PASS="Incident#2026"
curl -s -o /dev/null -X POST "$BASE/api/v1/auth/register" -H 'Content-Type: application/json' \
  -d "{\"email\":\"$ORG\",\"password\":\"$PASS\",\"fullName\":\"Incident Organizer\",\"role\":\"organizer\",\"dateOfBirth\":\"1995-01-01\",\"phoneNumber\":\"+380661234511\"}"
TOKEN=$(curl -s -X POST "$BASE/api/v1/auth/login" -H 'Content-Type: application/json' \
  -d "{\"email\":\"$ORG\",\"password\":\"$PASS\",\"rememberMe\":false}" | json 'd["tokens"]["accessToken"]')
SPORT=$(curl -s "$BASE/api/v1/sport" | json 'd[0]["id"]')
TID=$(curl -s -X POST "$BASE/api/v1/tournaments" -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d "{\"title\":\"Incident Cup $(date -u +%H%M%S)\",\"startDate\":\"2027-03-10T10:00:00Z\",\"endDate\":\"2027-03-12T10:00:00Z\",\"registrationCloseDate\":\"2027-03-01T23:59:59Z\",\"sport\":\"$SPORT\",\"maxParticipants\":16}" | json 'd["id"]')
echo "$(utc) setup: organizer $ORG, tournament $TID"

python3 -c "import base64,sys; sys.stdout.buffer.write(base64.b64decode('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg=='))" > /tmp/banner.png

echo "$(utc) traffic start: $BROWSE catalogue requests + $UPLOADS banner uploads"
for i in $(seq 1 "$UPLOADS"); do
  for _ in $(seq 1 $(( BROWSE / UPLOADS ))); do
    curl -s -o /dev/null "$BASE/api/v1/tournaments?page=1&pageSize=10" &
    curl -s -o /dev/null "$BASE/api/v1/sport"
  done
  wait
  code=$(curl -s -o /tmp/upload.json -w '%{http_code}' -X POST "$BASE/api/v1/tournaments/$TID/image" \
    -H "Authorization: Bearer $TOKEN" -F "file=@/tmp/banner.png;type=image/png")
  echo "$(utc) upload #$i -> HTTP $code $(head -c 160 /tmp/upload.json)"
done
echo "$(utc) traffic end"
echo "$(utc) /health -> $(curl -s -o /dev/null -w '%{http_code}' "$BASE/health")"
[ -n "${API_LOG:-}" ] && echo "$(utc) error-level log lines for banner uploads: $(grep -c 'Banner storage unavailable\|Banner upload failed' "$API_LOG")"

curl -s "$BASE/metrics" > /tmp/incident-metrics.txt
python3 - <<'PY'
import re
tot=bad=0; up_tot=up_bad=0
for line in open('/tmp/incident-metrics.txt'):
    m=re.match(r'http_requests_total\{method="(\w+)",route="([^"]*)",status="(\d+)"\} ([0-9.e+]+)', line)
    if not m or m.group(2) in ('health','version','metrics'): continue
    n=float(m.group(4)); tot+=n; bad+=n if m.group(3).startswith('5') else 0
    if m.group(2)=='api/v1/tournaments/{id}/image': up_tot+=n; up_bad+=n if m.group(3).startswith('5') else 0
share=bad/tot*100
print(f"alert TournamentApiHighErrorRate: 5xx share {share:.2f}% of {tot:.0f} requests -> {'FIRING' if share>5 else 'NOT FIRING (threshold 5%)'}")
print(f"banner uploads: {up_tot:.0f} total, {up_bad:.0f} with 5xx")
ratio = up_bad / max(up_tot, 1)
print(f"alert TournamentBannerUploadFailing (CA-2): 5xx ratio {ratio:.0%}, attempts {up_tot:.0f} -> {'FIRING' if ratio > 0.5 and up_tot >= 3 else 'NOT FIRING'}")
PY
python3 "$ROOT/scripts/ops/error_budget.py" --metrics /tmp/incident-metrics.txt | sed -n '/^SLI from/,$p'
