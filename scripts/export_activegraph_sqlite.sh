#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -ne 2 ]; then
  echo "usage: $0 RUNS_DB OUTPUT_DIRECTORY" >&2
  exit 2
fi

database="$1"
output_directory="$2"

if [ ! -f "$database" ]; then
  echo "database not found: $database" >&2
  exit 2
fi

mkdir -p "$output_directory"
database_uri="file:${database}?mode=ro&immutable=1"

run_count=0
event_count=0

while IFS= read -r run_id; do
  if [[ ! "$run_id" =~ ^[A-Za-z0-9._:-]+$ ]]; then
    echo "unsafe run id refused: $run_id" >&2
    exit 1
  fi

  output="$output_directory/$run_id.jsonl"

  sqlite3 -noheader "$database_uri" \
    "SELECT json_object(
       'id', id,
       'type', type,
       'caused_by', caused_by,
       'payload', json(payload)
     )
     FROM events
     WHERE run_id = '$run_id'
     ORDER BY seq;" > "$output"

  count="$(wc -l < "$output" | tr -d ' ')"
  run_count=$((run_count + 1))
  event_count=$((event_count + count))
done < <(
  sqlite3 -noheader "$database_uri" \
    "SELECT DISTINCT run_id FROM events ORDER BY run_id;"
)

echo "database_sha256=$(shasum -a 256 "$database" | awk '{print $1}')"
echo "runs=$run_count"
echo "events=$event_count"
echo "output=$output_directory"
