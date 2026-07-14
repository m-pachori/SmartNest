# SmartNest – Smart Home Management Platform
## High-Level Architecture & Implementation Plan

---

## Overview

SmartNest is a cloud-native smart home backend built entirely on Microsoft Azure Free Tier services.
It follows a microservices architecture, Domain-Driven Design principles, and Event Sourcing for
full state auditability. All compute runs on Azure Functions. Messaging uses Azure Service Bus topics
(pub/sub) and queues (dedicated processing). Identity is centralised via Azure AD (Entra ID) using
App Roles. Storage uses Azure Cosmos DB Serverless (documents) and Azure Blob Storage (media/files).
Observability is provided by Azure Application Insights and Azure Monitor.

**In-scope:** Backend only. No UI. All services deployed on Azure Free Tier.
**Out-of-scope:** Mobile apps, web frontends, third-party integrations.

---

## Azure Service Map

| Azure Service           | Purpose                                          | Free Tier / Trial Limit      |
|-------------------------|--------------------------------------------------|------------------------------|
| Azure Functions         | All microservice compute (HTTP, timer, message, blob triggers) | 1M executions/month |
| Azure Cosmos DB         | Document store — one logical database per bounded context | **Serverless** — pay-per-RU; no idle cost |
| Azure Service Bus       | Topic/subscription pub-sub + dedicated queues    | 10M operations/month         |
| Azure Blob Storage      | Snapshots, documents, media uploads              | 5 GB LRS                     |
| Azure AD (Entra ID)     | Centralised identity, JWT issuance, App Roles    | Free tier — no MAU charge    |
| Azure API Management    | Single ingress gateway, JWT validation policy    | Developer tier               |
| Azure Application Insights | Distributed tracing, structured logging, custom metrics | 5 GB/month — monitor weekly |
| Azure Monitor           | Dashboards, metric-based alerts                  | Free tier                    |

---

## Bounded Contexts & Aggregates

| Bounded Context   | Aggregates          | Value Objects              | Domain Events                                      |
|-------------------|---------------------|----------------------------|----------------------------------------------------|
| Home              | Home, Room          | Address, HomeSettings      | HomeCreated, RoomAdded, HomeDeleted                |
| Device            | Device, DeviceState | DeviceMetadata, StateValue | DeviceRegistered, DeviceStateChanged, DeviceRemoved|
| Identity/Access   | User, RoleAssignment| Email, Role                | UserInvited, RoleAssigned, UserDeactivated         |
| Automation        | Rule, Trigger       | Condition, Action          | RuleCreated, RuleTriggered, AutomationExecuted     |
| Alert             | Alert, Channel      | AlertSeverity, Recipient   | AlertRaised, AlertDelivered, AlertAcknowledged     |
| Audit             | AuditEntry          | EventEnvelope              | (append-only — consumes all domain events)         |
| Summary           | DailySummary        | UsagePeriod, SummaryStats  | SummaryGenerated                                   |
| Media             | Snapshot, Document  | BlobReference, MediaMeta   | SnapshotUploaded, DocumentProcessed                |

---

## Messaging Topology (Azure Service Bus)

| Topic / Queue                  | Type         | Publisher          | Subscribers / Consumers                        |
|--------------------------------|--------------|--------------------|------------------------------------------------|
| `device-events` topic          | Pub/Sub      | Device Service     | Automation, Alert, Audit Services              |
| `home-events` topic            | Pub/Sub      | Home Service       | Audit Service                                  |
| `user-events` topic            | Pub/Sub      | Identity Service   | Audit Service                                  |
| `media-processing` queue       | ~~Removed~~  | —                  | Replaced by Blob trigger (see Task 9)          |
| `summary-trigger` (timer)      | Timer cron   | Azure Functions    | Summary Service                                |

---

## Azure Functions Trigger Matrix

| Service            | Function Name             | Trigger Type     | Trigger Source                    |
|--------------------|---------------------------|------------------|-----------------------------------|
| Home Service       | ManageHomes               | HTTP             | API Management                    |
| Device Service     | ManageDevices             | HTTP             | API Management                    |
| Device Service     | UpdateDeviceState         | HTTP             | API Management                    |
| Identity Service   | ManageUsers               | HTTP             | API Management                    |
| Automation Service | EvaluateRules             | Service Bus      | `device-events` subscription      |
| Alert Service      | DispatchAlerts            | Service Bus      | `device-events` subscription      |
| Audit Service      | AppendAuditLog            | Service Bus      | All topics (multi-subscription)   |
| Audit Service      | ReplayEvents              | HTTP             | API Management **(Owner-only)**   |
| Summary Service    | GenerateDailySummary      | Timer            | CRON: `0 0 0 * * *` (midnight)    |
| Media Service      | ProcessMedia              | Blob             | Azure Blob Storage container      |
| Media Service      | UploadMedia               | HTTP             | API Management                    |

---

## RBAC Roles (Azure AD App Roles)

| Role        | Permissions                                                                          |
|-------------|--------------------------------------------------------------------------------------|
| Owner       | Full CRUD on home, rooms, devices, rules, alerts, users within their home            |
| Technician  | Read/write devices and device state; read-only on home/rules — scoped to their homeId |
| Guest       | Read-only on device state within their assigned homeId                               |

JWT tokens carry `roles` (App Role) and `homeId` (custom claim via optional claims config)
claims. API Management validates the token signature and audience. Downstream Functions
enforce both the `role` value and `homeId` matching against the resource being accessed.

---

## Event Sourcing Strategy

- The **Audit Service** is the dedicated event store.
- Every domain event published to Service Bus topics is also consumed by a dedicated
  `audit` subscription and appended as an immutable `AuditEntry` document in Cosmos DB.
- Each `AuditEntry` carries: `eventId`, `eventType`, `aggregateId`, `aggregateType`,
  `occurredAt`, `actorId`, `payload` (full event body), `sequenceNumber`.
- **`sequenceNumber` is assigned atomically** using a Cosmos DB transactional batch: a
  counter document keyed by `aggregateId` is incremented and the new `AuditEntry` is
  inserted in the same batch, preventing duplicate or conflicting sequence numbers.
- The `ReplayEvents` HTTP Function (Owner-only) accepts `aggregateId` + optional `fromSequence`
  and returns the ordered event stream — enabling full state reconstruction.
- Cosmos DB change feed can be used in future to project read models without re-processing.

### Standard Event Payload Schema

All domain events published to Service Bus **must** conform to this envelope to prevent
breaking changes during POC development:

```json
{
  "eventId":       "<uuid>",
  "eventType":     "DeviceStateChanged",
  "aggregateId":   "<deviceId>",
  "aggregateType": "Device",
  "occurredAt":    "2024-01-15T00:00:00Z",
  "actorId":       "<userId>",
  "homeId":        "<homeId>",
  "correlationId": "<uuid>",
  "payload": {
    "deviceId":    "<deviceId>",
    "homeId":      "<homeId>",
    "property":    "temperature",
    "oldValue":    "21",
    "newValue":    "25",
    "unit":        "celsius"
  }
}
```

All events share the top-level envelope fields. The `payload` object is event-type-specific
but must always include `deviceId` and `homeId` for `DeviceStateChanged` events.

---

## Observability Strategy

- All Azure Functions are instrumented with the Application Insights SDK.
- `TelemetryClient` emits:
  - **Structured logs** — request/response, domain event publish/consume
  - **Distributed traces** — correlation IDs propagated via Service Bus message properties
  - **Custom metrics** — `device.state.changes`, `automation.rules.triggered`, `alerts.dispatched`
- Azure Monitor is configured with:
  - Alert rule: App Insights exception rate > threshold
  - Dashboard: device event throughput, summary job status, media processing lag
- **Log verbosity control (cost protection):** Non-critical services (Automation, Summary, Media)
  are set to `Warning`/`Error` log level only. Device Service and Audit Service retain `Information`
  level. Monitor Application Insights ingestion weekly in Azure Portal → Usage and estimated costs
  to stay within the 5 GB/month free limit. Reduce sampling rate if ingestion approaches 4 GB.

---

## Sub-Tasks

---

### Task 1 — Azure Infrastructure Setup
**Status:** `[ ] pending`

**Intent:**
Provision all Azure services required by the platform within the Free Tier. This forms the
foundation that every microservice depends on.

**Expected Outcomes:**
- Azure Resource Group `smartnest-rg` created
- Azure AD (Entra ID) App Registration configured with Owner / Guest / Technician App Roles
  and `homeId` optional claim; no B2C tenant required
- Azure API Management instance deployed in Developer tier with JWT validation policy
- Azure Cosmos DB account with one database (`smartnest-db`) and one container per bounded context
- Azure Service Bus namespace with the `device-events`, `home-events`, `user-events` topics and
  their subscriptions (`automation`, `alert`, `audit`); plus `media-processing` queue
- Azure Blob Storage account with `media-uploads` container and `processed-media` container
- Azure Application Insights workspace linked to all Functions apps
- Azure Monitor dashboard and alert rules configured

**Todo List:**
- [ ] Create Resource Group `smartnest-rg` in a Free Tier eligible region (e.g. East US)
- [ ] In Azure AD (Entra ID): create App Registration `smartnest-api`; define App Roles:
  `SmartNest.Owner`, `SmartNest.Technician`, `SmartNest.Guest`
- [ ] Add `homeId` as an optional claim in the App Registration token configuration
- [ ] Create a second App Registration `smartnest-client` (for APIM / test clients) and grant
  it the app roles; no B2C tenant or user flows needed
- [ ] Deploy API Management (Developer tier); configure JWT validation inbound policy using
  the Entra ID OIDC metadata endpoint (`https://login.microsoftonline.com/{tenantId}/v2.0`)
- [ ] Create Cosmos DB account (**Serverless** capacity mode); create `smartnest-db`
- [ ] Create Cosmos DB containers: `homes`, `devices`, `users`, `rules`, `alerts`, `audit-log`, `summaries`, `media-metadata`
- [ ] Create Service Bus namespace (Standard tier); create topics + subscriptions as per topology table
- [ ] Create `media-processing` queue in the same namespace
- [ ] Create Storage account; create `media-uploads` and `processed-media` blob containers
- [ ] Create Application Insights workspace; note Instrumentation Key / Connection String
- [ ] Create Azure Monitor dashboard with widgets for key metrics
- [ ] Configure Azure Monitor alert rule on App Insights exception rate

**Relevant Context:**
- All services must remain within Free Tier / $200 trial credit quotas (see Azure Service Map table)
- Azure AD free tier supports App Roles and optional claims at no cost — no B2C tenant needed
- Service Bus Standard tier is required for topics (Basic only supports queues)
- Cosmos DB **Serverless** mode: no idle RU/s charge; billed only on actual reads/writes — optimal
  for POC workloads. Do not use provisioned throughput (1000 RU/s would cost ~$58/month)

---

### Task 2 — Home Service
**Status:** `[ ] pending`

**Intent:**
Implement the Home bounded context as an Azure Function App exposing HTTP endpoints
for CRUD operations on Homes and Rooms. Enforces Owner role. Publishes `HomeCreated`,
`RoomAdded`, and `HomeDeleted` domain events to the `home-events` Service Bus topic.

**Expected Outcomes:**
- Azure Function App `smartnest-home-svc` deployed
- HTTP endpoints: `POST /homes`, `GET /homes/{id}`, `PUT /homes/{id}`, `DELETE /homes/{id}`,
  `POST /homes/{id}/rooms`, `DELETE /homes/{id}/rooms/{roomId}`
- Home and Room aggregates persisted in Cosmos DB `homes` container
- Domain events published to `home-events` topic on every state change
- Application Insights telemetry enabled

**Todo List:**
- [ ] Scaffold Azure Functions project `home-service` (HTTP trigger, .NET 8 Isolated or Node.js)
- [ ] Define `Home` aggregate with value objects `Address` and `HomeSettings`
- [ ] Define `Room` entity within the Home aggregate
- [ ] Implement Cosmos DB repository for Home aggregate
- [ ] Implement HTTP Functions: CreateHome, GetHome, UpdateHome, DeleteHome, AddRoom, RemoveRoom
- [ ] Add role guard middleware: only `Owner` App Role may mutate; `Guest`/`Technician` may read;
  verify `homeId` claim matches the home being accessed
- [ ] Publish domain events (`HomeCreated`, `RoomAdded`, `HomeDeleted`) to Service Bus topic
- [ ] Wire Application Insights SDK; emit structured logs and custom metrics
- [ ] Register Function App in API Management with route prefix `/homes`
- [ ] Write integration tests against Cosmos DB emulator and Service Bus emulator

**Relevant Context:**
- RBAC enforced via JWT `roles` App Role claim + `homeId` claim; APIM validates token signature;
  Functions enforce role + homeId match
- Use Cosmos DB partition key `/homeId` for all home-related containers
- Domain event envelope must follow the standard schema defined in the Event Sourcing Strategy section

---

### Task 3 — Device Service
**Status:** `[ ] pending`

**Intent:**
Implement the Device bounded context. Core service of the platform — manages device
registration and processes state changes. Every state change publishes `DeviceStateChanged`
to the `device-events` topic, which fans out to Automation, Alert, and Audit services.

**Expected Outcomes:**
- Azure Function App `smartnest-device-svc` deployed
- HTTP endpoints: `POST /homes/{homeId}/devices`, `GET /devices/{id}`,
  `PATCH /devices/{id}/state`, `DELETE /devices/{id}`
- Device aggregate and DeviceState entity persisted in Cosmos DB `devices` container
- `DeviceStateChanged` event published to `device-events` topic on every PATCH
- Application Insights custom metric `device.state.changes` incremented on each state change

**Todo List:**
- [ ] Scaffold Azure Functions project `device-service`
- [ ] Define `Device` aggregate with `DeviceMetadata` value object
- [ ] Define `DeviceState` entity with `StateValue` value object (typed: on/off, temperature, etc.)
- [ ] Implement Cosmos DB repository for Device aggregate (partition key `/homeId`)
- [ ] Implement HTTP Functions: RegisterDevice, GetDevice, UpdateDeviceState, RemoveDevice
- [ ] On `UpdateDeviceState`: persist new state snapshot + publish `DeviceStateChanged` event
- [ ] Add role guards: Owner/Technician may mutate (verify `homeId` claim == device's homeId);
  Guest read-only (verify `homeId` claim matches)
- [ ] Emit custom metric `device.state.changes` via Application Insights TelemetryClient
- [ ] Register Function App in API Management with route prefix `/devices`
- [ ] Write integration tests

**Relevant Context:**
- `DeviceStateChanged` is the most important domain event — it triggers Automation, Alert, and Audit
- State history is not stored in the Device aggregate directly; the Audit service is the event store
- Correlation ID must be injected into the Service Bus message `CorrelationId` property for tracing
- `DeviceStateChanged` payload must conform to the standard event schema (see Event Sourcing Strategy)
- Technician role is scoped to their `homeId` claim; reject requests where the device's `homeId`
  does not match the `homeId` in the JWT

---

### Task 4 — Identity & Access Service
**Status:** `[ ] pending`

**Intent:**
Implement user management within homes (invite, assign roles, deactivate). Azure AD (Entra ID)
handles authentication and App Role assignment; this service manages the application-level
home-scoped role records stored in Cosmos DB and publishes identity events for audit.

**Expected Outcomes:**
- Azure Function App `smartnest-identity-svc` deployed
- HTTP endpoints: `POST /homes/{homeId}/users/invite`, `PUT /users/{id}/role`,
  `DELETE /homes/{homeId}/users/{userId}`
- Role assignments persisted in Cosmos DB `users` container
- Domain events `UserInvited`, `RoleAssigned`, `UserDeactivated` published to `user-events` topic

**Todo List:**
- [ ] Scaffold Azure Functions project `identity-service`
- [ ] Define `User` aggregate with `RoleAssignment` value object
- [ ] Implement Cosmos DB repository for User aggregate
- [ ] Implement HTTP Functions: InviteUser, UpdateUserRole, RemoveUser
- [ ] Validate that caller has Owner role before allowing role mutation
- [ ] On role change: update Cosmos DB record + publish domain event to `user-events` topic
- [ ] Wire Application Insights SDK
- [ ] Register in API Management with route prefix `/users`

**Relevant Context:**
- Azure AD (Entra ID) manages credentials and App Roles; this service manages the `homeId`
  scoping record that pairs a user's object ID to a specific home
- `roles` App Role claim + `homeId` optional claim in the JWT are the authoritative sources
  for runtime enforcement across all services

---

### Task 5 — Automation Service
**Status:** `[ ] pending`

**Intent:**
Implement the Automation bounded context. Consumes `DeviceStateChanged` events from the
`device-events` topic (via `automation` subscription). Evaluates rules defined by Owners.
Publishes `AutomationExecuted` events and can trigger device state changes via internal calls.

**Expected Outcomes:**
- Azure Function App `smartnest-automation-svc` deployed
- Service Bus triggered Function `EvaluateRules` reacts to every `DeviceStateChanged`
- HTTP endpoints for rule CRUD: `POST /rules`, `GET /rules/{id}`, `PUT /rules/{id}`, `DELETE /rules/{id}`
- Rules persisted in Cosmos DB `rules` container
- `AutomationExecuted` event published when a rule fires

**Todo List:**
- [ ] Scaffold Azure Functions project `automation-service`
- [ ] Define `Rule` aggregate with `Condition` and `Action` value objects
- [ ] Implement Cosmos DB repository for Rule aggregate
- [ ] Implement Service Bus triggered Function: load matching rules, evaluate conditions, execute actions
- [ ] Implement HTTP Functions: CreateRule, GetRule, UpdateRule, DeleteRule
- [ ] Publish `AutomationExecuted` event to `device-events` topic (for audit chain)
- [ ] Wire Application Insights; emit `automation.rules.triggered` custom metric
- [ ] Register HTTP Functions in API Management with route prefix `/rules`

**Relevant Context:**
- Rule conditions evaluate fields on `DeviceStateChanged` payload (e.g., `temperature > 30`)
- Actions can be: send-to-queue (trigger another device state change) or raise-alert
- Consume from `automation` subscription on `device-events` topic

---

### Task 6 — Alert Service
**Status:** `[ ] pending`

**Intent:**
Implement the Alert bounded context. Consumes `DeviceStateChanged` events (and optionally
`AutomationExecuted`) via the `alert` subscription. Evaluates alert rules, dispatches
notifications, and persists alert history in Cosmos DB.

**Expected Outcomes:**
- Azure Function App `smartnest-alert-svc` deployed
- Service Bus triggered Function `DispatchAlerts` consumes device events
- Alert records persisted in Cosmos DB `alerts` container
- `AlertRaised` and `AlertDelivered` events published for audit
- Application Insights `alerts.dispatched` custom metric

**Todo List:**
- [ ] Scaffold Azure Functions project `alert-service`
- [ ] Define `Alert` aggregate with `AlertSeverity` and `Recipient` value objects
- [ ] Implement Cosmos DB repository for Alert aggregate
- [ ] Implement Service Bus triggered Function: evaluate alert conditions, create Alert, dispatch
- [ ] Implement HTTP Functions: GetAlerts, AcknowledgeAlert (Owner/Technician only)
- [ ] Publish `AlertRaised` event to Service Bus for audit consumption
- [ ] Emit `alerts.dispatched` custom metric via Application Insights
- [ ] Register HTTP Functions in API Management with route prefix `/alerts`

**Relevant Context:**
- Notification delivery (email/SMS) can be stubbed or use Azure Communication Services (free tier)
- Consume from `alert` subscription on `device-events` topic

---

### Task 7 — Audit Service (Event Store)
**Status:** `[ ] pending`

**Intent:**
Implement the Audit bounded context as the platform's event store. Consumes all domain
events from all topics via dedicated `audit` subscriptions. Appends immutable `AuditEntry`
documents to Cosmos DB. Exposes a replay HTTP endpoint for state reconstruction.

**Expected Outcomes:**
- Azure Function App `smartnest-audit-svc` deployed
- Service Bus triggered Functions consuming `device-events/audit`, `home-events/audit`, `user-events/audit`
- Every domain event stored as an immutable `AuditEntry` in Cosmos DB `audit-log` container
- HTTP endpoint `GET /audit/{aggregateId}?from={sequence}` returns ordered event stream
- HTTP endpoint `POST /audit/replay/{aggregateId}` reconstructs aggregate state from events

**Todo List:**
- [ ] Scaffold Azure Functions project `audit-service`
- [ ] Define `AuditEntry` with fields: `eventId`, `eventType`, `aggregateId`, `aggregateType`,
  `occurredAt`, `actorId`, `homeId`, `correlationId`, `payload`, `sequenceNumber`
- [ ] Define a `SequenceCounter` document per `aggregateId` (fields: `aggregateId`, `lastSequence`)
- [ ] Implement Cosmos DB repository (append-only; no updates or deletes)
- [ ] **Assign `sequenceNumber` atomically**: use a Cosmos DB transactional batch that increments
  the `SequenceCounter` document and inserts the `AuditEntry` in a single batch operation,
  preventing duplicate or conflicting sequence numbers under concurrent writes
- [ ] Implement 3 Service Bus triggered Functions (one per topic subscription)
- [ ] Implement HTTP Functions: GetAuditLog, ReplayEvents
- [ ] **ReplayEvents is Owner-only**: enforce `SmartNest.Owner` App Role claim inside the Function
  (do not rely solely on APIM policy)
- [ ] Wire Application Insights
- [ ] Register HTTP Functions in API Management under `/audit` (restricted to Owner role policy)

**Relevant Context:**
- Cosmos DB partition key for `audit-log` should be `/aggregateId`
- Both `SequenceCounter` and `AuditEntry` documents must share the same partition key (`aggregateId`)
  for the transactional batch to work (Cosmos DB transactions are partition-scoped)
- Replay endpoint is admin-only — enforce `SmartNest.Owner` role in Function + APIM policy
- This is the single source of truth for historical state

---

### Task 8 — Summary Service (Scheduled Job)
**Status:** `[ ] pending`

**Intent:**
Implement a nightly scheduled Azure Function that aggregates device state change counts,
automation rule triggers, and alert counts per home into a `DailySummary` document in
Cosmos DB. Publishes `SummaryGenerated` event.

**Expected Outcomes:**
- Azure Function App `smartnest-summary-svc` deployed
- Timer triggered Function `GenerateDailySummary` runs at midnight UTC (`0 0 0 * * *`)
- Queries Cosmos DB `audit-log` for previous day's events grouped by homeId
- Persists `DailySummary` document in `summaries` container
- Publishes `SummaryGenerated` event to Service Bus
- HTTP endpoint `GET /summaries/{homeId}?date={date}` for retrieving summaries

**Todo List:**
- [ ] Scaffold Azure Functions project `summary-service`
- [ ] Define `DailySummary` aggregate with `UsagePeriod` and `SummaryStats` value objects;
  set `DailySummaryId = {homeId}_{YYYY-MM-DD}` (e.g. `home-42_2024-01-15`) as the document id
  and Cosmos DB partition key — ensures one document per home per day with no collisions
- [ ] Implement Cosmos DB repository for DailySummary (upsert by `DailySummaryId`)
- [ ] Implement Timer triggered Function with CRON `0 0 0 * * *`
- [ ] Query `audit-log` container for events in the previous 24-hour window
- [ ] Aggregate counts by homeId and event type; build `SummaryStats`
- [ ] Persist `DailySummary`; publish `SummaryGenerated` event
- [ ] Implement HTTP Function: GetDailySummary
- [ ] Wire Application Insights; log summary generation outcome
- [ ] Register HTTP Function in API Management under `/summaries`

**Relevant Context:**
- Timer triggers in Azure Functions use NCrontab format: `0 0 0 * * *` = daily at midnight UTC
- Query Cosmos DB `audit-log` using `occurredAt` range filter + `aggregateType` filter
- `DailySummaryId` format `{homeId}_{YYYY-MM-DD}` is the Cosmos DB document `id` and partition key;
  use this as an upsert key so re-runs are idempotent

---

### Task 9 — Media Service (Blob Processing)
**Status:** `[ ] pending`

**Intent:**
Implement background processing for device snapshots and uploaded documents using a single,
unambiguous two-step trigger path: an HTTP Function receives the upload and writes the raw
file to Blob Storage; a Blob trigger then fires automatically to process it. There is no
dual-path ambiguity — the HTTP Function only writes to blob storage and returns immediately;
all processing is done exclusively by the Blob trigger.

**Trigger path (definitive):**
```
Client → HTTP UploadMedia → writes to media-uploads/{name}
                                         ↓  (Blob trigger fires automatically)
                              Blob ProcessMedia → validate → extract metadata
                                               → copy to processed-media/
                                               → delete from media-uploads/
                                               → persist MediaMetadata in Cosmos DB
                                               → publish SnapshotProcessed event
```

**Expected Outcomes:**
- Azure Function App `smartnest-media-svc` deployed
- HTTP Function `UploadMedia` accepts multipart file upload, validates type and size (≤ 10 MB),
  writes raw file to `media-uploads` blob container, returns `202 Accepted` immediately
- Blob triggered Function `ProcessMedia` fires on every new blob in `media-uploads`;
  performs all processing asynchronously
- `MediaMetadata` document persisted in Cosmos DB `media-metadata` container
- Processed file available in `processed-media` blob container
- `SnapshotProcessed` / `DocumentProcessed` event published to Service Bus
- Application Insights logs each processing step with blob name and duration

**Todo List:**
- [ ] Scaffold Azure Functions project `media-service`
- [ ] Define `Snapshot` and `Document` aggregates with `BlobReference` and `MediaMeta` value objects
- [ ] Implement Cosmos DB repository for MediaMetadata
- [ ] Implement HTTP Function `UploadMedia`: validate Content-Type (image/jpeg, image/png, application/pdf
  only) and size ≤ 10 MB; write to `media-uploads/{deviceId}/{guid}.{ext}`; return `202 Accepted`
  with the blob name — do not trigger any processing here
- [ ] Implement Blob triggered Function `ProcessMedia` bound to `media-uploads/{name}`:
  extract metadata (file size, content type, deviceId from path), copy to `processed-media/{name}`,
  delete source blob from `media-uploads`, persist `MediaMetadata` doc, publish domain event
- [ ] The `media-processing` Service Bus queue is **removed** from the topology — the Blob trigger
  is the sole mechanism; update the Messaging Topology table accordingly
- [ ] Wire Application Insights; track custom metric `media.processed`
- [ ] Register `UploadMedia` in API Management under `/media`

**Relevant Context:**
- Blob trigger binding path: `media-uploads/{name}`
- The HTTP Function must return before any processing begins — it is a pure write-to-storage step
- The Blob trigger provides reliable at-least-once processing; idempotency is handled by checking
  whether a `MediaMetadata` document with the same blob name already exists before processing
- Maximum upload size: enforce 10 MB in the HTTP Function; reject with `413 Payload Too Large`

---

### Task 10 — API Management Configuration
**Status:** `[ ] pending`

**Intent:**
Configure Azure API Management as the single ingress for all microservices. Define APIs,
operations, products, JWT validation inbound policies, and rate limiting. Routes requests
to the correct Azure Function App backends.

**Expected Outcomes:**
- All 8 microservice backends registered in APIM
- JWT validation policy applied globally (validates Azure AD / Entra ID tokens)
- `ReplayEvents` operation explicitly marked Owner-only with a `roles` claim check policy
- Role-based routing policies applied per-operation where required
- Rate limiting policy (calls/minute per subscription key) applied globally
- Health-check endpoint `GET /health` configured
- APIM developer portal enabled for API documentation

**Todo List:**
- [ ] Create APIM API definitions for: Home, Device, Identity, Automation, Alert, Audit, Summary, Media
- [ ] Configure backend URLs pointing to each Azure Function App's function URL
- [ ] Apply global inbound policy: `validate-jwt` using Entra ID OIDC metadata endpoint
  (`https://login.microsoftonline.com/{tenantId}/v2.0/.well-known/openid-configuration`),
  audience = `smartnest-api` App Registration client ID
- [ ] Apply per-operation policy on `POST /audit/replay/{aggregateId}` and `GET /audit/{aggregateId}`:
  check `roles` claim contains `SmartNest.Owner`; return 403 otherwise
- [ ] Configure rate limiting: 100 calls/minute per IP
- [ ] Add `X-Correlation-ID` header injection policy (propagate to backends)
- [ ] Enable APIM developer portal; import OpenAPI specs from each Function App
- [ ] Configure APIM diagnostics to send logs to Application Insights

**Relevant Context:**
- APIM JWT validation policy uses Entra ID's OIDC metadata endpoint — not B2C
- `ReplayEvents` access control is enforced at two layers: APIM policy (role claim check)
  and inside the Function itself (defence in depth)
- Correlation ID injected at APIM level propagates through Service Bus message properties
  enabling end-to-end distributed tracing in Application Insights

---

### Task 11 — Observability & Monitoring Setup
**Status:** `[ ] pending`

**Intent:**
Ensure all services emit consistent structured logs, distributed traces, and custom metrics
to Azure Application Insights. Configure Azure Monitor dashboards and alert rules.

**Expected Outcomes:**
- All Function Apps share a single Application Insights workspace
- Distributed traces connect APIM → Function → Service Bus → downstream Function
- Custom metrics visible in App Insights Metrics explorer:
  `device.state.changes`, `automation.rules.triggered`, `alerts.dispatched`, `media.processed`
- Azure Monitor dashboard with 4 widgets: event throughput, alert rate, summary job status, error rate
- Azure Monitor alert rule: error rate > 5% over 5-minute window triggers email notification

**Todo List:**
- [ ] Confirm all Function Apps have `APPLICATIONINSIGHTS_CONNECTION_STRING` app setting
- [ ] Validate distributed trace correlation: trigger a device state change end-to-end and
  verify the full trace appears in App Insights Transaction Search
- [ ] Create Azure Monitor Workbook (dashboard) with the 4 required widgets
- [ ] Create Azure Monitor alert rule on App Insights `exceptions/count` metric
- [ ] Document the observability setup and metric names in `docs/observability.md`

**Relevant Context:**
- Use `TelemetryClient.TrackMetric()` for custom metrics in .NET
- Use `TelemetryClient.TrackEvent()` for domain event telemetry
- Operation ID correlation: set `Activity.Current` from Service Bus message's `Diagnostic-Id` property

---

## Final Deployment Checklist

- [ ] All Function Apps deployed to Azure (not just local)
- [ ] All environment variables (Cosmos DB connection strings, Service Bus connection strings,
  App Insights connection string, Entra ID tenant ID / client ID) stored in Azure Key Vault or Function App settings
- [ ] API Management frontend URL tested end-to-end for each major flow
- [ ] Event Sourcing replay tested: update device state 5 times, replay returns all 5 events in order
- [ ] Nightly summary job manually triggered via Azure Portal and verified in Cosmos DB
- [ ] Blob processing tested: upload image via API, verify processed-media container and Cosmos DB record
- [ ] RBAC tested: Guest token cannot PATCH device state (expect 403)
- [ ] Application Insights shows correlated traces across 3+ services for a single device state change

---

## Architecture Decision Records

### ADR-001: Azure Functions over Azure App Service
**Decision:** Use Azure Functions (Consumption plan) for all compute.
**Rationale:** Consumption plan falls within Free Tier (1M executions/month). App Service Basic
tier has a 750 compute-hour free limit and is less suited to event-driven workloads. Functions
provide native bindings for Service Bus, Blob Storage, and Timer triggers eliminating boilerplate.

### ADR-002: Cosmos DB Serverless over SQL / Table Storage
**Decision:** Use Azure Cosmos DB with serverless capacity mode.
**Rationale:** Cosmos DB offers a flexible document model suitable for heterogeneous aggregates.
Serverless mode costs only per request unit consumed — ideal for development workloads. Azure
Table Storage lacks query expressiveness needed for audit log range queries.

### ADR-003: Service Bus Standard over Event Hub
**Decision:** Use Azure Service Bus Standard for messaging.
**Rationale:** Service Bus topics with named subscriptions (Automation, Alert, Audit) provide
independent consumption without coordination. Event Hub is optimised for high-throughput telemetry
streaming and lacks subscription-level filtering. Service Bus Standard is available in the Free
Trial and supports the pub/sub model required.

### ADR-004: Dedicated Audit Service for Event Sourcing
**Decision:** Event Sourcing is implemented as a dedicated Audit Service, not within each aggregate's own store.
**Rationale:** A cross-cutting append-only event log provides a single replay point for any
aggregate in the system. This avoids duplicating event store logic across 6+ services and
enables future projections via Cosmos DB change feed.

### ADR-005: Azure AD (Entra ID) App Roles over Azure AD B2C
**Decision:** Use Azure AD (Entra ID) with App Roles and optional claims instead of Azure AD B2C.
**Rationale:** AD B2C requires a separate tenant, custom user flows, and custom policy XML for
role claims — adding significant setup cost for a POC. Azure AD free tier supports App Roles
(`SmartNest.Owner`, `SmartNest.Technician`, `SmartNest.Guest`) natively and allows `homeId` to
be added as an optional claim with zero additional configuration. APIM's `validate-jwt` policy
works identically with Entra ID tokens. This saves setup time and has no MAU cost within the
$200 trial credit budget.

### ADR-006: Blob Trigger as Sole Media Processing Path
**Decision:** Media Service uses HTTP upload → Blob write → Blob trigger only. The `media-processing`
Service Bus queue is removed from the topology.
**Rationale:** Having both a Service Bus queue and a Blob trigger as processing paths created
ambiguity about which would execute first and whether processing could run twice. The Blob trigger
fires automatically and reliably on every new blob, making the queue redundant. Idempotency is
handled inside `ProcessMedia` by checking for an existing `MediaMetadata` document.

### ADR-007: Cosmos DB Transactional Batch for Sequence Numbers
**Decision:** `sequenceNumber` in the Audit Service is assigned via a Cosmos DB transactional batch.
**Rationale:** Optimistic concurrency (etag) requires retry loops and can produce gaps under
contention. A transactional batch that atomically increments a `SequenceCounter` document and
inserts the `AuditEntry` in the same partition ensures strict monotonicity with no gaps or
duplicates — critical for reliable event replay.

### ADR-008: Cosmos DB Serverless Capacity Mode
**Decision:** Use Cosmos DB Serverless (not provisioned throughput).
**Rationale:** Provisioned throughput at 1000 RU/s costs approximately $58/month — a significant
portion of the $200 trial credit for a POC. Serverless mode charges only for RUs actually consumed,
making it essentially free for low-volume development and testing workloads.
