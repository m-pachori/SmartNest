## Plan: Task 2 — Home Service Implementation

Implement the Home bounded context as a .NET 8 Isolated Azure Functions app (`services/home-service/`), backed by a new shared library (`services/shared/`), with Cosmos DB persistence, `home-events` Service Bus publishing, JWT/homeId authorization, unit tests, and the supporting infra (Function App hosting + APIM registration — neither exists yet anywhere in the repo) plus a CI/CD pipeline stage.

**Steps**

**Phase A — Shared library (`services/shared/SmartNest.Shared`)**
1. Scaffold `SmartNest.sln` at `services/` root, add `SmartNest.Shared` class library (net8.0).
2. Add `EventEnvelope` record matching the standard schema in smartnest-plan.md (eventId, eventType, aggregateId, aggregateType, occurredAt, actorId, homeId, correlationId, payload).
3. Add `IEventPublisher` + `ServiceBusEventPublisher` (wraps `ServiceBusClient`, sends to a named topic, sets `CorrelationId` message property).
4. Add JWT claims helpers: `CurrentUser` accessor (parses `roles` claim(s) and `homeId` claim from the incoming bearer token) + `AuthorizationGuard` static helpers: `RequireRole(user, "Owner")`, `RequireHomeIdMatch(user, resourceHomeId)` — throw a typed `ForbiddenException`/`UnauthorizedException` mapped later to 403/401 HTTP responses.
5. Add a thin `CosmosRepositoryBase<T>` (generic get/create/update/delete-by-id-and-partitionKey helpers) to avoid duplicating Cosmos SDK boilerplate in every future service.
6. Add `SmartNest.Shared.Tests` (xUnit) covering envelope serialization + `AuthorizationGuard` logic. *(parallel with Phase B once shared interfaces are stubbed)*

**Phase B — Home Service domain & persistence** (*depends on Phase A interfaces existing, can start in parallel by coding against stubs*)
7. Scaffold `services/home-service/SmartNest.HomeService` (Azure Functions Worker, isolated model, HTTP triggers) referencing `SmartNest.Shared`.
8. Add `host.json`, `local.settings.json.sample` (real `local.settings.json` gitignored).
9. Domain model: `Home` aggregate (Id/HomeId, OwnerId, Name, `Address` VO, `HomeSettings` VO, `List<Room>`, timestamps) with invariant methods (`Create`, `AddRoom`, `RemoveRoom`) that raise domain events; `Room` entity; `Address`/`HomeSettings` value objects.
10. `IHomeRepository` + `CosmosHomeRepository` (container `homes`, partition key `/homeId`, built on `CosmosRepositoryBase<T>`).
11. `HomeEventPublisher` (wraps shared `IEventPublisher`, targets `home-events` topic) builds `HomeCreated`, `RoomAdded`, `HomeDeleted` payloads per the standard envelope.

**Phase C — HTTP Functions + authorization** (*depends on Phase B*)
12. Implement thin Function triggers + testable handler classes (e.g. `CreateHomeHandler`, `GetHomeHandler`, `UpdateHomeHandler`, `DeleteHomeHandler`, `AddRoomHandler`, `RemoveRoomHandler`) so business logic is unit-testable without faking `FunctionContext`/`HttpRequestData`.
13. Wire endpoints: `POST /homes`, `GET /homes/{id}`, `PUT /homes/{id}`, `DELETE /homes/{id}`, `POST /homes/{id}/rooms`, `DELETE /homes/{id}/rooms/{roomId}`.
14. Apply `AuthorizationGuard`: Owner role required to mutate; Guest/Technician read-only; `homeId` claim must match the resource's homeId for all operations except `CreateHome` (see Decisions below for the create-time homeId nuance).
15. `Program.cs`: DI registration (CosmosClient, ServiceBusClient, repositories, publishers, Azure Monitor OpenTelemetry / Application Insights for Functions Worker).

**Phase D — Unit tests** (*depends on Phase C*)
16. `SmartNest.HomeService.Tests`: handler tests (mock `IHomeRepository`, `IEventPublisher`) covering success paths, Owner-only enforcement, homeId-mismatch 403, not-found 404.
17. Domain tests: `Home` aggregate invariants (duplicate room name rejected, event raised on each mutation).

**Phase E — Infra additions** (*can run in parallel with Phases A–D*)
18. Add generic `infra/modules/function-app.bicep`: Consumption (Y1) plan + Linux Function App (`DOTNET-ISOLATED|8.0`), system-assigned identity, app settings wired via Key Vault reference URIs (Cosmos endpoint + Key Vault secret URIs for keys/connection strings), App Insights connection string; parameterized by `serviceName` so Device/Identity/etc. reuse it later.
19. Instantiate it in infra/main.bicep as `homeFunctionAppModule` (name `smartnest-home-svc-{env}`), after `keyVaultModule`.
20. Add a role assignment in main.bicep granting the new Function App's managed identity **Key Vault Secrets User** on the Key Vault (same role id already used for APIM: `4633458b-17de-408a-b874-0445c86b69e6`) — required because Key Vault has `enableRbacAuthorization: true`.
21. Add generic `infra/modules/apim-api.bicep` (api + operations + backend, parameterized) and instantiate for `/homes` routes pointing at the new Function App, reusing the existing `smartnest-backend` product; function-level auth key stored as a new Key Vault secret (`home-svc-function-key`), referenced as the APIM backend credential.
22. Update `infra/parameters/*.parameters.json` only if new required params are introduced (aim to default everything from `projectName`/`environment`).

**Phase F — CI/CD** (*depends on Phases A–E existing*)
23. Update azure-pipelines.yml: add `services/**` to `trigger.paths.include` / `pr.paths.include`.
24. Add a `BuildAndTestHomeService` job (parallel with `Validate`): `dotnet restore/build/test` against `services/SmartNest.sln`, publish test results.
25. Extend `DeployDev` stage (and Staging/Prod with their existing manual gates) with a step to `dotnet publish` + deploy the Function App (e.g. `AzureFunctionApp@2` task or `az functionapp deployment source config-zip`) after the infra deploy step, gated the same way as existing stages.

**Relevant files**
- `services/SmartNest.sln`, `services/shared/SmartNest.Shared/**`, `services/shared/SmartNest.Shared.Tests/**` — new
- `services/home-service/SmartNest.HomeService/**`, `services/home-service/SmartNest.HomeService.Tests/**` — new
- `infra/modules/function-app.bicep`, `infra/modules/apim-api.bicep` — new, generic/reusable
- infra/main.bicep — add Function App module instance + KV role assignment + APIM API instance
- infra/modules/key-vault.bicep — reference only (RBAC model confirmed; no direct edits needed since role assignment is added in main.bicep)
- azure-pipelines.yml — trigger paths + new build/test/deploy steps

**Verification**
1. `dotnet test services/SmartNest.sln` passes locally (shared + home-service unit tests).
2. `az bicep build --file infra/main.bicep` succeeds; `az deployment group validate` passes against `smartnest-rg-dev` params.
3. Manual: deploy to dev, call `POST /homes` via APIM gateway URL with an Owner-role JWT → `201`; call same endpoint with a Guest-role JWT → `403`.
4. Manual: `PATCH`/`PUT` a home with mismatched `homeId` claim → `403`; matching claim → `200`.
5. Confirm a message lands on the `home-events` topic (`audit` subscription) after `POST /homes`, matching the standard event envelope schema.
6. Application Insights Transaction Search shows the HTTP request trace for a `CreateHome` call.

**Decisions**
- Function/handler split: HTTP triggers stay thin; all logic lives in plain handler classes for easy unit testing (isolated-worker `FunctionContext` is hard to fake reliably).
- `CreateHome` cannot enforce a `homeId`-claim match (no home exists yet) — only the `Owner` role is required; the created `homeId` is returned to the caller. Reconciling how a user's JWT `homeId` claim gets populated after creating a home is an Identity/Access Service (Task 4) concern and is explicitly out of scope here.
- Function App infra (hosting plan + APIM registration) is included in this task since it's explicitly listed under Task 2's expected outcomes, even though Task 1 didn't provision compute. The new bicep modules are written generically so Tasks 3–9 can reuse them without rework.
- Home deletion is a hard delete from the `homes` container; the Audit Service (Task 7) — not yet implemented — is the durable history, so no soft-delete flag is added now.

**Further Considerations**
1. Function auth model for APIM→Function calls: using a function-level key stored in Key Vault (simple, consistent with existing secret-management pattern) vs. switching the Function App to `AuthLevel.Anonymous` and relying solely on network/APIM trust. Recommend the function-key approach (Option A) to match existing security posture; flag if you'd prefer Easy Auth / managed-identity-based backend auth instead.

**Follow-up items raised during review (not yet incorporated)**
- Swagger/OpenAPI support via `Microsoft.Azure.Functions.Worker.Extensions.OpenApi` (decorate Functions with `[OpenApiOperation]` etc., exposes `/api/swagger/ui` + `/api/openapi.json`); could replace manual APIM operation definitions in `apim-api.bicep` with an OpenAPI import, matching Task 10's intent to "import OpenAPI specs from each Function App". Needs a decision on whether to lock down the swagger/openapi endpoints from public APIM exposure.
- Disadvantages/trade-offs discussed: Functions cold starts, Task 1/Task 2 infra scope bundling, unit-tests-only coverage gap (no Cosmos/Service Bus emulator integration tests), CreateHome homeId-claim gap (deferred to Task 4), function-key APIM auth vs Easy Auth, RU cost visibility deferred to Task 11, pipeline duplication risk across future service tasks.
