# SmartNest — Application Workflow Diagram

This document visualizes how a request/event actually flows through the SmartNest backend,
based on the implemented services (`home-service`, `device-service`, `identity-service`,
`platform-service`) and the Service Bus / Blob Storage topology in [`infra/`](../infra).

## 1. Overall Request & Event Flow

```mermaid
flowchart TB
    Client(["Client<br/>(HTTP + JWT)"])
    Entra["Azure AD (Entra ID)<br/>JWT · App Roles · homeId claim"]
    APIM["Azure API Management<br/>validate-jwt · rate-limit · route"]

    Client -- "1 - sign in" --> Entra
    Entra -- "2 - JWT bearer token" --> Client
    Client -- "3 - HTTPS + Bearer JWT" --> APIM

    subgraph HTTP["HTTP Microservices (Azure Functions · HTTP Trigger)"]
        Home["Home Service<br/>/homes"]
        Device["Device Service<br/>/devices, /devices/{id}/state"]
        Identity["Identity Service<br/>/users"]
        Rules["Platform Service · Automation<br/>/rules"]
        Alerts["Platform Service · Alert<br/>/alerts"]
        AuditHttp["Platform Service · Audit<br/>/audit (Owner-only replay)"]
        Summaries["Platform Service · Summary<br/>/summaries/{homeId}"]
        Media["Platform Service · Media<br/>/media (upload)"]
    end

    APIM --> Home
    APIM --> Device
    APIM --> Identity
    APIM --> Rules
    APIM --> Alerts
    APIM --> AuditHttp
    APIM --> Summaries
    APIM --> Media

    subgraph Bus["Azure Service Bus — Topics & Subscriptions"]
        DeviceTopic["device-events topic<br/>subs: automation · alert · audit"]
        HomeTopic["home-events topic<br/>sub: audit"]
        UserTopic["user-events topic<br/>sub: audit"]
    end

    Home -- "HomeCreated / RoomAdded / HomeDeleted" --> HomeTopic
    Device -- "DeviceRegistered / DeviceStateChanged / DeviceRemoved" --> DeviceTopic
    Identity -- "UserInvited / RoleAssigned / UserDeactivated" --> UserTopic

    subgraph Consumers["Service Bus — Consumer Functions"]
        EvaluateRules["EvaluateRules<br/>(automation sub)"]
        DispatchAlerts["DispatchAlerts<br/>(alert sub)"]
        AppendDeviceEvents["AppendDeviceEvents<br/>(audit sub)"]
        AppendHomeEvents["AppendHomeEvents<br/>(audit sub)"]
        AppendUserEvents["AppendUserEvents<br/>(audit sub)"]
    end

    DeviceTopic --> EvaluateRules
    DeviceTopic --> DispatchAlerts
    DeviceTopic --> AppendDeviceEvents
    HomeTopic --> AppendHomeEvents
    UserTopic --> AppendUserEvents

    CosmosAudit[("Cosmos DB<br/>audit-log container<br/>(TransactionalBatch seq. numbers)")]
    AppendDeviceEvents --> CosmosAudit
    AppendHomeEvents --> CosmosAudit
    AppendUserEvents --> CosmosAudit
    AuditHttp -- "ReplayEvents (Owner)" --> CosmosAudit

    EvaluateRules -- "action: ChangeDeviceState<br/>(HTTP, function key)" --> Device
    EvaluateRules -- "action: RaiseAlert<br/>(in-process call)" --> DispatchAlerts

    Timer(["Timer Trigger<br/>0 0 0 * * * (midnight UTC)"])
    Timer --> GenerateSummary["GenerateDailySummary"]
    GenerateSummary -- "query prior 24h" --> CosmosAudit
    GenerateSummary --> CosmosSummary[("Cosmos DB<br/>summaries container")]

    UploadMedia["UploadMedia (HTTP)"] -- "write blob" --> BlobUploads[("Blob Storage<br/>media-uploads")]
    BlobUploads -- "BlobTrigger" --> ProcessMedia["ProcessMedia"]
    ProcessMedia -- "move + persist metadata" --> BlobProcessed[("Blob Storage<br/>processed-media")]
    ProcessMedia --> CosmosMedia[("Cosmos DB<br/>media-metadata container")]
    Media --> UploadMedia

    AppInsights["Application Insights<br/>traces · metrics · logs"]
    Monitor["Azure Monitor<br/>alerts · dashboard"]
    HTTP -.-> AppInsights
    Consumers -.-> AppInsights
    GenerateSummary -.-> AppInsights
    ProcessMedia -.-> AppInsights
    AppInsights --> Monitor
```

## 2. Scenario Sequence — Device State Change → Automation → Alert → Audit

The most representative end-to-end workflow: a device state change fans out through the
`device-events` topic to three independent subscribers.

```mermaid
sequenceDiagram
    actor U as Client (Owner/Technician)
    participant APIM as API Management
    participant Dev as Device Service
    participant Bus as device-events topic
    participant Auto as EvaluateRules (automation sub)
    participant Alert as DispatchAlerts (alert sub)
    participant Audit as AppendDeviceEvents (audit sub)
    participant Cosmos as Cosmos DB (audit-log)

    U->>APIM: PATCH /devices/{id}/state (JWT)
    APIM->>Dev: forward request
    Dev->>Dev: validate role + homeId ownership
    Dev->>Bus: publish DeviceStateChanged
    Dev-->>U: 200 OK

    par fan-out to 3 subscriptions
        Bus->>Auto: DeviceStateChanged
        Auto->>Auto: load rules for homeId+deviceId, evaluate Condition
        alt action = ChangeDeviceState
            Auto->>Dev: PATCH /devices/{id}/state (function key)
        else action = RaiseAlert
            Auto->>Alert: CreateAlertHandler (in-process)
        end
        Auto->>Bus: publish AutomationExecuted
    and
        Bus->>Alert: DeviceStateChanged
        Alert->>Alert: evaluate threshold, create Alert
        Alert->>Alert: INotificationSender (stub)
        Alert->>Bus: publish AlertRaised / AlertDelivered
    and
        Bus->>Audit: DeviceStateChanged
        Audit->>Cosmos: TransactionalBatch (increment seq, append AuditEntry)
    end
```

## 3. Scenario Sequence — Media Upload & Processing

```mermaid
sequenceDiagram
    actor U as Client
    participant APIM as API Management
    participant Upload as UploadMedia (HTTP)
    participant Blob as Blob Storage (media-uploads)
    participant Process as ProcessMedia (Blob trigger)
    participant Processed as Blob Storage (processed-media)
    participant Cosmos as Cosmos DB (media-metadata)

    U->>APIM: POST /media (multipart, JWT)
    APIM->>Upload: forward request
    Upload->>Upload: validate content-type + size (<=10MB)
    Upload->>Blob: write media-uploads/{deviceId}/{guid}.{ext}
    Upload-->>U: 202 Accepted

    Blob-->>Process: BlobTrigger fires
    Process->>Cosmos: idempotency check (existing metadata?)
    alt not yet processed
        Process->>Processed: copy blob
        Process->>Blob: delete source
        Process->>Cosmos: persist MediaMetadata
        Process->>Process: publish SnapshotProcessed/DocumentProcessed
    else already processed
        Process->>Process: skip (idempotent)
    end
```

## 4. Daily Summary Workflow

```mermaid
sequenceDiagram
    participant Timer as Timer Trigger (midnight UTC)
    participant Gen as GenerateDailySummary
    participant Cosmos as Cosmos DB (audit-log)
    participant Sum as Cosmos DB (summaries)

    Timer->>Gen: fire (0 0 0 * * *)
    Gen->>Cosmos: query prior 24h window, group by homeId/eventType
    Gen->>Gen: build SummaryStats
    Gen->>Sum: upsert DailySummary
    Gen->>Gen: publish SummaryGenerated
```

## Notes

- All HTTP services enforce role (`SmartNest.Owner` / `SmartNest.Technician` / `SmartNest.Guest`)
  and `homeId` ownership via `AuthorizationGuard`, in addition to APIM's `validate-jwt` policy.
- Automation, Alert, Audit, Summary, and Media are merged into a single Function App
  (`SmartNest.PlatformService`) sharing one hosting plan — see
  [`plan-platformService.prompt.md`](../plan-platformService.prompt.md).
- The `ReplayEvents` endpoint (`/audit`) is Owner-only and reconstructs an aggregate's full
  event stream ordered by `sequenceNumber`.
- Source diagram lives alongside [`smartnest-architecture-diagram.html`](../smartnest-architecture-diagram.html),
  which renders the static topology as hand-built SVG; this file focuses on dynamic
  request/event *workflows* using Mermaid so it renders directly in GitHub/VS Code previews.
