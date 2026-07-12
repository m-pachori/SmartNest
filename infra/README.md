# SmartNest — Azure Infrastructure

This directory contains all **Infrastructure-as-Code (IaC)** resources for the SmartNest platform,
written in [Azure Bicep](https://learn.microsoft.com/en-us/azure/azure-resource-manager/bicep/).

---

## Directory Layout

```
infra/
├── main.bicep                          # Top-level orchestration — deploys all modules
├── deploy.sh                           # Bash deployment script
├── deploy.ps1                          # PowerShell deployment script
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
│   └── dev.parameters.json             # Parameter values for the dev environment
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
