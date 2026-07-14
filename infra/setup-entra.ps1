#Requires -Version 5.1
<#
.SYNOPSIS
    SmartNest - Entra ID (Azure AD) Setup Script (PowerShell)

.DESCRIPTION
    Creates both App Registrations, defines App Roles, adds the homeId
    optional claim, and assigns roles to the client app.

    Run ONCE before the main Bicep deployment (idempotent - safe to re-run).
    Requires Azure CLI, logged in as Application Administrator.

    Fix H1: App Role IDs are now generated as proper random UUIDs at
            runtime - no more fake placeholder GUIDs.
    Fix H2: Script is idempotent - existing App Registrations, roles,
            claims, and role assignments are detected and skipped.

.EXAMPLE
    .\infra\setup-entra.ps1
#>
[CmdletBinding(SupportsShouldProcess)]
param()

$ErrorActionPreference = 'Stop'

Write-Host '====================================================' -ForegroundColor Cyan
Write-Host '   SmartNest - Entra ID Setup                       ' -ForegroundColor Cyan
Write-Host '====================================================' -ForegroundColor Cyan

# ------------------------------------------------------------------
# Step 1 - Create (or reuse) the API App Registration
# Fix H2: check for an existing registration before creating
# ------------------------------------------------------------------
Write-Host ''
Write-Host '>> Step 1/5 - Ensuring App Registration smartnest-api ...' -ForegroundColor Yellow

$ExistingApi = az ad app list --display-name 'smartnest-api' --query '[0].appId' -o tsv 2>$null
if ($ExistingApi) {
    Write-Host "  smartnest-api already exists - reusing appId: $ExistingApi"
    $ApiAppClientId = $ExistingApi
} else {
    $ApiAppJson = az ad app create `
        --display-name 'smartnest-api' `
        --sign-in-audience 'AzureADMyOrg' `
        --output json | ConvertFrom-Json
    $ApiAppClientId = $ApiAppJson.appId
    if (-not $ApiAppClientId) { throw 'Failed to create smartnest-api app registration. Check the az ad app create output above.' }
    Write-Host "  smartnest-api created - appId: $ApiAppClientId"

    # Set identifier URI using the appId — api://<appId> is always valid regardless of tenant domain
    az ad app update --id $ApiAppClientId --identifier-uris "api://$ApiAppClientId"
    Write-Host "  identifier URI set to api://$ApiAppClientId"
}

# ------------------------------------------------------------------
# Step 2 - Define App Roles on smartnest-api
# Fix H1: generate proper random UUIDs instead of placeholder values
# Fix H2: skip if roles are already present
# ------------------------------------------------------------------
Write-Host ''
Write-Host '>> Step 2/5 - Defining App Roles (Owner, Technician, Guest) ...' -ForegroundColor Yellow

$ExistingRoleValues = az ad app show --id $ApiAppClientId `
    --query 'appRoles[].value' -o tsv 2>$null

if ($ExistingRoleValues -and ($ExistingRoleValues -match 'SmartNest.Owner')) {
    Write-Host '  App Roles already defined - skipping.'
} else {
    # Fix H1: generate real random UUIDs at runtime
    $OwnerRoleId = [System.Guid]::NewGuid().ToString()
    $TechRoleId  = [System.Guid]::NewGuid().ToString()
    $GuestRoleId = [System.Guid]::NewGuid().ToString()

    # Write JSON to a temp file — avoids PowerShell shell-quoting issues with az CLI
    $AppRolesJson = @(
        @{
            allowedMemberTypes = @('Application','User')
            description        = 'Full CRUD on home, rooms, devices, rules, alerts, users within their home'
            displayName        = 'SmartNest Owner'
            id                 = $OwnerRoleId
            isEnabled          = $true
            value              = 'SmartNest.Owner'
        },
        @{
            allowedMemberTypes = @('Application','User')
            description        = 'Read/write devices and device state; read-only on home/rules'
            displayName        = 'SmartNest Technician'
            id                 = $TechRoleId
            isEnabled          = $true
            value              = 'SmartNest.Technician'
        },
        @{
            allowedMemberTypes = @('Application','User')
            description        = 'Read-only on device state within their assigned homeId'
            displayName        = 'SmartNest Guest'
            id                 = $GuestRoleId
            isEnabled          = $true
            value              = 'SmartNest.Guest'
        }
    ) | ConvertTo-Json -Depth 5
    $AppRolesFile = [System.IO.Path]::GetTempFileName()
    Set-Content -Path $AppRolesFile -Value $AppRolesJson -Encoding UTF8

    az ad app update --id $ApiAppClientId --app-roles "@$AppRolesFile"
    Remove-Item $AppRolesFile -Force
    Write-Host '  App Roles created.'
}

# ------------------------------------------------------------------
# Step 3 - Add homeId optional claim
# Fix H2: skip if claim already present
# ------------------------------------------------------------------
Write-Host ''
Write-Host '>> Step 3/5 - Adding homeId optional claim ...' -ForegroundColor Yellow

$ExistingClaims = az ad app show --id $ApiAppClientId `
    --query 'optionalClaims.accessToken[].name' -o tsv 2>$null

if ($ExistingClaims -and ($ExistingClaims -match 'homeId')) {
    Write-Host "  Optional claim 'homeId' already present - skipping."
} else {
    # Write JSON to a temp file — avoids PowerShell shell-quoting issues with az CLI
    $OptionalClaimsJson = @{
        accessToken = @( @{ name = 'homeId'; source = $null; essential = $false; additionalProperties = @() } )
        idToken     = @( @{ name = 'homeId'; source = $null; essential = $false; additionalProperties = @() } )
    } | ConvertTo-Json -Depth 5
    $OptionalClaimsFile = [System.IO.Path]::GetTempFileName()
    Set-Content -Path $OptionalClaimsFile -Value $OptionalClaimsJson -Encoding UTF8

    az ad app update --id $ApiAppClientId --optional-claims "@$OptionalClaimsFile"
    Remove-Item $OptionalClaimsFile -Force
    Write-Host "  Optional claim 'homeId' added."
}

# ------------------------------------------------------------------
# Step 4 - Create (or reuse) the client App Registration
# Fix H2: check for an existing registration before creating
# ------------------------------------------------------------------
Write-Host ''
Write-Host '>> Step 4/5 - Ensuring App Registration smartnest-client ...' -ForegroundColor Yellow

$ExistingClient = az ad app list --display-name 'smartnest-client' --query '[0].appId' -o tsv 2>$null
if ($ExistingClient) {
    Write-Host "  smartnest-client already exists - reusing appId: $ExistingClient"
    $ClientAppClientId = $ExistingClient
} else {
    $ClientAppJson = az ad app create `
        --display-name 'smartnest-client' `
        --sign-in-audience 'AzureADMyOrg' `
        --output json | ConvertFrom-Json
    $ClientAppClientId = $ClientAppJson.appId
    Write-Host "  smartnest-client created - appId: $ClientAppClientId"
}

# Ensure service principal exists for BOTH apps (idempotent - safe if already present)
try { az ad sp create --id $ApiAppClientId    --output none 2>$null } catch {}
try { az ad sp create --id $ClientAppClientId --output none 2>$null } catch {}

$ApiSpObjectId    = az ad sp show --id $ApiAppClientId    --query id -o tsv
$ClientSpObjectId = az ad sp show --id $ClientAppClientId --query id -o tsv

if (-not $ApiSpObjectId)    { throw "Could not resolve service principal for smartnest-api ($ApiAppClientId). Try running: az ad sp create --id $ApiAppClientId" }
if (-not $ClientSpObjectId) { throw "Could not resolve service principal for smartnest-client ($ClientAppClientId)." }

# ------------------------------------------------------------------
# Step 5 - Assign App Roles to smartnest-client service principal
# Fix H2: skip individual assignments that already exist
# ------------------------------------------------------------------
Write-Host ''
Write-Host '>> Step 5/5 - Assigning App Roles to smartnest-client ...' -ForegroundColor Yellow

$AssignedRoles = az rest `
    --method GET `
    --uri "https://graph.microsoft.com/v1.0/servicePrincipals/$ClientSpObjectId/appRoleAssignments" `
    --query "value[?resourceId=='$ApiSpObjectId'].appRoleId" `
    -o tsv 2>$null

# Read the actual role IDs from the app registration
$RoleIds = az ad app show --id $ApiAppClientId --query 'appRoles[].id' -o tsv

foreach ($RoleId in $RoleIds) {
    if ($AssignedRoles -and ($AssignedRoles -match $RoleId)) {
        Write-Host "  Role $RoleId already assigned - skipping."
    } else {
        # Write body to temp file to avoid PowerShell quoting issues
        # Add Content-Type header — required by MS Graph for POST requests
        $BodyJson = @{
            principalId = $ClientSpObjectId
            resourceId  = $ApiSpObjectId
            appRoleId   = $RoleId
        } | ConvertTo-Json
        $BodyFile = [System.IO.Path]::GetTempFileName()
        Set-Content -Path $BodyFile -Value $BodyJson -Encoding UTF8

        az rest `
            --method POST `
            --uri "https://graph.microsoft.com/v1.0/servicePrincipals/$ClientSpObjectId/appRoleAssignments" `
            --headers "Content-Type=application/json" `
            --body "@$BodyFile" `
            --output none
        Remove-Item $BodyFile -Force
        Write-Host "  Role $RoleId assigned."
    }
}

# ------------------------------------------------------------------
# Summary
# ------------------------------------------------------------------
$TenantId = az account show --query tenantId -o tsv

Write-Host ''
Write-Host '====================================================' -ForegroundColor Green
Write-Host '   Entra ID Setup Complete                          ' -ForegroundColor Green
Write-Host '   Add these to the smartnest-common variable group:' -ForegroundColor Green
Write-Host '====================================================' -ForegroundColor Green
Write-Host ''
Write-Host "  AZURE_TENANT_ID   = $TenantId"
Write-Host "  API_APP_CLIENT_ID = $ApiAppClientId"
Write-Host ''
Write-Host '>> Next: run .\infra\deploy.ps1  OR  trigger the Azure Pipeline' -ForegroundColor Cyan
