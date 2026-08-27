#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -ne 1 ]; then
  echo "usage: $0 RUNS_DB" >&2
  exit 2
fi

database="$1"

if [ ! -f "$database" ]; then
  echo "database not found: $database" >&2
  exit 2
fi

script_directory="$(cd "$(dirname "$0")" && pwd)"
database_uri="file:${database}?mode=ro&immutable=1"

echo "database_sha256=$(shasum -a 256 "$database" | awk '{print $1}')"
sqlite3 -readonly -json "$database_uri" < "$script_directory/audit_activegraph_forks.sql"
