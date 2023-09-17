#!/usr/bin/env bash

set -eu
set -o pipefail

dotnet run -v:m --project ./build/build.fsproj -- -t "$@"
