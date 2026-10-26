#!/usr/bin/env bash
set -euo pipefail
repo_dir=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
cd "$repo_dir"
if [[ ! -f .env ]]; then
  echo 'Run bash scripts/install.sh first to prepare dependencies and local configuration.' >&2
  exit 1
fi
mkdir -p .run
bash scripts/compose.sh config --quiet
bash scripts/compose.sh up -d --wait --wait-timeout 120
for service in proxy web; do
  if [[ "$service" == proxy ]]; then
    entry="$repo_dir/src/proxy/index.js"
  else
    entry="$repo_dir/src/web/server.js"
  fi
  pid_file="$repo_dir/.run/$service.pid"
  if [[ -f "$pid_file" ]] && ps -p "$(cat "$pid_file")" -o command= | grep -Fq -- "$entry"; then
    continue
  fi
  nohup node "$entry" >"$repo_dir/.run/$service.log" 2>&1 </dev/null &
  echo "$!" >"$pid_file"
done
for attempt in {1..60}; do
  if curl -fsS --max-time 2 http://127.0.0.1:8090/ -o /dev/null 2>/dev/null && curl -fsS --max-time 2 http://127.0.0.1:8080/ -o /dev/null 2>/dev/null; then
    node scripts/smoke.cjs
    for service in proxy web; do
      if [[ "$service" == proxy ]]; then
        entry="$repo_dir/src/proxy/index.js"
      else
        entry="$repo_dir/src/web/server.js"
      fi
      if ! ps -p "$(cat "$repo_dir/.run/$service.pid")" -o command= | grep -Fq -- "$entry"; then
        echo "Expected $service process is not running; inspect .run/$service.log for port conflicts or startup errors." >&2
        exit 1
      fi
    done
    exit 0
  fi
  sleep 1
done
echo 'HTTP readiness failed; inspect compose logs and .run/*.log' >&2
exit 1
