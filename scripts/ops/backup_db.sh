#!/usr/bin/env bash
# RB-001 / backup policy: full logical backup of the PostgreSQL database (pg_dump custom format).
# Writes <out>/tournament_<UTC timestamp>.dump, a .sha256 and a .manifest with row counts per table
# (the manifest is what the restore drill compares against). With BACKUP_PASSPHRASE_FILE set the dump
# is encrypted (gpg AES256) and the plain dump is removed.
# Usage: scripts/ops/backup_db.sh [out_dir]   env: DB_CONTAINER (tournament-db), POSTGRES_USER, POSTGRES_DB
set -euo pipefail
OUT="${1:-backups}"
C="${DB_CONTAINER:-tournament-db}"
: "${POSTGRES_USER:?set POSTGRES_USER}" "${POSTGRES_DB:?set POSTGRES_DB}"
mkdir -p "$OUT"
TS="$(date -u +%Y%m%dT%H%M%SZ)"
BASE="$OUT/tournament_$TS"

docker exec "$C" pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Fc --no-owner > "$BASE.dump"
docker exec "$C" psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Atc \
  "select table_name||'='||(xpath('/row/c/text()', query_to_xml('select count(*) as c from public.\"'||table_name||'\"', false, true, '')))[1]::text
   from information_schema.tables where table_schema='public' and table_type='BASE TABLE' order by 1" > "$BASE.manifest"

if [[ -n "${BACKUP_PASSPHRASE_FILE:-}" ]]; then
  gpg --batch --yes --pinentry-mode loopback --passphrase-file "$BACKUP_PASSPHRASE_FILE" \
      --symmetric --cipher-algo AES256 -o "$BASE.dump.gpg" "$BASE.dump"
  rm "$BASE.dump"
  sha256sum "$BASE.dump.gpg" > "$BASE.sha256"
else
  sha256sum "$BASE.dump" > "$BASE.sha256"
fi
echo "backup: $(ls "$BASE".dump* ) ($(du -h "$BASE".dump* | cut -f1)), tables: $(wc -l < "$BASE.manifest")"
