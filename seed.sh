#!/usr/bin/env bash
set -euo pipefail

SERVER="${SERVER:-https://dev.broca.luit.ink}"
API_KEY="${API_KEY:-dev-api-key-12345-change-in-production}"
PREFIX="${PREFIX:-sample_}"
COUNT="${COUNT:-3}"
ROUTE_PREFIX="${ROUTE_PREFIX:-ap}"
ADMIN="${ADMIN:-admin}"

dotnet run --project "$(dirname "$0")/tools/Broca.SampleData/Broca.SampleData.csproj" -- \
  --server        "$SERVER" \
  --api-key       "$API_KEY" \
  --prefix        "$PREFIX" \
  --count         "$COUNT" \
  --route-prefix  "$ROUTE_PREFIX" \
  --admin         "$ADMIN"
