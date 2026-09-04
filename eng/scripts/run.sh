#!/bin/sh
set -eu

repo_root=$(CDPATH='' cd -- "$(dirname -- "$0")/../.." && pwd)
app=${APP:-src/GbcNet.App/GbcNet.App.csproj}
configuration=${RUN_CONFIGURATION:-Debug}
dotnet=${DOTNET:-dotnet}

cd "$repo_root"
exec "$dotnet" run --project "$app" --configuration "$configuration"
