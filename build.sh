#!/usr/bin/env bash

set -eu
set -o pipefail

dotnet build ./build/build.fsproj -v d

FAKE_DETAILED_ERRORS=true dotnet run --project ./build/build.fsproj -- -t "$@"
