#!/bin/sh
set -eu

PYTHON="$(brew --prefix python@3.13)/bin/python3.13"
EXPAT_LIB="$(brew --prefix expat)/lib"

if [ ! -x "$PYTHON" ]; then
    printf '%s\n' "Python 3.13 is required; install python@3.13 with Homebrew." >&2
    exit 1
fi

DYLD_LIBRARY_PATH="$EXPAT_LIB" exec "$PYTHON" "$@"
