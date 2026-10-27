#!/bin/sh
set -eu

ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
CONFIGURATION=Release
case "${1:-}" in
    --debug) CONFIGURATION=Debug; shift ;;
    --release) shift ;;
esac
RUNTIME="$ROOT/out/build/macos-arm64/src/$CONFIGURATION/Main.app/Contents/MacOS"

if [ ! -x "$RUNTIME/Main" ]; then
    printf 'MU Online %s build not found: %s\n' "$CONFIGURATION" "$RUNTIME/Main" >&2
    exit 1
fi
printf 'Launching MU Online %s: %s\n' "$CONFIGURATION" "$RUNTIME/Main" >&2

cd "$RUNTIME"
exec ./Main "$@"
