#!/bin/sh
set -eu

SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
SOURCE_DIR=$(CDPATH= cd -- "$SCRIPT_DIR/../.." && pwd)
STAGE_DIR=$(mktemp -d "${TMPDIR:-/tmp}/openmu-local-testing.XXXXXX")
trap 'rm -rf "$STAGE_DIR"' EXIT HUP INT TERM

dotnet build "$SOURCE_DIR/src/GameLogic/MUnique.OpenMU.GameLogic.csproj" -c Release -p:ci=true
mkdir -p "$STAGE_DIR/assemblies"
for ASSEMBLY in MUnique.OpenMU.GameLogic MUnique.OpenMU.AttributeSystem; do
    cp "$SOURCE_DIR/bin/Release/$ASSEMBLY.dll" "$STAGE_DIR/assemblies/"
    cp "$SOURCE_DIR/bin/Release/$ASSEMBLY.pdb" "$STAGE_DIR/assemblies/"
done
docker build -f "$SCRIPT_DIR/Dockerfile" -t "${OPENMU_TESTING_IMAGE:-mupinas/openmu:local-testing-20261028}" "$STAGE_DIR"
