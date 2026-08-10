#!/bin/sh
set -eu

repository_root=$(CDPATH= cd -- "$(dirname -- "$0")/../.." && pwd)
artifact_dir="$repository_root/artifacts/publish/Servanda-linux-x64"
test_output="$repository_root/tests/Servanda.E2E/bin/Debug/net10.0"

cd -- "$repository_root"

dotnet restore
dotnet publish src/Servanda.App/Servanda.App.csproj -p:PublishProfile=linux-x64 --no-restore
dotnet build tests/Servanda.E2E/Servanda.E2E.csproj --no-restore -m:1

"$test_output/.playwright/node/linux-x64/node" \
    "$test_output/.playwright/package/cli.js" \
    install chromium firefox

SERVANDA_BROWSER_E2E_ARTIFACT="$artifact_dir" \
    dotnet test tests/Servanda.E2E/Servanda.E2E.csproj \
    --no-build \
    --filter Category=Browser
