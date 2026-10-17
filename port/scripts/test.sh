#!/bin/bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
DOTNET="${DOTNET:-dotnet}"
for project in InspectorTests SmokeTests; do
 "$DOTNET" run --project "$ROOT/port/$project/$project.csproj" -c Release --disable-build-servers -p:UseSharedCompilation=false
done
