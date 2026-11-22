#!/usr/bin/env bash
# ЛР-11: генерує TypeScript SDK з контракту docs/api/openapi.yaml (SSOT). Результат — build/sdk/typescript.
# Потрібен Docker. Версія генератора зафіксована, щоб SDK відтворювався байт-у-байт.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
OUT="${1:-build/sdk/typescript}"
docker run --rm -u "$(id -u):$(id -g)" -v "$ROOT:/local" openapitools/openapi-generator-cli:v7.10.0 generate \
  -i /local/docs/api/openapi.yaml -g typescript-fetch -o "/local/$OUT" \
  --additional-properties=npmName=@zvytiaha/api-client,npmVersion=1.2.0,supportsES6=true,typescriptThreePlus=true
echo "SDK: $ROOT/$OUT"
