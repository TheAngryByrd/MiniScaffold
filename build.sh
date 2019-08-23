#!/usr/bin/env bash

set -eu
set -o pipefail

# liberated from https://stackoverflow.com/a/18443300/433393
realpath() {
  OURPWD=$PWD
  cd "$(dirname "$1")"
  LINK=$(readlink "$(basename "$1")")
  while [ "$LINK" ]; do
    cd "$(dirname "$LINK")"
    LINK=$(readlink "$(basename "$1")")
  done
  REALPATH="$PWD/$(basename "$1")"
  cd "$OURPWD"
  echo "$REALPATH"
}

echo "Restoring dotnet tools..."
dotnet tool restore

PAKET_TOOL_PATH=$(realpath .paket)
PAKET="$PAKET_TOOL_PATH"/paket

if ! [ -e "$PAKET" ]
then
  dotnet tool install paket --tool-path "$PAKET_TOOL_PATH"
fi

FAKE_DETAILED_ERRORS=true dotnet fake build -t "$@"
