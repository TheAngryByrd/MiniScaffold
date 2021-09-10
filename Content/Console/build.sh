#!/usr/bin/env bash

set -eu
set -o pipefail

echo "Restoring dotnet tools..."
dotnet tool restore

FAKE_DETAILED_ERRORS=true dotnet run -p ./build/build.fsproj -- -t "$@"
