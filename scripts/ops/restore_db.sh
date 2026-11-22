#!/usr/bin/env bash
# RB-001: restore a backup made by backup_db.sh into database <target_db> and verify it.
# Verification = sha256 of the file + row counts per table equal to the backup manifest.
# Restoring into the production database name requires CONFIRM=yes (the API must be stopped first).
# Usage: scripts/ops/restore_db.sh <backup.dump|backup.dump.gpg> <target_db>
set -euo pipefail
FILE="$1"; TARGET="$2"
C="${DB_CONTAINER:-tournament-db}"
: "${POSTGRES_USER:?set POSTGRES_USER}" "${POSTGRES_DB:?set POSTGRES_DB}"
BASE="${FILE%.gpg}"; BASE="${BASE%.dump}"
if [[ "$TARGET" == "$POSTGRES_DB" && "${CONFIRM:-}" != "yes" ]]; then
  echo "refusing to overwrite production database '$TARGET' without CONFIRM=yes" >&2; exit 2
fi
start=$(date +%s)
sha256sum -c "$BASE.sha256"

DUMP="$FILE"
if [[ "$FILE" == *.gpg ]]; then
  : "${BACKUP_PASSPHRASE_FILE:?encrypted backup needs BACKUP_PASSPHRASE_FILE}"
  DUMP="$(mktemp)"; trap 'rm -f "$DUMP"' EXIT
  gpg --batch --quiet --pinentry-mode loopback --passphrase-file "$BACKUP_PASSPHRASE_FILE" -d -o "$DUMP" --yes "$FILE"
fi

psql_admin() { docker exec -i "$C" psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d postgres -q "$@"; }
psql_admin -c "select pg_terminate_backend(pid) from pg_stat_activity where datname='$TARGET' and pid<>pg_backend_pid();" >/dev/null
psql_admin -c "drop database if exists \"$TARGET\";"
psql_admin -c "create database \"$TARGET\";"
docker exec -i "$C" pg_restore -U "$POSTGRES_USER" -d "$TARGET" --no-owner --exit-on-error < "$DUMP"

ACTUAL="$(docker exec "$C" psql -U "$POSTGRES_USER" -d "$TARGET" -Atc \
  "select table_name||'='||(xpath('/row/c/text()', query_to_xml('select count(*) as c from public.\"'||table_name||'\"', false, true, '')))[1]::text
   from information_schema.tables where table_schema='public' and table_type='BASE TABLE' order by 1")"
if diff <(echo "$ACTUAL") "$BASE.manifest" > /dev/null; then
  echo "verify: row counts match manifest ($(wc -l < "$BASE.manifest") tables)"
else
  echo "verify: MISMATCH"; diff <(echo "$ACTUAL") "$BASE.manifest" || true; exit 1
fi
echo "restore into '$TARGET' finished in $(( $(date +%s) - start )) s"
