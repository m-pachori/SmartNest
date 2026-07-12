#!/usr/bin/env bash
# ============================================================
#  SmartNest — Entra ID (Azure AD) Setup Script
#
#  Run this ONCE before the main Bicep deployment.
#  Requires: Azure CLI, logged in as a user with Application
#  Administrator role in the target tenant.
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
# Step 1 — Create the API App Registration
# ------------------------------------------------------------------
echo ""
echo "► Step 1/5 — Creating App Registration 'smartnest-api' ..."
API_APP_OUTPUT=$(az ad app create \
  --display-name "smartnest-api" \
  --sign-in-audience "AzureADMyOrg" \
  --identifier-uris "api://smartnest-api" \
  --output json)

API_APP_CLIENT_ID=$(echo "$API_APP_OUTPUT" | jq -r '.appId')
API_APP_OBJECT_ID=$(echo "$API_APP_OUTPUT" | jq -r '.id')
echo "  smartnest-api appId : ${API_APP_CLIENT_ID}"

# ------------------------------------------------------------------
# Step 2 — Define App Roles on smartnest-api
# ------------------------------------------------------------------
echo ""
echo "► Step 2/5 — Defining App Roles (Owner, Technician, Guest) ..."
az ad app update \
  --id "$API_APP_CLIENT_ID" \
  --app-roles '[
    {
      "allowedMemberTypes": ["Application","User"],
      "description": "Full CRUD on home, rooms, devices, rules, alerts, users within their home",
      "displayName": "SmartNest Owner",
      "id": "00000000-0000-0000-0000-000000000001",
      "isEnabled": true,
      "value": "SmartNest.Owner"
    },
    {
      "allowedMemberTypes": ["Application","User"],
      "description": "Read/write devices and device state; read-only on home/rules",
      "displayName": "SmartNest Technician",
      "id": "00000000-0000-0000-0000-000000000002",
      "isEnabled": true,
      "value": "SmartNest.Technician"
    },
    {
      "allowedMemberTypes": ["Application","User"],
      "description": "Read-only on device state within their assigned homeId",
      "displayName": "SmartNest Guest",
      "id": "00000000-0000-0000-0000-000000000003",
      "isEnabled": true,
      "value": "SmartNest.Guest"
    }
  ]'
echo "  App Roles created."

# ------------------------------------------------------------------
# Step 3 — Add homeId optional claim
# ------------------------------------------------------------------
echo ""
echo "► Step 3/5 — Adding 'homeId' optional claim ..."
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

# ------------------------------------------------------------------
# Step 4 — Create the client App Registration (smartnest-client)
# ------------------------------------------------------------------
echo ""
echo "► Step 4/5 — Creating App Registration 'smartnest-client' ..."
CLIENT_APP_OUTPUT=$(az ad app create \
  --display-name "smartnest-client" \
  --sign-in-audience "AzureADMyOrg" \
  --output json)

CLIENT_APP_CLIENT_ID=$(echo "$CLIENT_APP_OUTPUT" | jq -r '.appId')
echo "  smartnest-client appId : ${CLIENT_APP_CLIENT_ID}"

# Create service principal for role assignment
az ad sp create --id "$CLIENT_APP_CLIENT_ID" --output none

API_SP_OBJECT_ID=$(az ad sp show --id "$API_APP_CLIENT_ID" --query id -o tsv)
CLIENT_SP_OBJECT_ID=$(az ad sp show --id "$CLIENT_APP_CLIENT_ID" --query id -o tsv)

# ------------------------------------------------------------------
# Step 5 — Assign all three roles to smartnest-client
# ------------------------------------------------------------------
echo ""
echo "► Step 5/5 — Assigning App Roles to 'smartnest-client' ..."
for ROLE_ID in \
  "00000000-0000-0000-0000-000000000001" \
  "00000000-0000-0000-0000-000000000002" \
  "00000000-0000-0000-0000-000000000003"; do
  az rest \
    --method POST \
    --uri "https://graph.microsoft.com/v1.0/servicePrincipals/${CLIENT_SP_OBJECT_ID}/appRoleAssignments" \
    --body "{
      \"principalId\": \"${CLIENT_SP_OBJECT_ID}\",
      \"resourceId\": \"${API_SP_OBJECT_ID}\",
      \"appRoleId\": \"${ROLE_ID}\"
    }" \
    --output none
done
echo "  Roles assigned."

# ------------------------------------------------------------------
# Summary
# ------------------------------------------------------------------
TENANT_ID=$(az account show --query tenantId -o tsv)

echo ""
echo "╔══════════════════════════════════════════════════════╗"
echo "║   Entra ID Setup Complete                            ║"
echo "╠══════════════════════════════════════════════════════╣"
echo "║  Update infra/parameters/dev.parameters.json with:  ║"
echo "╚══════════════════════════════════════════════════════╝"
echo ""
echo "  \"tenantId\":       \"${TENANT_ID}\""
echo "  \"apiAppClientId\": \"${API_APP_CLIENT_ID}\""
echo ""
echo "► Next: run ./infra/deploy.sh dev"
