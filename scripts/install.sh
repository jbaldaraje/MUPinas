#!/usr/bin/env bash
set -euo pipefail
repo_dir=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
cd "$repo_dir"
node -e 'if (Number(process.versions.node.split(".")[0]) < 22) { console.error("Node.js 22 or newer is required; .nvmrc selects 22."); process.exit(1); }'
docker info >/dev/null
if [[ ! -e .env ]]; then
  cp .env.loopback.example .env
fi
npm ci --no-audit --no-fund
bash scripts/compose.sh config --quiet
bash scripts/compose.sh pull --quiet
