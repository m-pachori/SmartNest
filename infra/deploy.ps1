#Requires -Version 5.1
<#
.SYNOPSIS
    SmartNest — Azure Infrastructure Deployment Script (PowerShell)

.DESCRIPTION
    Creates the Resource Group then deploys the full SmartNest Bicep stack.

    Prerequisites:
      - Azure CLI installed and authenticated (az login)
      - Correct subscription selected:
          az account set --subscription "<subscription-name-or-id>"
      - Entra ID setup completed (see infra\modules\entra-app-registration.bicep)
      - dev.parameters.json updated with real tenantId / apiAppClientId

.PARAMETER Environment
    Target environment: dev | staging | prod  (default: dev)

.PARAMETER Location
    Azure region  (default: eastus)

.EXAMPLE
    .\infra\deploy.ps1
    .\infra\deploy.ps1 -Environment dev -Location eastus
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [ValidateSet('dev','staging','prod')]
    [string]$Environment = 'dev',

    [string]$Location = 'eastus'
)

$ErrorActionPreference = 'Stop'

$ResourceGroup    = 'smartnest-rg'
$ParametersFile   = "infra\parameters\$Environment.parameters.json"
$TemplateFile     = 'infra\main.bicep'

Write-Host ''
Write-Host '╔══════════════════════════════════════════════════════╗' -ForegroundColor Cyan
Write-Host '║   SmartNest — Azure Infrastructure Deployment        ║' -ForegroundColor Cyan
Write-Host '╠══════════════════════════════════════════════════════╣' -ForegroundColor Cyan
Write-Host "║  Environment   : $Environment"
Write-Host "║  Location      : $Location"
Write-Host "║  Resource Group: $ResourceGroup"
Write-Host '╚══════════════════════════════════════════════════════╝' -ForegroundColor Cyan
Write-Host ''

# ------------------------------------------------------------------
# 1. Ensure Resource Group
# ------------------------------------------------------------------
Write-Host '► Step 1/4 — Creating Resource Group ...' -ForegroundColor Yellow
az group create `
    --name $ResourceGroup `
    --location $Location `
    --tags project=smartnest environment=$Environment managedBy=bicep `
    --output table

if ($LASTEXITCODE -ne 0) { throw "Resource group creation failed." }

# ------------------------------------------------------------------
# 2. Validate
# ------------------------------------------------------------------
Write-Host ''
Write-Host '► Step 2/4 — Validating Bicep template ...' -ForegroundColor Yellow
az deployment group validate `
    --resource-group $ResourceGroup `
    --template-file $TemplateFile `
    --parameters "@$ParametersFile" `
    --output table

if ($LASTEXITCODE -ne 0) { throw "Template validation failed." }

# ------------------------------------------------------------------
# 3. What-if
# ------------------------------------------------------------------
Write-Host ''
Write-Host '► Step 3/4 — Running what-if preview ...' -ForegroundColor Yellow
az deployment group what-if `
    --resource-group $ResourceGroup `
    --template-file $TemplateFile `
    --parameters "@$ParametersFile"

Write-Host ''
$Confirm = Read-Host '► Proceed with actual deployment? [y/N]'
if ($Confirm -notmatch '^[Yy]$') {
    Write-Host 'Deployment cancelled.' -ForegroundColor Red
    exit 0
}

# ------------------------------------------------------------------
# 4. Deploy
# ------------------------------------------------------------------
Write-Host ''
Write-Host '► Step 4/4 — Deploying SmartNest infrastructure ...' -ForegroundColor Yellow
$DeploymentName = "smartnest-infra-$(Get-Date -Format 'yyyyMMddHHmmss')"

$OutputJson = az deployment group create `
    --resource-group $ResourceGroup `
    --template-file $TemplateFile `
    --parameters "@$ParametersFile" `
    --name $DeploymentName `
    --output json

if ($LASTEXITCODE -ne 0) { throw "Deployment failed." }

Write-Host ''
Write-Host '✔  Deployment complete.' -ForegroundColor Green
Write-Host ''
Write-Host '━━━ Outputs ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━' -ForegroundColor Cyan

$Outputs = ($OutputJson | ConvertFrom-Json).properties.outputs.PSObject.Properties
foreach ($o in $Outputs) {
    Write-Host "  $($o.Name): $($o.Value.value)"
}

Write-Host ''
Write-Host '⚠  Secrets (cosmosPrimaryKey, storageConnectionString, serviceBusFunctionsConnectionString)' -ForegroundColor Yellow
Write-Host '   should be moved to Azure Key Vault before provisioning Function Apps.' -ForegroundColor Yellow
