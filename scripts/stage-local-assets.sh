#!/bin/sh
set -eu

ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
SOURCE="$ROOT/MU_S6E3/Data"
CONFIGURATION=${1:-Release}
case "$CONFIGURATION" in
    Release|Debug) ;;
    *) printf 'Expected Release or Debug, got: %s\n' "$CONFIGURATION" >&2; exit 1 ;;
esac
if [ "$#" -gt 1 ]; then
    printf 'Usage: %s [Release|Debug]\n' "$0" >&2
    exit 1
fi
RUNTIME="$ROOT/out/build/macos-arm64/src/$CONFIGURATION/Main.app/Contents/MacOS/Data"

if [ ! -d "$SOURCE" ]; then
    printf 'Game data directory not found: %s\n' "$SOURCE" >&2
    exit 1
fi

mkdir -p "$RUNTIME"

# Remove the old sound/music links before overlaying the complete package.
for asset in Sound Music; do
    if [ -L "$RUNTIME/$asset" ]; then
        rm "$RUNTIME/$asset"
    fi
done

rsync -a --checksum --exclude='.DS_Store' --exclude='Thumbs.db' "$SOURCE/" "$RUNTIME/"
