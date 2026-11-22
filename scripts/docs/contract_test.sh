#!/usr/bin/env bash
# ЛР-11: контрактний тест. Schemathesis генерує запити з docs/api/openapi.yaml і перевіряє,
# що відповіді запущеного API (статус-коди, content-type, JSON-схема) збігаються з контрактом.
# NUL-байт у шляху вимкнено: Kestrel відхиляє такий запит порожнім 400 ще до застосунку (поза контрактом API).
# Usage: scripts/docs/contract_test.sh [base_url]   (за замовчуванням http://localhost:5050)
set -euo pipefail
BASE_URL="${1:-http://localhost:5050}"
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
# Test user from the login example in the contract (201 on first run, 409 afterwards - both fine).
curl -s -o /dev/null -w "register contract user: HTTP %{http_code}\n" -X POST "$BASE_URL/api/v1/auth/register" \
  -H 'Content-Type: application/json' \
  -d '{"email":"contract.player@example.com","password":"Contract#2026","fullName":"Contract Player","role":"player","dateOfBirth":"2000-01-01","phoneNumber":"+380661234500"}'
mkdir -p "$ROOT/build"
st run "$ROOT/docs/api/openapi.yaml" --base-url "$BASE_URL" \
  --checks not_a_server_error,status_code_conformance,content_type_conformance,response_schema_conformance \
  --hypothesis-max-examples "${MAX_EXAMPLES:-50}" --hypothesis-seed 11 \
  --generation-allow-x00 false \
  --exclude-checks ignored_auth --junit-xml "${JUNIT_XML:-build/contract-test.xml}"
