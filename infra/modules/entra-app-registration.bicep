// ============================================================
//  SmartNest - Entra ID (Azure AD) App Registrations
//
//  NOTE: Azure AD objects (App Registrations, App Roles,
//  optional claims) are tenant-level resources and CANNOT be
//  created by ARM/Bicep subscription-scoped or resource-group-
//  scoped deployments. They must be provisioned via one of:
//    a) Azure Portal - manual, one-time setup
//    b) Microsoft Graph API (az rest / curl)
//    c) Terraform azuread provider
//    d) Azure CLI (az ad app create …)
//
//  This module documents the DESIRED STATE as Bicep comments
//  and emits the equivalent Azure CLI commands that an operator
//  or CI pipeline (setup-entra.ps1 / setup-entra.sh) should
//  run once, before the main Bicep deployment.
//
//  After running the CLI commands the operator pastes the
//  resulting App IDs into dev.parameters.json.
// ============================================================

// This file is intentionally a documentation module.
// It outputs nothing and deploys nothing - but it keeps the
// desired Entra state co-located with the rest of the infra
// code so future changes are made here first.

/*
────────────────────────────────────────────────────────────────
  STEP 1 - Create the API App Registration (smartnest-api)
────────────────────────────────────────────────────────────────
  az ad app create \
    --display-name "smartnest-api" \
    --sign-in-audience "AzureADMyOrg" \
    --identifier-uris "api://smartnest-api"

  # Note the returned appId (API_APP_CLIENT_ID)

────────────────────────────────────────────────────────────────
  STEP 2 - Define App Roles on smartnest-api
────────────────────────────────────────────────────────────────
  API_APP_CLIENT_ID="<appId from step 1>"

  az ad app update \
    --id $API_APP_CLIENT_ID \
    --app-roles '[
      {
        "allowedMemberTypes": ["Application","User"],
        "description": "Full access - home owner",
        "displayName": "SmartNest Owner",
        "id": "00000000-0000-0000-0000-000000000001",
        "isEnabled": true,
        "value": "SmartNest.Owner"
      },
      {
        "allowedMemberTypes": ["Application","User"],
        "description": "Device read/write, limited home read",
        "displayName": "SmartNest Technician",
        "id": "00000000-0000-0000-0000-000000000002",
        "isEnabled": true,
        "value": "SmartNest.Technician"
      },
      {
        "allowedMemberTypes": ["Application","User"],
        "description": "Read-only device state within assigned home",
        "displayName": "SmartNest Guest",
        "id": "00000000-0000-0000-0000-000000000003",
        "isEnabled": true,
        "value": "SmartNest.Guest"
      }
    ]'

────────────────────────────────────────────────────────────────
  STEP 3 - Add homeId optional claim to the API App Registration
────────────────────────────────────────────────────────────────
  az ad app update \
    --id $API_APP_CLIENT_ID \
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

────────────────────────────────────────────────────────────────
  STEP 4 - Create the client App Registration (smartnest-client)
────────────────────────────────────────────────────────────────
  az ad app create \
    --display-name "smartnest-client" \
    --sign-in-audience "AzureADMyOrg"

  # Note the returned appId (CLIENT_APP_CLIENT_ID)
  CLIENT_APP_CLIENT_ID="<appId from step 4>"

  # Create service principal so we can assign roles
  az ad sp create --id $CLIENT_APP_CLIENT_ID

  # Assign all three App Roles from smartnest-api to smartnest-client
  API_SP_OBJECT_ID=$(az ad sp show --id $API_APP_CLIENT_ID --query id -o tsv)
  CLIENT_SP_OBJECT_ID=$(az ad sp show --id $CLIENT_APP_CLIENT_ID --query id -o tsv)

  for ROLE_ID in \
    "00000000-0000-0000-0000-000000000001" \
    "00000000-0000-0000-0000-000000000002" \
    "00000000-0000-0000-0000-000000000003"; do
    az rest \
      --method POST \
      --uri "https://graph.microsoft.com/v1.0/servicePrincipals/$CLIENT_SP_OBJECT_ID/appRoleAssignments" \
      --body "{
        \"principalId\": \"$CLIENT_SP_OBJECT_ID\",
        \"resourceId\": \"$API_SP_OBJECT_ID\",
        \"appRoleId\": \"$ROLE_ID\"
      }"
  done

────────────────────────────────────────────────────────────────
  STEP 5 - After completing steps 1-4, update dev.parameters.json
────────────────────────────────────────────────────────────────
  - Set "tenantId"       → your Azure AD Tenant ID
  - Set "apiAppClientId" → $API_APP_CLIENT_ID
────────────────────────────────────────────────────────────────
*/
