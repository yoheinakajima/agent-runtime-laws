#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/.."

dotnet restore AgentRuntimeLaws.sln
dotnet build AgentRuntimeLaws.sln --no-restore
dotnet test \
  tests/AgentRuntimeLaws.Properties/AgentRuntimeLaws.Properties.fsproj \
  --no-build \
  --no-restore

dotnet run --project apps/AgentRuntimeLaws.Cli --no-build -- demo
dotnet run --project apps/AgentRuntimeLaws.Cli --no-build -- \
  conformance conformance/vectors/v1.json
dotnet run --project apps/AgentRuntimeLaws.Cli --no-build -- \
  manifest evidence/manifest.json

git diff --check
