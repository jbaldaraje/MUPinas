#!/usr/bin/env bash
set -euo pipefail
repo_dir=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
cd "$repo_dir"
for service in proxy web; do
  if [[ "$service" == proxy ]]; then
    entry="$repo_dir/src/proxy/index.js"
  else
    entry="$repo_dir/src/web/server.js"
  fi
  pid_file="$repo_dir/.run/$service.pid"
  if [[ -f "$pid_file" ]] && ps -p "$(cat "$pid_file")" -o command= | grep -Fq -- "$entry"; then
    kill "$(cat "$pid_file")"
  fi
done
bash scripts/compose.sh stop
