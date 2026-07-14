#!/usr/bin/env bash
# ============================================================
#  SmartNest — Entra ID (Azure AD) Setup Script
#
#  Run this ONCE before the main Bicep deployment.
#  Requires: Azure CLI, logged in as a user with Application
#  Administrator role in the target tenant.
#
#  Fix H1: App Role IDs are now generated as proper random UUIDs
#           at script runtime — no more fake placeholder GUIDs.
#  Fix H2: Script is now idempotent — if App Registrations already
#           exist (identified by display name) they are reused and
#           only missing roles/claims are updated.
#
#  Usage:
#    chmod +x infra/setup-entra.sh
#    ./infra/setup-entra.sh
# ============================================================
set -euo pipefail

echo "╔══════════════════════════════════════════════════════╗"
echo "║   SmartNest — Entra ID Setup                         ║"
echo "╚══════════════════════════════════════════════════════╝"

# ------------------------------------------------------------------
# Step 1 — Create (or reuse) the API App Registration
# Fix H2: check for an existing registration before creating
# ------------------------------------------------------------------
echo ""
echo "► Step 1/5 — Ensuring App Registration 'smartnest-api' ..."

EXISTING_API=$(az ad app list --display-name "smartnest-api" --query "[0].appId" -o tsv 2>/dev/null || true)
if [ -n "$EXISTING_API" ]; then
  echo "  smartnest-api already exists — reusing appId: ${EXISTING_API}"
  API_APP_CLIENT_ID="$EXISTING_API"
else
  API_APP_OUTPUT=$(az ad app create \
    --display-name "smartnest-api" \
    --sign-in-audience "AzureADMyOrg" \
    --identifier-uris "api://smartnest-api" \
    --output json)
  API_APP_CLIENT_ID=$(echo "$API_APP_OUTPUT" | jq -r '.appId')
  echo "  smartnest-api created — appId: ${API_APP_CLIENT_ID}"
fi

# ------------------------------------------------------------------
# Step 2 — Define App Roles on smartnest-api
# Fix H1: generate proper random UUIDs instead of placeholder values
# Fix H2: skip if roles are already present (idempotent update)
# ------------------------------------------------------------------
echo ""
echo "► Step 2/5 — Defining App Roles (Owner, Technician, Guest) ..."

EXISTING_ROLES=$(az ad app show --id "$API_APP_CLIENT_ID" --query "appRoles[].value" -o tsv 2>/dev/null || true)

if echo "$EXISTING_ROLES" | grep -q "SmartNest.Owner"; then
  echo "  App Roles already defined — skipping."
else
  # Fix H1: generate real random UUIDs at runtime
  OWNER_ROLE_ID=$(uuidgen | tr '[:upper:]' '[:lower:]')
  TECH_ROLE_ID=$(uuidgen | tr '[:upper:]' '[:lower:]')
  GUEST_ROLE_ID=$(uuidgen | tr '[:upper:]' '[:lower:]')

  az ad app update \
    --id "$API_APP_CLIENT_ID" \
    --app-roles "[
      {
        \"allowedMemberTypes\": [\"Application\",\"User\"],
        \"description\": \"Full CRUD on home, rooms, devices, rules, alerts, users within their home\",
        \"displayName\": \"SmartNest Owner\",
        \"id\": \"${OWNER_ROLE_ID}\",
        \"isEnabled\": true,
        \"value\": \"SmartNest.Owner\"
      },
      {
        \"allowedMemberTypes\": [\"Application\",\"User\"],
        \"description\": \"Read/write devices and device state; read-only on home/rules\",
        \"displayName\": \"SmartNest Technician\",
        \"id\": \"${TECH_ROLE_ID}\",
        \"isEnabled\": true,
        \"value\": \"SmartNest.Technician\"
      },
      {
        \"allowedMemberTypes\": [\"Application\",\"User\"],
        \"description\": \"Read-only on device state within their assigned homeId\",
        \"displayName\": \"SmartNest Guest\",
        \"id\": \"${GUEST_ROLE_ID}\",
        \"isEnabled\": true,
        \"value\": \"SmartNest.Guest\"
      }
    ]"
  echo "  App Roles created."
fi

# ------------------------------------------------------------------
# Step 3 — Add homeId optional claim
# Fix H2: skip if claim is already present
# ------------------------------------------------------------------
echo ""
echo "► Step 3/5 — Adding 'homeId' optional claim ..."

EXISTING_CLAIMS=$(az ad app show --id "$API_APP_CLIENT_ID" --query "optionalClaims.accessToken[].name" -o tsv 2>/dev/null || true)

if echo "$EXISTING_CLAIMS" | grep -q "homeId"; then
  echo "  Optional claim 'homeId' already present — skipping."
else
  az ad app update \
    --id "$API_APP_CLIENT_ID" \
    --optional-claims '{
      "accessToken": [
        {
          "name": "homeId",
          "source": null,
          "essential": false,
          "additionalProperties": []
        }
      ],
      "idToken": [
        {
          "name": "homeId",
          "source": null,
          "essential": false,
          "additionalProperties": []
        }
      ]
    }'
  echo "  Optional claim 'homeId' added."
fi

# ------------------------------------------------------------------
# Step 4 — Create (or reuse) the client App Registration
# Fix H2: check for an existing registration before creating
# ------------------------------------------------------------------
echo ""
echo "► Step 4/5 — Ensuring App Registration 'smartnest-client' ..."

EXISTING_CLIENT=$(az ad app list --display-name "smartnest-client" --query "[0].appId" -o tsv 2>/dev/null || true)
if [ -n "$EXISTING_CLIENT" ]; then
  echo "  smartnest-client already exists — reusing appId: ${EXISTING_CLIENT}"
  CLIENT_APP_CLIENT_ID="$EXISTING_CLIENT"
else
  CLIENT_APP_OUTPUT=$(az ad app create \
    --display-name "smartnest-client" \
    --sign-in-audience "AzureADMyOrg" \
    --output json)
  CLIENT_APP_CLIENT_ID=$(echo "$CLIENT_APP_OUTPUT" | jq -r '.appId')
  echo "  smartnest-client created — appId: ${CLIENT_APP_CLIENT_ID}"
fi

# Ensure service principal exists (idempotent — fails silently if already present)
az ad sp create --id "$CLIENT_APP_CLIENT_ID" --output none 2>/dev/null || true

API_SP_OBJECT_ID=$(az ad sp show --id "$API_APP_CLIENT_ID" --query id -o tsv)
CLIENT_SP_OBJECT_ID=$(az ad sp show --id "$CLIENT_APP_CLIENT_ID" --query id -o tsv)

# ------------------------------------------------------------------
# Step 5 — Assign all three roles to smartnest-client
# Fix H2: skip individual role assignments that already exist
# ------------------------------------------------------------------
echo ""
echo "► Step 5/5 — Assigning App Roles to 'smartnest-client' ..."

ASSIGNED_ROLES=$(az rest \
  --method GET \
  --uri "https://graph.microsoft.com/v1.0/servicePrincipals/${CLIENT_SP_OBJECT_ID}/appRoleAssignments" \
  --query "value[?resourceId=='${API_SP_OBJECT_ID}'].appRoleId" \
  -o tsv 2>/dev/null || true)

# Re-read the actual role IDs from the app (they were generated above or already exist)
ROLE_IDS=$(az ad app show --id "$API_APP_CLIENT_ID" --query "appRoles[].id" -o tsv)

for ROLE_ID in $ROLE_IDS; do
  if echo "$ASSIGNED_ROLES" | grep -qi "$ROLE_ID"; then
    echo "  Role ${ROLE_ID} already assigned — skipping."
  else
    az rest \
      --method POST \
      --uri "https://graph.microsoft.com/v1.0/servicePrincipals/${CLIENT_SP_OBJECT_ID}/appRoleAssignments" \
      --body "{
        \"principalId\": \"${CLIENT_SP_OBJECT_ID}\",
        \"resourceId\": \"${API_SP_OBJECT_ID}\",
        \"appRoleId\": \"${ROLE_ID}\"
      }" \
      --output none
    echo "  Role ${ROLE_ID} assigned."
  fi
done

# ------------------------------------------------------------------
# Summary
# ------------------------------------------------------------------
TENANT_ID=$(az account show --query tenantId -o tsv)

echo ""
echo "╔══════════════════════════════════════════════════════╗"
echo "║   Entra ID Setup Complete                            ║"
echo "╠══════════════════════════════════════════════════════╣"
echo "║  Add these to the 'smartnest-common' variable group: ║"
echo "╚══════════════════════════════════════════════════════╝"
echo ""
echo "  AZURE_TENANT_ID   = ${TENANT_ID}"
echo "  API_APP_CLIENT_ID = ${API_APP_CLIENT_ID}"
echo ""
echo "► Next: run ./infra/deploy.sh dev  OR  trigger the Azure Pipeline"
