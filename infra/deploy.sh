#!/usr/bin/env bash
# ============================================================
#  SmartNest — Azure Infrastructure Deployment Script (Bash)
#
#  Prerequisites:
#    - Azure CLI installed and authenticated (az login)
#    - Correct subscription selected:
#        az account set --subscription "<subscription-name-or-id>"
#    - Entra ID setup completed (see infra/modules/entra-app-registration.bicep)
#    - dev.parameters.json updated with real tenantId / apiAppClientId
#
#  Usage:
#    chmod +x infra/deploy.sh
#    ./infra/deploy.sh [environment]          # default: dev
#    ./infra/deploy.sh dev eastus             # explicit region
# ============================================================
set -euo pipefail

ENVIRONMENT="${1:-dev}"
LOCATION="${2:-eastus}"
RESOURCE_GROUP="smartnest-rg"
PARAMETERS_FILE="infra/parameters/${ENVIRONMENT}.parameters.json"
TEMPLATE_FILE="infra/main.bicep"

echo "╔══════════════════════════════════════════════════════╗"
echo "║   SmartNest — Azure Infrastructure Deployment        ║"
echo "╠══════════════════════════════════════════════════════╣"
echo "║  Environment  : ${ENVIRONMENT}"
echo "║  Location     : ${LOCATION}"
echo "║  Resource Group: ${RESOURCE_GROUP}"
echo "╚══════════════════════════════════════════════════════╝"

# ------------------------------------------------------------------
# 1. Ensure Resource Group exists
# ------------------------------------------------------------------
echo ""
echo "► Step 1/4 — Creating Resource Group '${RESOURCE_GROUP}' in ${LOCATION} ..."
az group create \
  --name "${RESOURCE_GROUP}" \
  --location "${LOCATION}" \
  --tags project=smartnest environment="${ENVIRONMENT}" managedBy=bicep \
  --output table

# ------------------------------------------------------------------
# 2. Validate the Bicep template before deploying
# ------------------------------------------------------------------
echo ""
echo "► Step 2/4 — Validating Bicep template ..."
az deployment group validate \
  --resource-group "${RESOURCE_GROUP}" \
  --template-file "${TEMPLATE_FILE}" \
  --parameters "@${PARAMETERS_FILE}" \
  --output table

# ------------------------------------------------------------------
# 3. Deploy (what-if preview first, then actual)
# ------------------------------------------------------------------
echo ""
echo "► Step 3/4 — Running what-if preview ..."
az deployment group what-if \
  --resource-group "${RESOURCE_GROUP}" \
  --template-file "${TEMPLATE_FILE}" \
  --parameters "@${PARAMETERS_FILE}"

echo ""
read -rp "► Proceed with actual deployment? [y/N] " CONFIRM
if [[ "${CONFIRM,,}" != "y" ]]; then
  echo "Deployment cancelled."
  exit 0
fi

# ------------------------------------------------------------------
# 4. Deploy
# ------------------------------------------------------------------
echo ""
echo "► Step 4/4 — Deploying SmartNest infrastructure ..."
DEPLOYMENT_OUTPUT=$(az deployment group create \
  --resource-group "${RESOURCE_GROUP}" \
  --template-file "${TEMPLATE_FILE}" \
  --parameters "@${PARAMETERS_FILE}" \
  --name "smartnest-infra-$(date +%Y%m%d%H%M%S)" \
  --output json)

echo ""
echo "✔  Deployment complete."
echo ""
echo "━━━ Outputs ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "${DEPLOYMENT_OUTPUT}" | jq -r '.properties.outputs | to_entries[] | "  \(.key): \(.value.value)"'
echo ""
echo "⚠  Secrets (cosmosPrimaryKey, storageConnectionString, serviceBusFunctionsConnectionString)"
echo "   should be moved to Azure Key Vault before provisioning Function Apps."
