#!/bin/sh
set -eu

ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
ENV_FILE=${OPENMU_LOCAL_ENV_FILE:-$ROOT/out/staging/openmu-local-testing/live.private.env}
if [ ! -f "$ENV_FILE" ]; then
    printf 'Private environment file missing: %s\nSee docs/local-testing-macos.md.\n' "$ENV_FILE" >&2
    exit 1
fi
cd "$ROOT"
exec docker compose --env-file "$ENV_FILE" -f docker-compose.yml -f docker-compose.local-testing.yml up -d --no-deps openmu-startup
