#!/bin/sh
set -eu

ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
cd "$ROOT"

CONFIGURATION=Release
PRESET=macos-arm64-release
CLEAN=0
while [ "$#" -gt 0 ]; do
    case "$1" in
        --debug) CONFIGURATION=Debug; PRESET=macos-arm64-debug ;;
        --release) CONFIGURATION=Release; PRESET=macos-arm64-release ;;
        --clean) CLEAN=1 ;;
        *)
            printf 'Usage: %s [--release|--debug] [--clean]\n' "$0" >&2
            exit 1
            ;;
    esac
    shift
done

cmake --preset macos-arm64 \
    -DCMAKE_C_COMPILER=/usr/bin/clang \
    -DCMAKE_CXX_COMPILER=/usr/bin/clang++ \
    -DPython3_EXECUTABLE="$ROOT/scripts/python-with-expat.sh"
if [ "$CLEAN" -eq 1 ]; then
    cmake --build --preset "$PRESET" --target clean
fi
cmake --build --preset "$PRESET"
./scripts/stage-local-assets.sh "$CONFIGURATION"
