# SmartNest — Azure Infrastructure

This directory contains all **Infrastructure-as-Code (IaC)** resources for the SmartNest platform,
written in [Azure Bicep](https://learn.microsoft.com/en-us/azure/azure-resource-manager/bicep/).

---

## Directory Layout

```
/                                       # repo root
├── azure-pipelines-infra.yml           # Azure Pipelines CI/CD — infra (Bicep) only
├── azure-pipelines-code.yml            # Azure Pipelines CI/CD — service code build/deploy only
│
infra/
├── main.bicep                          # Top-level orchestration — deploys all modules
├── deploy.sh                           # Bash deployment script (local use)
├── deploy.ps1                          # PowerShell deployment script (local use)
├── setup-entra.sh                      # Entra ID (Azure AD) setup — Bash
├── setup-entra.ps1                     # Entra ID (Azure AD) setup — PowerShell
│
├── modules/
│   ├── cosmos-db.bicep                 # Cosmos DB Serverless + 8 containers
│   ├── service-bus.bicep               # Service Bus Standard + topics/subscriptions/queues
│   ├── storage.bicep                   # Blob Storage + media-uploads, processed-media, snapshots
│   ├── app-insights.bicep              # Log Analytics Workspace + Application Insights
│   ├── apim.bicep                      # API Management Developer tier + JWT global policy
│   ├── monitor.bicep                   # Azure Monitor Dashboard + Alert rules
│   └── entra-app-registration.bicep    # Documentation module — CLI commands for Entra ID setup
│
├── parameters/
│   ├── dev.parameters.json             # Parameter values for dev
│   ├── staging.parameters.json         # Parameter values for staging
│   └── prod.parameters.json            # Parameter values for prod
│
└── policies/
    └── jwt-validation.xml              # APIM JWT validation policy (reference copy)
```

---

## Prerequisites

| Tool | Version | Notes |
|------|---------|-------|
| [Azure CLI](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli) | ≥ 2.55 | `az bicep install` needed first time |
| Azure subscription | — | Free tier or Pay-As-You-Go with $200 trial credit |
| Application Administrator role | — | Required to run the Entra ID setup scripts |

---

## Quick Start

### 1. Login to Azure

```bash
az login
az account set --subscription "<your-subscription-name-or-id>"
az bicep install   # installs the Bicep transpiler into the CLI
```

### 2. Set up Entra ID (one-time)

> Entra ID App Registrations are tenant-level resources that cannot be created by ARM deployments.
> Run the setup script once before deploying infrastructure.

**Bash:**
```bash
chmod +x infra/setup-entra.sh
./infra/setup-entra.sh
```

**PowerShell:**
```powershell
.\infra\setup-entra.ps1
```

The script outputs two values — copy them into [`infra/parameters/dev.parameters.json`](parameters/dev.parameters.json):

```json
"tenantId":       "YOUR_TENANT_ID",
"apiAppClientId": "YOUR_API_APP_CLIENT_ID"
```

Also update `apimPublisherEmail` and `monitorAlertEmailAddress` with real addresses.

### 3. Deploy Infrastructure

**Bash:**
```bash
./infra/deploy.sh dev eastus
```

**PowerShell:**
```powershell
.\infra\deploy.ps1 -Environment dev -Location eastus
```

The script will:
1. Create the Resource Group `smartnest-rg`
2. Validate the Bicep template
3. Show a what-if preview
4. Ask for confirmation before deploying

---

## Azure Pipelines (CI/CD)

Infra and application code are deployed by **two separate pipelines** so infra changes and
service code changes can be reviewed, approved, and deployed independently:

- [`azure-pipelines-infra.yml`](../azure-pipelines-infra.yml) — deploys `infra/main.bicep` only.
  Triggered by changes under `infra/**`. Never touches application code.
- [`azure-pipelines-code.yml`](../azure-pipelines-code.yml) — builds, tests, and deploys service
  code only. Triggered by changes under `services/**`. Never runs `az deployment group create`;
  it assumes the target Function Apps already exist (created by the infra pipeline).

> Run the infra pipeline first on a fresh environment — the code pipeline deploys into
> Function Apps that must already exist.

### Infra Pipeline Stages (`azure-pipelines-infra.yml`)

| Stage | Trigger | What it does |
|-------|---------|--------------|
| **Validate** | Every push / PR | `az bicep build` lint + `az deployment group validate` against dev RG |
| **What-If** | Every push / PR | `az deployment group what-if` — shows planned changes, never modifies resources |
| **Deploy Dev** | `main` branch only, automatic | Deploys to `smartnest-rg-dev` |
| **Deploy Staging** | `main` branch, **manual approval** | Deploys to `smartnest-rg-staging` |
| **Deploy Prod** | After staging approved, **manual approval** | Deploys to `smartnest-rg-prod` |

### Code Pipeline Stages (`azure-pipelines-code.yml`)

| Stage | Trigger | What it does |
|-------|---------|--------------|
| **BuildAndTestHomeService** | Every push / PR | `dotnet build`/`test`/`publish`, archives the Function App package |
| **Deploy Dev** | `main` branch only, automatic | Deploys the package to the `smartnest-home-svc-dev` Function App |
| **Deploy Staging** | `main` branch, **manual approval** | Deploys to `smartnest-home-svc-staging` |
| **Deploy Prod** | After staging approved, **manual approval** | Deploys to `smartnest-home-svc-prod` |

### One-time Azure DevOps Setup

#### 1. Create a Service Connection

In **Project Settings → Service Connections**:

1. Click **New service connection → Azure Resource Manager**
2. Select **Service principal (automatic)** or **Workload Identity federation**
3. Scope to your subscription
4. Name it exactly **`smartnest-azure-sc`**
5. Grant the service principal **both** of the following roles at the subscription (or
   per-resource-group) scope — `Contributor` alone is **not** enough, because `main.bicep`
   creates a `Microsoft.Authorization/roleAssignments` resource (Key Vault Secrets User grant
   for the Function App's managed identity), which requires
   `Microsoft.Authorization/roleAssignments/write`:
   - `Contributor` — to create/manage resources
   - `User Access Administrator` — to create the role assignment(s)

   ```bash
   # Replace <sp-object-id> with the service connection's SP object id (shown in the
   # error message as "with object id '<...>'"), and <scope> with the subscription or
   # resource group scope (e.g. /subscriptions/<sub-id>/resourceGroups/smartnest-rg-dev)
   az role assignment create \
     --assignee-object-id <sp-object-id> \
     --assignee-principal-type ServicePrincipal \
     --role "User Access Administrator" \
     --scope <scope>
   ```

   Repeat for each environment's resource group (`smartnest-rg-dev`, `smartnest-rg-staging`,
   `smartnest-rg-prod`), or assign once at the subscription scope if the same service
   connection is used for all three.

   > This service connection is used by **both** pipelines. `azure-pipelines-code.yml` only
   > needs `Contributor` (it never creates role assignments) — if you'd rather use two
   > separate service connections for least privilege, name the code pipeline's connection
   > differently and update the `azureServiceConnection` variable in `azure-pipelines-code.yml`.

#### 2. Create the Variable Group

In **Pipelines → Library → + Variable group**, name it **`smartnest-common`** and add:

| Variable | Description | Secret? |
|----------|-------------|---------|
| `AZURE_SUBSCRIPTION_ID` | Azure subscription GUID | No |
| `AZURE_TENANT_ID` | Entra ID tenant GUID | No |
| `API_APP_CLIENT_ID` | App Registration client ID (from setup-entra script) | Yes |
| `APIM_PUBLISHER_EMAIL` | APIM publisher e-mail | No |
| `MONITOR_ALERT_EMAIL` | Alert notification e-mail | No |

#### 3. Create Pipeline Environments with Approval Gates

In **Pipelines → Environments**, create three environments and add an **Approvals** check to `staging` and `prod`:

| Environment name | Approval required |
|-----------------|-------------------|
| `smartnest-dev` | No (auto) |
| `smartnest-staging` | Yes |
| `smartnest-prod` | Yes |

#### 4. Register the Pipelines

1. Go to **Pipelines → New Pipeline → Azure Repos Git**
2. Select your repository
3. Choose **Existing Azure Pipelines YAML file**
4. Path: `/azure-pipelines-infra.yml` — name it e.g. `SmartNest-Infra`
5. Repeat: **New Pipeline** → same repo → path `/azure-pipelines-code.yml` — name it e.g. `SmartNest-Code`

> **Secrets in parameters files** — `tenantId`, `apiAppClientId`, `apimPublisherEmail`, and
> `monitorAlertEmailAddress` are intentionally omitted from the checked-in parameter files and
> are injected at pipeline runtime from the `smartnest-common` variable group.

---

## Resources Deployed

| Module | Azure Resource | Key Settings |
|--------|----------------|-------------|
| `cosmos-db.bicep` | Cosmos DB account | **Serverless** mode, free tier enabled |
| `cosmos-db.bicep` | `smartnest-db` database | 8 containers with optimal partition keys |
| `service-bus.bicep` | Service Bus namespace | **Standard** tier (topics require Standard+) |
| `service-bus.bicep` | Topics: `device-events`, `home-events`, `user-events` | Subscriptions: automation, alert, audit |
| `service-bus.bicep` | Queue: `media-processing` | Retained for optional future use |
| `storage.bicep` | Storage account | Standard LRS, HTTPS-only, no public blob access |
| `storage.bicep` | Containers: `media-uploads`, `processed-media`, `snapshots` | Private access only |
| `app-insights.bicep` | Log Analytics Workspace | 30-day retention, 200 MB/day ingestion cap |
| `app-insights.bicep` | Application Insights | Workspace-based, adaptive sampling |
| `apim.bicep` | API Management | **Developer** tier, system-assigned identity |
| `apim.bicep` | Global JWT policy | Validates Entra ID tokens, enforces role claim |
| `monitor.bicep` | Action Group | Email on alerts |
| `monitor.bicep` | Alert: exception rate | Fires when exceptions > 5 in 5-min window |
| `monitor.bicep` | Alert: ingestion warning | Fires when daily log ingestion > 150 MB |
| `monitor.bicep` | Dashboard | Device events, exceptions, automation, alerts widgets |

---

## Cosmos DB Containers & Partition Keys

| Container | Partition Key | Bounded Context |
|-----------|---------------|-----------------|
| `homes` | `/homeId` | Home |
| `devices` | `/homeId` | Device |
| `users` | `/homeId` | Identity/Access |
| `rules` | `/homeId` | Automation |
| `alerts` | `/homeId` | Alert |
| `audit-log` | `/aggregateId` | Audit (event store) |
| `summaries` | `/homeId` | Summary |
| `media-metadata` | `/homeId` | Media |

---

## Service Bus Topology

| Resource | Type | Subscriptions |
|----------|------|---------------|
| `device-events` | Topic | `automation`, `alert`, `audit` |
| `home-events` | Topic | `audit` |
| `user-events` | Topic | `audit` |
| `media-processing` | Queue | — |

---

## Free Tier Cost Controls

| Concern | Control |
|---------|---------|
| Cosmos DB idle cost | Serverless mode — charged per RU consumed, not provisioned |
| App Insights ingestion | 200 MB/day workspace cap; 25% APIM sampling; adaptive sampling on Functions |
| Service Bus operations | Standard tier: 10M ops/month free |
| Storage | Standard LRS: 5 GB free |
| Azure Functions compute | 1M executions/month free |

---

## Secrets & Key Vault

The Bicep outputs include secrets (Cosmos primary key, Service Bus connection string, Storage connection string).  
**Do not store these in source control.** After deployment:

1. Create an Azure Key Vault in `smartnest-rg`
2. Store the secrets from the deployment output
3. Configure Function App settings to reference Key Vault using managed identity

---

## Entra ID App Roles Summary

| Role | Value | Permissions |
|------|-------|-------------|
| Owner | `SmartNest.Owner` | Full CRUD on home, rooms, devices, rules, alerts, users |
| Technician | `SmartNest.Technician` | Read/write devices; read-only home/rules |
| Guest | `SmartNest.Guest` | Read-only device state within assigned `homeId` |

JWT tokens carry `roles` (App Role) and `homeId` (optional claim).  
APIM validates the token; downstream Functions enforce role + homeId matching.
