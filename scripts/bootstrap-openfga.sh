#!/usr/bin/env bash
#
# Creates the OpenFGA store and loads the authorization model, then prints the
# two ids ms-authz needs in its configuration.
#
# Why this exists: ms-authz does NOT create its own store. It expects
# OpenFga:StoreId to be a store that already holds the model from openfga/model.fga.
# A fresh environment ships with StoreId = "REPLACE_WITH_DEV_STORE_ID", so
# without this step the service starts but every call to OpenFGA fails.
#
# Run once per environment, after OpenFGA is up and migrated:
#   docker compose --profile migration run --rm openfga-migrate
#   docker compose up -d openfga
#   ./scripts/bootstrap-openfga.sh
#
# Requires the `fga` CLI: brew install openfga/tap/fga
#
set -euo pipefail

FGA_URL="${FGA_API_URL:-http://localhost:8082}"
STORE_NAME="${STORE_NAME:-ms-authz}"
MODEL_FILE="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)/openfga/model.fga"

if ! command -v fga >/dev/null 2>&1; then
    echo "✗ The 'fga' CLI is not installed."
    echo "  brew install openfga/tap/fga"
    exit 1
fi

if [ ! -f "$MODEL_FILE" ]; then
    echo "✗ Model file not found: $MODEL_FILE"
    exit 1
fi

if ! curl -sf "$FGA_URL/healthz" >/dev/null 2>&1; then
    echo "✗ OpenFGA is not reachable at $FGA_URL"
    echo "  Start it first, or override with FGA_API_URL."
    exit 1
fi

echo "Creating store '$STORE_NAME' at $FGA_URL and loading $MODEL_FILE ..."

RESPONSE=$(FGA_API_URL="$FGA_URL" fga store create --name "$STORE_NAME" --model "$MODEL_FILE")

STORE_ID=$(echo "$RESPONSE" | python3 -c "import sys,json; print(json.load(sys.stdin)['store']['id'])")
MODEL_ID=$(echo "$RESPONSE" | python3 -c "import sys,json; print(json.load(sys.stdin).get('model',{}).get('authorization_model_id',''))")

if [ -z "$STORE_ID" ]; then
    echo "✗ Could not read the store id from the response:"
    echo "$RESPONSE"
    exit 1
fi

cat <<EOF

✓ Store created.

Put these in ms-authz's configuration (appsettings.Development.json, or the
OpenFga__StoreId / OpenFga__AuthorizationModelId environment variables):

  "OpenFga": {
    "ApiUrl": "$FGA_URL",
    "StoreId": "$STORE_ID",
    "AuthorizationModelId": "$MODEL_ID"
  }

Next: mount the catalog file (Catalog__Path) and sync every tenant so the
catalog is materialised into tuples:

  curl -X POST http://localhost:6010/catalog/sync \\
    -H "X-Api-Key: \$MS_AUTHZ_API_KEY" \\
    -H "Content-Type: application/json" \\
    -d '{"tenantCodes":["<tenant code>", "..."]}'
EOF
