#!/usr/bin/env bash
set -euo pipefail
repo_dir=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
cd "$repo_dir"
exec docker compose -p mu-browser -f docker-compose.yml -f docker-compose.override.yml "$@"
