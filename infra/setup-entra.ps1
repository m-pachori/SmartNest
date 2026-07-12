#Requires -Version 5.1
<#
.SYNOPSIS
    SmartNest — Entra ID (Azure AD) Setup Script (PowerShell)

.DESCRIPTION
    Creates both App Registrations, defines App Roles, adds the homeId
    optional claim, and assigns roles to the client app.

    Run ONCE before the main Bicep deployment.
    Requires Azure CLI, logged in as Application Administrator.

.EXAMPLE
    .\infra\setup-entra.ps1
#>
[CmdletBinding(SupportsShouldProcess)]
param()

$ErrorActionPreference = 'Stop'

Write-Host '╔══════════════════════════════════════════════════════╗' -ForegroundColor Cyan
Write-Host '║   SmartNest — Entra ID Setup                         ║' -ForegroundColor Cyan
Write-Host '╚══════════════════════════════════════════════════════╝' -ForegroundColor Cyan

# ------------------------------------------------------------------
# Step 1 — Create the API App Registration (smartnest-api)
# ------------------------------------------------------------------
Write-Host ''
Write-Host '► Step 1/5 — Creating App Registration smartnest-api ...' -ForegroundColor Yellow

$ApiAppJson = az ad app create `
    --display-name 'smartnest-api' `
    --sign-in-audience 'AzureADMyOrg' `
    --identifier-uris 'api://smartnest-api' `
    --output json | ConvertFrom-Json

$ApiAppClientId  = $ApiAppJson.appId
$ApiAppObjectId  = $ApiAppJson.id
Write-Host "  smartnest-api appId : $ApiAppClientId"

# ------------------------------------------------------------------
# Step 2 — Define App Roles
# ------------------------------------------------------------------
Write-Host ''
Write-Host '► Step 2/5 — Defining App Roles (Owner, Technician, Guest) ...' -ForegroundColor Yellow

$AppRoles = @(
    @{
        allowedMemberTypes = @('Application','User')
        description        = 'Full CRUD on home, rooms, devices, rules, alerts, users within their home'
        displayName        = 'SmartNest Owner'
        id                 = '00000000-0000-0000-0000-000000000001'
        isEnabled          = $true
        value              = 'SmartNest.Owner'
    },
    @{
        allowedMemberTypes = @('Application','User')
        description        = 'Read/write devices and device state; read-only on home/rules'
        displayName        = 'SmartNest Technician'
        id                 = '00000000-0000-0000-0000-000000000002'
        isEnabled          = $true
        value              = 'SmartNest.Technician'
    },
    @{
        allowedMemberTypes = @('Application','User')
        description        = 'Read-only on device state within their assigned homeId'
        displayName        = 'SmartNest Guest'
        id                 = '00000000-0000-0000-0000-000000000003'
        isEnabled          = $true
        value              = 'SmartNest.Guest'
    }
) | ConvertTo-Json -Depth 5 -Compress

az ad app update `
    --id $ApiAppClientId `
    --app-roles $AppRoles

Write-Host '  App Roles created.'

# ------------------------------------------------------------------
# Step 3 — Add homeId optional claim
# ------------------------------------------------------------------
Write-Host ''
Write-Host '► Step 3/5 — Adding homeId optional claim ...' -ForegroundColor Yellow

$OptionalClaims = @{
    accessToken = @( @{ name = 'homeId'; source = $null; essential = $false; additionalProperties = @() } )
    idToken     = @( @{ name = 'homeId'; source = $null; essential = $false; additionalProperties = @() } )
} | ConvertTo-Json -Depth 5 -Compress

az ad app update `
    --id $ApiAppClientId `
    --optional-claims $OptionalClaims

Write-Host "  Optional claim 'homeId' added."

# ------------------------------------------------------------------
# Step 4 — Create smartnest-client App Registration
# ------------------------------------------------------------------
Write-Host ''
Write-Host '► Step 4/5 — Creating App Registration smartnest-client ...' -ForegroundColor Yellow

$ClientAppJson = az ad app create `
    --display-name 'smartnest-client' `
    --sign-in-audience 'AzureADMyOrg' `
    --output json | ConvertFrom-Json

$ClientAppClientId = $ClientAppJson.appId
Write-Host "  smartnest-client appId : $ClientAppClientId"

# Create service principal
az ad sp create --id $ClientAppClientId --output none

$ApiSpObjectId    = az ad sp show --id $ApiAppClientId    --query id -o tsv
$ClientSpObjectId = az ad sp show --id $ClientAppClientId --query id -o tsv

# ------------------------------------------------------------------
# Step 5 — Assign App Roles to smartnest-client service principal
# ------------------------------------------------------------------
Write-Host ''
Write-Host '► Step 5/5 — Assigning App Roles to smartnest-client ...' -ForegroundColor Yellow

$RoleIds = @(
    '00000000-0000-0000-0000-000000000001',
    '00000000-0000-0000-0000-000000000002',
    '00000000-0000-0000-0000-000000000003'
)

foreach ($RoleId in $RoleIds) {
    $Body = @{
        principalId = $ClientSpObjectId
        resourceId  = $ApiSpObjectId
        appRoleId   = $RoleId
    } | ConvertTo-Json -Compress

    az rest `
        --method POST `
        --uri "https://graph.microsoft.com/v1.0/servicePrincipals/$ClientSpObjectId/appRoleAssignments" `
        --body $Body `
        --output none
}
Write-Host '  Roles assigned.'

# ------------------------------------------------------------------
# Summary
# ------------------------------------------------------------------
$TenantId = az account show --query tenantId -o tsv

Write-Host ''
Write-Host '╔══════════════════════════════════════════════════════╗' -ForegroundColor Green
Write-Host '║   Entra ID Setup Complete                            ║' -ForegroundColor Green
Write-Host '╠══════════════════════════════════════════════════════╣' -ForegroundColor Green
Write-Host '║   Update infra\parameters\dev.parameters.json with: ║' -ForegroundColor Green
Write-Host '╚══════════════════════════════════════════════════════╝' -ForegroundColor Green
Write-Host ''
Write-Host "  `"tenantId`"       : `"$TenantId`""
Write-Host "  `"apiAppClientId`" : `"$ApiAppClientId`""
Write-Host ''
Write-Host '► Next: run .\infra\deploy.ps1' -ForegroundColor Cyan
