#!/usr/bin/env sh
set -eu

app_project=${1:?Usage: package.sh <app-project> <runtime>}
runtime=${2:?Usage: package.sh <app-project> <runtime>}

case "$runtime" in
  osx-*)
    packaging/macos/create-app-bundle.sh "$app_project" "$runtime"
    ;;
  *)
    echo "No packaging script configured for runtime '$runtime'." >&2
    exit 1
    ;;
esac
