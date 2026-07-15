## Plan: Task 3 — Device Service Implementation

Implement the Device bounded context as a .NET 8 Isolated Azure Functions app (`services/device-service/`), reusing the existing `SmartNest.Shared` library, with Cosmos DB persistence (`devices` container, partition key `/homeId` — already provisioned in Task 1), `device-events` Service Bus publishing (using the already-provisioned least-privilege `DeviceServiceSend` connection string), JWT/`homeId`-claim authorization, unit tests, and the supporting infra (Function App hosting + APIM registration, reusing the generic modules built in Task 2) plus CI/CD pipeline updates. This is the platform's core service — `DeviceStateChanged` is the event that fans out to Automation, Alert, and Audit.

**Status: implemented.** No Phase A (shared library) was needed — `SmartNest.Shared` (`EventEnvelope`, `IEventPublisher`/`ServiceBusEventPublisher`, `CurrentUser`, `AuthorizationGuard`, `CosmosRepositoryBase<T>`) is reused as-is from Task 2. `infra/modules/function-app.bicep` and `infra/modules/apim-api.bicep` (built generically in Task 2) were reused with a second instantiation each — no new Bicep modules were required. `infra/modules/service-bus.bicep`, `cosmos-db.bicep`, and `key-vault.bicep` already provisioned the `device-events` topic (+ `automation`/`alert`/`audit` subscriptions), the `devices` Cosmos container (`/homeId`), and the `servicebus-devicesvc-send-connection-string` Key Vault secret from Task 1 — no changes were needed there.

**Steps**

**Phase A — Device domain & persistence**
1. Scaffolded `services/device-service/SmartNest.DeviceService` (Functions Worker, isolated model, HTTP triggers), referencing `SmartNest.Shared`, added to `services/SmartNest.sln`.
2. Added `host.json`, `local.settings.json.sample`, `.gitignore`, `Properties/launchSettings.json` — copied the pattern from `services/home-service/SmartNest.HomeService`.
3. Domain model: `Device` aggregate (`DeviceId`, `HomeId`, `DeviceMetadata` VO, `DeviceState?` entity, timestamps) with invariant methods (`Register`, `UpdateState`, `MarkRemoved`) raising domain events; `DeviceMetadata` value object (name, type, manufacturer, model); `StateValue` value object supporting typed state (bool on/off, numeric+unit, text) per smartnest-plan.md's `DeviceStateChanged` payload example (`property`/`oldValue`/`newValue`/`unit`).
4. `IDeviceRepository` + `CosmosDeviceRepository` (container `devices`, partition key `/homeId`, built on `CosmosRepositoryBase<DeviceDocument>`).
5. `DeviceEventPublisher` (wraps shared `IEventPublisher`, targets `device-events` topic) builds `DeviceRegistered`, `DeviceStateChanged`, `DeviceRemoved` payloads per the standard envelope.

**Phase B — HTTP Functions + authorization**
6. Implemented thin Function triggers + testable handler classes (`RegisterDeviceHandler`, `GetDeviceHandler`, `UpdateDeviceStateHandler`, `RemoveDeviceHandler`), mirroring Home Service's handler-per-operation pattern (ADR-010) — no mediator/CQRS.
7. Wired endpoints: `POST /homes/{homeId}/devices`, `GET /devices/{id}`, `PATCH /devices/{id}/state`, `DELETE /devices/{id}`.
8. Applied `AuthorizationGuard`:
   - `RegisterDevice`: `RequireRole(user, "SmartNest.Owner", "SmartNest.Technician")`, then `RequireHomeIdMatch(user, homeId)` (route param).
   - `UpdateDeviceState`, `RemoveDevice`: load the device first (cross-partition query by id — see Decisions), then `RequireRole(user, "SmartNest.Owner", "SmartNest.Technician")` + `RequireHomeIdMatch(user, device.HomeId)`.
   - `GetDevice`: any authenticated caller (no role restriction) + `RequireHomeIdMatch(user, device.HomeId)`.
9. `Program.cs`: DI registration (CosmosClient/Container for `devices`, ServiceBusClient using the `DeviceServiceSend` connection string, repository, publisher, Azure Monitor OpenTelemetry for the Functions Worker, including the custom metrics meter).
10. `device.state.changes` custom metric emitted from `UpdateDeviceStateHandler` via a `System.Diagnostics.Metrics.Counter<long>` (`Telemetry/DeviceMetrics.cs`), registered with the OpenTelemetry `MeterProvider` in `Program.cs` (`.WithMetrics(m => m.AddMeter(DeviceMetrics.MeterName))`) — consistent with the OpenTelemetry-based instrumentation already used for tracing, not the classic `TelemetryClient`.

**Phase C — Unit tests**
11. `SmartNest.DeviceService.Tests`: handler tests (Moq `IDeviceRepository`, `IEventPublisher`) covering success paths, Owner/Technician mutate enforcement, Guest-mutate 403 (`ForbiddenException`), `homeId`-mismatch 403, not-found 404 (`KeyNotFoundException`) — 20 handler tests across 4 handler classes.
12. Domain tests: `Device` aggregate invariants (register/update-state/mark-removed raise the correct events, old-value carried forward on subsequent state changes) + `StateValue` factory validation — 4 domain tests. All 24 tests pass (`dotnet test services/device-service/SmartNest.DeviceService.Tests`).

**Phase D — Infra additions**
13. Added `deviceServiceFunctionAppName` (`${projectName}-device-svc-${environment}`) and `deviceServiceHostingPlanName` params to `infra/main.bicep`, following the `homeServiceFunctionAppName`/`homeServiceHostingPlanName` pattern.
14. Instantiated `modules/function-app.bicep` again as `deviceFunctionAppModule` (`serviceName: 'device'`), after `homeApiModule`. Wired `serviceBusConnectionStringSecretUri` to `keyVaultModule.outputs.serviceBusDeviceSvcSendSecretUri` (the dedicated send-only secret — **not** the broader `serviceBusFunctionsSecretUri` that Home Service uses — least privilege, since Device Service only ever publishes, never listens). Set `additionalAppSettings: { 'Cosmos:DevicesContainerName': 'devices' }`.
15. Added a second `Microsoft.Authorization/roleAssignments` resource (`deviceFunctionAppKvRoleAssignment`, Key Vault Secrets User) for the new Function App's managed identity, mirroring `homeFunctionAppKvRoleAssignment`.
16. Added a `device-svc-function-key` Key Vault secret (from `deviceFunctionAppModule.outputs.defaultFunctionKey`), mirroring `homeSvcFunctionKeySecret`.
17. Instantiated `modules/apim-api.bicep` again as `deviceApiModule` (`apiName: 'devices'`), reusing the existing `smartnest-backend` product, with operations: `register-device` (`POST /homes/{homeId}/devices`), `get-device` (`GET /devices/{id}`), `update-device-state` (`PATCH /devices/{id}/state`), `remove-device` (`DELETE /devices/{id}`).
18. Added `deviceServiceFunctionAppName`/`deviceServiceFunctionAppDefaultHostName`/`deviceServiceApiName` outputs at the bottom of `main.bicep`, mirroring the Home Service outputs.
19. No changes needed to `infra/parameters/*.parameters.json` — all new params default from `projectName`/`environment`.
20. `az bicep build --file infra/main.bicep` compiles cleanly (no new errors introduced; pre-existing warnings unrelated to this change remain).

**Phase E — CI/CD**
21. Extended `azure-pipelines-code.yml`'s `BuildAndTestHomeService` stage (kept the stage id for Azure DevOps history continuity; renamed `displayName` to "Build & Test — Home + Device Services") — the existing `dotnet test $(homeServiceSolution)` step already covers `SmartNest.DeviceService.Tests` once it's part of the solution, so no separate test step was needed. Added `dotnet publish`/`ArchiveFiles@2`/`publish` steps producing a `device-service-package` artifact, mirroring the Home Service steps.
22. Extended `DeployDev`/`DeployStaging`/`DeployProd` stages with a `download` + `AzureFunctionApp@2` step pair deploying the Device Service package to `smartnest-device-svc-{env}`, placed after the existing Home Service deploy steps, gated identically. `appType` matches whatever OS the Function App hosting plan currently uses (`functionAppLinux`, per `infra/modules/function-app.bicep`'s current Linux Consumption configuration).

**Relevant files**
- `services/device-service/SmartNest.DeviceService/**`, `services/device-service/SmartNest.DeviceService.Tests/**` — new
- `services/SmartNest.sln` — new projects added (with a `device-service` solution folder)
- `services/shared/SmartNest.Shared/**` — reference only, no changes
- `infra/main.bicep` — added `deviceFunctionAppModule` + KV role assignment + function-key secret + `deviceApiModule` instance + outputs
- `infra/modules/function-app.bicep`, `infra/modules/apim-api.bicep`, `infra/modules/service-bus.bicep`, `infra/modules/key-vault.bicep`, `infra/modules/cosmos-db.bicep` — reference only, already provisioned in Task 1/2
- `azure-pipelines-code.yml` — extended build/test job + added Device Service deploy steps to all three deploy stages

**Verification**
1. `dotnet test services/SmartNest.sln` passes locally (shared + home-service + device-service unit tests) — confirmed: 24/24 Device Service tests pass; full solution builds with 0 warnings, 0 errors.
2. `az bicep build --file infra/main.bicep` succeeds — confirmed, no new errors.
3. Manual (pending actual Azure deployment): deploy to dev, call `POST /homes/{homeId}/devices` via APIM with an Owner-role JWT carrying a matching `homeId` claim → `201`; same call with mismatched `homeId` claim → `403`.
4. Manual: `PATCH /devices/{id}/state` with a Guest-role JWT → `403`; with Technician-role JWT (matching `homeId`) → `200`.
5. Manual: confirm a `DeviceStateChanged` message lands on `device-events` (all three subscriptions: `automation`, `alert`, `audit`), matching the standard event envelope schema with `property`/`oldValue`/`newValue`/`unit` in the payload.
6. Manual: Application Insights Metrics Explorer shows the `device.state.changes` custom metric incrementing after a `PATCH` call.

**Decisions**
- Mirrors ADR-009/ADR-010 (Task 2): Azure Functions HTTP triggers (not a separate Web API host) and handler-per-operation (not CQRS) — same rationale applies (small aggregate, no read/write divergence, cost).
- `RegisterDevice`, `UpdateDeviceState`, and `RemoveDevice` are all treated as "mutate" per smartnest-plan.md Task 3 wording, allowing both `SmartNest.Owner` and `SmartNest.Technician` — a deliberate divergence from Home Service, where `CreateHome`/`DeleteHome` are Owner-only.
- Device Service uses the dedicated `DeviceServiceSend` (send-only) Service Bus connection string rather than the broader `FunctionsRoot` (Listen+Send+Manage) rule that Home Service currently uses — least privilege, and the infra was already provisioned this way in Task 1.
- Custom metric `device.state.changes` is implemented via the OpenTelemetry Metrics API (`Counter<long>`), not the classic Application Insights `TelemetryClient`, to stay consistent with the OpenTelemetry-based instrumentation already wired into `Program.cs`.
- **Cross-partition lookup by id**: the flat routes (`GET/PATCH/DELETE /devices/{id}`) don't carry the `homeId` partition key, so `IDeviceRepository.GetAsync(deviceId)` uses a cross-partition Cosmos query (`SELECT * FROM c WHERE c.id = @id`) rather than a point-read. `CreateAsync`/`UpdateAsync` still use efficient point-writes (the in-memory `Device` object always carries `HomeId`), and `DeleteAsync(deviceId, homeId)` takes the partition key explicitly (resolved from the already-loaded `Device.HomeId` by the calling handler) for an efficient point-delete. This trades one extra cross-partition read (only on Get/Update/Remove) for keeping the public API surface exactly as specified in smartnest-plan.md.
- `StateValue`'s typed representation (bool/numeric+unit/text) is modeled as an explicit discriminated shape (`Type` enum + one populated field) rather than `object`/`dynamic`, keeping the `DeviceStateChanged` payload schema stable for the Audit Service consumer; wire-format DTOs (`StateValueRequest`/`StateValueResponse`) mirror this shape flatly (matching the convention already used for `TemperatureUnit` in `HomeDtos`).

**Further Considerations**
- The `homeId`-claim enforcement gap (no claim source until Task 4) means Technician/Guest RBAC cannot be functionally verified end-to-end against real Entra ID tokens yet — only against hand-crafted test JWTs. This mirrors the `CreateHome` homeId nuance flagged in Task 2's plan and is explicitly deferred to Task 4.
- `RegisterDevice` does not verify the target home actually exists (would require Device Service to read the `homes` Cosmos container cross-context, or call Home Service's `GET /homes/{id}` via APIM) — out of scope for this task per the same reasoning as Task 2 (avoid service-to-service coupling before Task 4 lands); a non-existent `homeId` will simply result in an orphaned device until Task 4/7 reconcile it.
- `RemoveDevice`/`UpdateDeviceState`'s cross-partition `GetAsync` query is O(1) RU-cheap for point lookups by `id` in practice (Cosmos still uses the index efficiently), but at higher scale a secondary lookup (e.g. a `deviceId → homeId` mapping document, or requiring the `homeId` as a query-string/route parameter on these endpoints) could be considered if this becomes a hot path — not necessary for the current POC scale.
