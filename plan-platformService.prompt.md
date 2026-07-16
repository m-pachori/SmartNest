## Plan: Tasks 5-9 — Automation, Alert, Audit, Summary, Media as one merged "Platform Service"

Implement the Automation, Alert, Audit (event store), Summary, and Media bounded contexts (Tasks 5-9 in `smartnest-plan.md`) as **one new Azure Function App backed by one hosting plan** — `SmartNest.PlatformService` — instead of five separate Function Apps, reusing the existing layered pattern (Domain/Handlers/Functions/Repositories/Persistence) and the shared `SmartNest.Shared` library established by Home/Device/Identity Services. This directly follows the earlier decision to avoid multiplying Function Apps/hosting plans for every new bounded context.

**Status: planned, not yet implemented.**

---

## Decisions (confirmed with user)

- Automation, Alert, Audit, Summary, Media (Tasks 5-9) ship as **one** new Function App + **one** hosting plan (not 5 separate apps). Existing Home/Device/Identity stay separate/unchanged.
- APIM registration skipped for now (`deployApim` stays `false`); HTTP functions use `AuthorizationLevel.Function` (function key), called directly like today's dev setup.
- Cosmos containers (`rules`, `alerts`, `audit-log`, `summaries`, `media-metadata`) and blob containers (`media-uploads`, `processed-media`, `snapshots`) **already exist** in `infra/modules/cosmos-db.bicep` and `infra/modules/storage.bicep` — no changes needed there.
- Service Bus subscriptions already exist: `device-events` {`automation`, `alert`, `audit`}, `home-events` {`audit`}, `user-events` {`audit`}. No new topics/subscriptions needed. The `media-processing` queue is removed (Task 9 replaces it with a blob trigger).
- The new merged app needs **one** Service Bus connection string with Listen+Send rights (namespace-wide) — replaces the currently-unused `AuditServiceListen` rule (nothing consumes it yet since audit-svc was never built) with a new `PlatformServiceSendListen` rule.
- Automation's "raise-alert" rule action calls the Alert bounded context **in-process** (same deployment, no Service Bus round-trip needed) since both now live in the same Function App.
- Automation's "trigger device state change" action calls Device Service's existing HTTP endpoint (`PATCH /devices/{id}/state`) via an internal `HttpClient` using Device Service's function key (requires a `main.bicep` change: make `deviceSvcFunctionKeySecret` always created, not just `if (deployApim)`).
- Notification delivery in Alert Service is stubbed (`ILogger`-based `INotificationSender`) — matches the plan's "can be stubbed" wording.
- Naming: folder `services/platform-service/`, project `SmartNest.PlatformService` (+ `.Tests`), Function App name default `${projectName}-platform-svc-${environment}`, hosting plan `${projectName}-platform-svc-plan-${environment}`.

## Reference patterns (from Home/Device/Identity services)

- Layered structure per bounded context: `Domain/` (aggregate + value objects + domain events), `Dtos/`, `Events/` (payloads + EventPublisher), `Functions/` (thin HTTP/trigger wrappers + `HttpFunctionHelpers`), `Handlers/` (business logic, Scoped DI), `Repositories/` (`I*Repository` + `Cosmos*Repository` extends `CosmosRepositoryBase<T>`), `Persistence/` (Document + Mapper).
- Shared lib (`services/shared/SmartNest.Shared`): `IEventPublisher`/`ServiceBusEventPublisher`, `EventEnvelope<T>`, `CurrentUser`, `AuthorizationGuard` (`RequireRole`/`RequireOwnership`), `UnauthorizedException`/`ForbiddenException`, `CosmosRepositoryBase<T>`. Reused as-is.
- `Program.cs`: Singleton `CosmosClient`/containers/`ServiceBusClient`/repos/publishers; Scoped handlers.
- Tests: xunit + Moq + FluentAssertions, mirrors `Handlers`/`Domain` folders.
- No Service Bus **trigger** functions exist yet anywhere in the repo — this is new for Tasks 5/6/7.

---

## Steps

### Phase 1 — Scaffolding (blocking, do first)
1. Create `services/platform-service/SmartNest.PlatformService/` project: csproj (net8.0, V4 isolated worker, same package set as `HomeService.csproj` plus `Microsoft.Azure.Functions.Worker.Extensions.ServiceBus`, `.Storage.Blobs`, `.Timer`), `host.json` (copy pattern), `local.settings.json.sample`, `Program.cs` skeleton (builder + OpenTelemetry block copied from HomeService), `Properties/launchSettings.json` if present in siblings.
2. Create `services/platform-service/SmartNest.PlatformService.Tests/` csproj mirroring `SmartNest.HomeService.Tests.csproj`.
3. Add both new projects to `services/SmartNest.sln`.
4. Bicep/infra changes (parallel with steps 6-36 code work):
   - `infra/modules/service-bus.bicep`: add `platformServiceSendListenRule` (rights: Listen, Send, namespace-wide) + secure output `platformServiceSendListenConnectionString`; remove unused `auditServiceListenRule` + its output.
   - `infra/modules/key-vault.bicep`: add param + secret `servicebus-platformsvc-sendlisten-connection-string` + output `serviceBusPlatformSvcSendListenSecretUri`; remove the now-unused `servicebus-auditsvc-listen-connection-string` secret/output/param.
   - `infra/main.bicep`: wire new KV param from service-bus module output; add `platformServiceFunctionAppName`/`platformServiceHostingPlanName` params (with defaults); add `platformFunctionAppModule` (reuses `modules/function-app.bicep`) with `additionalAppSettings` for all 5 container names + `Cosmos:HomesContainerName` (ownership checks) + `DeviceService:BaseUrl` + `DeviceService:FunctionKey` (KV ref using `deviceSvcFunctionKeySecret`); add KV role assignment for its managed identity; change `deviceSvcFunctionKeySecret` resource condition from `if (deployApim)` to always-created; add output `platformServiceFunctionAppName`.
   - `infra/modules/service-bus.bicep`: remove the `media-processing` queue resource (superseded by blob trigger per Task 9).
5. `azure-pipelines-code.yml`: add `platformServiceProject` variable, publish/archive/artifact steps in `BuildAndTest`, and `AzureFunctionApp@2` deploy steps per environment (dev/staging/prod), mirroring the Home Service steps exactly with `appName smartnest-platform-svc-{env}`.

### Phase 2 — Automation Service (Task 5) — parallel with Phases 3-6
6. Domain: `Domain/Automation/Rule.cs` (aggregate: RuleId, HomeId, Name, Condition, Action, Enabled), `Domain/Automation/ValueObjects/Condition.cs` (Field, Operator enum [GreaterThan/LessThan/Equals], Value — simple evaluator, no expression-language dependency), `Domain/Automation/ValueObjects/Action.cs` (ActionType enum [ChangeDeviceState/RaiseAlert], target fields), `Domain/Automation/Events/DomainEvents.cs`.
7. Persistence: `Persistence/Automation/RuleDocument.cs` + mapper (partition key `/homeId`).
8. Repository: `Repositories/Automation/IRuleRepository.cs` + `CosmosRuleRepository.cs` (extends `CosmosRepositoryBase<RuleDocument>`) + `GetRulesForDeviceEventAsync(homeId, deviceId)` query method.
9. Events: `Events/Automation/EventPayloads.cs` (`AutomationExecutedPayload`) + `AutomationEventPublisher.cs` (publishes to `device-events` topic, mirrors `HomeEventPublisher`).
10. Handlers: `Handlers/Automation/CreateRuleHandler.cs`, `GetRuleHandler.cs`, `UpdateRuleHandler.cs`, `DeleteRuleHandler.cs` (role=Owner + ownership check via `IHomeOwnershipRepository`, same pattern as Device/Identity); `EvaluateRulesHandler.cs` (Service Bus trigger handler — deserializes `DeviceStateChanged` envelope, loads rules for homeId+deviceId, evaluates Condition, executes Action: calls internal `IDeviceStateClient.UpdateStateAsync(...)` for ChangeDeviceState, or in-process `CreateAlertHandler`/`AlertRepository` for RaiseAlert; publishes `AutomationExecuted`).
11. Infra client: `Infrastructure/DeviceServiceClient.cs` (`IDeviceStateClient` — wraps `HttpClient`, base URL + function key from config) used by `EvaluateRulesHandler`.
12. Functions: `Functions/Automation/CreateRule.cs`/`GetRule.cs`/`UpdateRule.cs`/`DeleteRule.cs` (HTTP, route `/rules`), `Functions/Automation/EvaluateRules.cs` (`[ServiceBusTrigger("device-events","automation")]`).
13. Tests: `Tests/Domain/Automation/RuleTests.cs`, `Tests/Handlers/Automation/*HandlerTests.cs` (mock `IRuleRepository`/`IEventPublisher`/`IDeviceStateClient`).

### Phase 3 — Alert Service (Task 6) — parallel with Phases 2,4-6
14. Domain: `Domain/Alert/Alert.cs` (aggregate: AlertId, HomeId, DeviceId, Severity, Message, Acknowledged), `ValueObjects/AlertSeverity.cs` (enum), `ValueObjects/Recipient.cs`.
15. Persistence + Repository: `Persistence/Alert/AlertDocument.cs` + mapper, `Repositories/Alert/IAlertRepository.cs` + `CosmosAlertRepository.cs` (partition `/homeId`).
16. Notification stub: `Infrastructure/INotificationSender.cs` + `LoggingNotificationSender.cs` (logs via `ILogger`, placeholder for real channel).
17. Events: `Events/Alert/EventPayloads.cs` (`AlertRaisedPayload`, `AlertDeliveredPayload`) + `AlertEventPublisher.cs` (publishes to `device-events` topic for audit consumption).
18. Handlers: `Handlers/Alert/DispatchAlertsHandler.cs` (Service Bus trigger on `device-events`/`alert` sub — evaluate simple threshold conditions on `DeviceStateChanged` payload, create+persist Alert, call `INotificationSender`, publish `AlertRaised`/`AlertDelivered`); `CreateAlertHandler.cs` (in-process entry point reused by Automation's raise-alert action); `GetAlertsHandler.cs`, `AcknowledgeAlertHandler.cs` (Owner/Technician + ownership check).
19. Functions: `Functions/Alert/DispatchAlerts.cs` (ServiceBusTrigger), `Functions/Alert/GetAlerts.cs`, `AcknowledgeAlert.cs` (HTTP, route `/alerts`).
20. Tests: mirror pattern under `Tests/Domain/Alert`, `Tests/Handlers/Alert`.

### Phase 4 — Audit Service / Event Store (Task 7) — parallel with Phases 2,3,5,6
21. Domain/Persistence: `Persistence/Audit/AuditEntryDocument.cs` (fields per plan: eventId, eventType, aggregateId, aggregateType, occurredAt, actorId, homeId, correlationId, payload, sequenceNumber) + `SequenceCounterDocument.cs` (aggregateId, lastSequence) — both partition key `/aggregateId`.
22. Repository: `Repositories/Audit/IAuditRepository.cs` + `CosmosAuditRepository.cs` (append-only; `AppendAsync(entry)` uses Cosmos `TransactionalBatch` scoped to `partitionKey(aggregateId)` to increment `SequenceCounter` + insert `AuditEntry` atomically; `GetByAggregateAsync(aggregateId, fromSequence)` query ordered by sequenceNumber).
23. Handlers: `Handlers/Audit/AppendAuditLogHandler.cs` (shared logic: envelope -> AuditEntry -> AppendAsync), `GetAuditLogHandler.cs`, `ReplayEventsHandler.cs` (enforces `AuthorizationGuard.RequireRole(user, "SmartNest.Owner")` inside the handler, per plan's explicit "do not rely solely on APIM policy" requirement).
24. Functions: 3 Service Bus trigger functions — `Functions/Audit/AppendDeviceEvents.cs` (`[ServiceBusTrigger("device-events","audit")]`), `AppendHomeEvents.cs` (`"home-events","audit"`), `AppendUserEvents.cs` (`"user-events","audit"`) — all delegate to `AppendAuditLogHandler`; plus `GetAuditLog.cs`, `ReplayEvents.cs` (HTTP, route `/audit`).
25. Tests: `Tests/Repositories/Audit` (transactional batch behavior via Cosmos emulator or mocked container), `Tests/Handlers/Audit/ReplayEventsHandlerTests.cs` (Owner-only check).

### Phase 5 — Summary Service (Task 8) — parallel with Phases 2-4,6
26. Domain/Persistence: `Persistence/Summary/DailySummaryDocument.cs` (id/partitionKey = `{homeId}_{yyyy-MM-dd}`), `Repositories/Summary/ISummaryRepository.cs` + `CosmosSummaryRepository.cs` (upsert by `DailySummaryId`).
27. Handlers: `Handlers/Summary/GenerateDailySummaryHandler.cs` (Timer-triggered logic — queries `audit-log` container for previous 24h window via `occurredAt` range + groups by homeId/eventType, builds SummaryStats, upserts, publishes `SummaryGenerated`); `GetDailySummaryHandler.cs`.
28. Events: `Events/Summary/EventPayloads.cs` (`SummaryGeneratedPayload`) + `SummaryEventPublisher.cs`.
29. Functions: `Functions/Summary/GenerateDailySummary.cs` (`[TimerTrigger("0 0 0 * * *")]`), `Functions/Summary/GetDailySummary.cs` (HTTP, route `/summaries/{homeId}`).
30. Tests: `Tests/Handlers/Summary/GenerateDailySummaryHandlerTests.cs` (mock audit repository query + summary repository upsert).

### Phase 6 — Media Service (Task 9) — parallel with Phases 2-5
31. Domain/Persistence: `Persistence/Media/MediaMetadataDocument.cs` (BlobReference, MediaMeta — fields: id, homeId, deviceId, blobName, contentType, sizeBytes, processedAt), `Repositories/Media/IMediaRepository.cs` + `CosmosMediaRepository.cs` (partition `/homeId`; existence check by blob name for idempotency).
32. Infra client: `Infrastructure/BlobStorageClientFactory.cs` (builds `BlobServiceClient` from the `AzureWebJobsStorage` app setting — already a plain value per `function-app.bicep` comment; exposes `GetContainerClient` for `media-uploads`/`processed-media`).
33. Handlers: `Handlers/Media/UploadMediaHandler.cs` (validates Content-Type [image/jpeg, image/png, application/pdf] + size ≤ 10 MB, writes to `media-uploads/{deviceId}/{guid}.{ext}`, returns 202 immediately — no processing); `Handlers/Media/ProcessMediaHandler.cs` (idempotency check via MediaMetadata existence, extract metadata, copy to `processed-media/{name}`, delete source, persist metadata, publish `SnapshotProcessed`/`DocumentProcessed` event).
34. Events: `Events/Media/EventPayloads.cs` + `MediaEventPublisher.cs` (topic: reuse `device-events` since media is device-scoped, consistent with existing topology — see Further Considerations).
35. Functions: `Functions/Media/UploadMedia.cs` (HTTP, route `/media`, multipart handling, 413 on oversize), `Functions/Media/ProcessMedia.cs` (`[BlobTrigger("media-uploads/{name}")]`).
36. Tests: `Tests/Handlers/Media/UploadMediaHandlerTests.cs` (size/type validation), `ProcessMediaHandlerTests.cs` (idempotency skip when metadata already exists).

### Phase 7 — Integration & Final Wiring (depends on Phases 2-6)
37. `Program.cs` final assembly: per-context `Add{Context}Services(IServiceCollection, IConfiguration)` extension methods (one per bounded context, defined alongside each context's `Repositories` folder) called from `Program.cs` to keep it scannable; register `CosmosClient`/5 containers/homes-container (read-only)/`ServiceBusClient`/`BlobServiceClient` as Singletons; Scoped handlers.
38. Update `services/platform-service/SmartNest.PlatformService/local.settings.json.sample` with all new setting keys for local dev.
39. Update the Messaging Topology table in `smartnest-plan.md` removing the `media-processing` queue row (only if the user wants docs updated — confirm before editing).

---

## Relevant files

- `services/home-service/SmartNest.HomeService/**` — reference template (read-only reference)
- `services/shared/SmartNest.Shared/**` — reused as-is (Events, Security, Persistence)
- `services/platform-service/SmartNest.PlatformService/**` — new project (all of Phases 1-7)
- `services/platform-service/SmartNest.PlatformService.Tests/**` — new test project
- `services/SmartNest.sln` — add 2 new projects
- `infra/modules/service-bus.bicep` — add `PlatformServiceSendListen` rule, remove `AuditServiceListen` + `media-processing` queue
- `infra/modules/key-vault.bicep` — add platform svc secret, remove `auditsvc-listen` secret
- `infra/main.bicep` — new platform Function App module + role assignment + always-create `deviceSvcFunctionKeySecret` + new app settings + outputs
- `azure-pipelines-code.yml` — new build/publish/deploy stage steps for platform-service

## Verification

1. `dotnet build services/SmartNest.sln` succeeds with the 2 new projects included.
2. `dotnet test services/SmartNest.sln` — all new Handler/Domain tests pass (xunit).
3. `az bicep build --file infra/main.bicep` (or the Bicep MCP build tool) compiles with no errors after the service-bus/key-vault/main changes.
4. Manual/dev: deploy to dev via `azure-pipelines-infra.yml` then `azure-pipelines-code.yml`; call `POST /api/rules?code={key}` on the platform Function App, trigger a `DeviceStateChanged` via Device Service PATCH, confirm `EvaluateRules`/`DispatchAlerts`/`AppendAuditLog` Service Bus triggers fire (check Application Insights live metrics/logs).
5. Confirm `GET /api/audit/{aggregateId}` returns ordered sequence numbers with no gaps under concurrent test writes (validates `TransactionalBatch` atomicity).
6. Confirm re-uploading/re-delivering the same blob name to `ProcessMedia` does not duplicate `MediaMetadata` (idempotency check).

## Further Considerations

1. Media event topic: the plan reuses the `device-events` topic for `SnapshotProcessed`/`DocumentProcessed` events since no dedicated media topic/subscription is provisioned and Audit already subscribes there. Confirm this is acceptable, or provision a dedicated `media-events` topic + audit subscription instead (extra infra change).
2. `smartnest-plan.md` task/status checkboxes and the messaging topology table are left unedited unless you want them updated to reflect Tasks 5-9 completion and the `media-processing` queue removal.
