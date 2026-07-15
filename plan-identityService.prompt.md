## Plan: Task 4 — Identity & Access Service Implementation

Implement the Identity/Access bounded context as a .NET 8 Isolated Azure Functions app (`services/identity-service/`), reusing `SmartNest.Shared`, with Cosmos DB persistence (`users` container, partition key `/homeId` — already provisioned in Task 1) and `user-events` Service Bus publishing. This service does **not** replace Entra ID — it is the per-home authorization-scope store that Entra ID structurally cannot provide (see the "Entra ID vs. Task 4" discussion): Entra ID owns authentication and a single, global `roles` App Role per user; this service owns the `(homeId, userId) → role` many-to-many mapping that lets the same person be `Owner` of one home and `Guest` on another.

No Phase A (shared library) is needed — `SmartNest.Shared` is reused as-is. `infra/modules/function-app.bicep` and `infra/modules/apim-api.bicep` are reused with a third instantiation each. `infra/modules/cosmos-db.bicep` already provisions the `users` container (`/homeId`) and `infra/modules/service-bus.bicep` already provisions the `user-events` topic (with an `audit` subscription) from Task 1 — but **unlike** Device Service, Task 1 did **not** pre-provision a least-privilege send-only Service Bus connection string for Identity Service, so that's a new infra addition here (Phase D).

**Steps**

**Phase A — Identity domain & persistence**
1. Scaffold `services/identity-service/SmartNest.IdentityService` (Functions Worker, isolated model, HTTP triggers), referencing `SmartNest.Shared`, added to `services/SmartNest.sln`. Copy `host.json`/`local.settings.json.sample`/`.gitignore`/`Properties/launchSettings.json` pattern from Device Service (use a free local port, e.g. `7146`).
2. Domain model: `HomeMembership` aggregate (`MembershipId`, `HomeId`, `UserId` — the invitee's Entra `oid`, `CurrentAssignment`, `Status`, timestamps) with invariant methods (`Invite`, `AssignRole`, `Deactivate`) raising domain events; `RoleAssignment` value object (`Role`, `AssignedByUserId`, `AssignedAt`). This is smartnest-plan.md's "User aggregate with RoleAssignment value object" — named `HomeMembership` here (not `User`) to avoid confusion with Entra ID's actual user identity, which this service never owns or duplicates.
3. `IIdentityRepository` + `CosmosIdentityRepository` (container `users`, partition key `/homeId`, built on `CosmosRepositoryBase<HomeMembershipDocument>`):
   - `GetAsync(membershipId)` — cross-partition query by id (same rationale as Device Service: the flat `PUT /users/{id}/role` route doesn't carry `homeId`).
   - `GetByHomeAndUserAsync(homeId, userId)` — partition-scoped query (used for invite-dedup and by `RemoveUser`, which *does* have `homeId` in its route).
   - `CreateAsync`, `UpdateAsync`.
4. `IdentityEventPublisher` (wraps shared `IEventPublisher`, targets `user-events` topic) builds `UserInvited`, `RoleAssigned`, `UserDeactivated` payloads per the standard envelope.
5. Reuse the ownership-check pattern from Device Service: add `IHomeOwnershipRepository`/`CosmosHomeOwnershipRepository` (identical shape, own copy — no cross-project reference between Function Apps) reading the `homes` container, so Identity Service can verify the caller managing membership actually owns the target home.

**Phase B — HTTP Functions + authorization**
6. Handlers: `InviteUserHandler`, `UpdateUserRoleHandler`, `RemoveUserHandler` (handler-per-operation, thin Function triggers — ADR-010 pattern).
7. Endpoints: `POST /homes/{homeId}/users/invite`, `PUT /users/{id}/role`, `DELETE /homes/{homeId}/users/{userId}` (exact routes from smartnest-plan.md Task 4).
8. Authorization (all three): `RequireRole(user, "SmartNest.Owner")` — only Owners manage membership (per smartnest-plan.md's Todo: "Validate that caller has Owner role before allowing role mutation") — then verify the caller owns the target home via `IHomeOwnershipRepository.GetOwnerIdAsync` + `AuthorizationGuard.RequireOwnership`.
   - `InviteUser`: check `GetByHomeAndUserAsync` first — reject with a 409-mapped `InvalidOperationException` (or just update-in-place — see Decisions) if an active membership already exists for that `(homeId, userId)`.
   - `UpdateUserRole`: load membership by `id` (cross-partition), then ownership-check against `membership.HomeId`.
   - `RemoveUser`: ownership-check against the route's `homeId` directly, then `GetByHomeAndUserAsync(homeId, userId)` (partition-scoped, no cross-partition query needed here), then `Deactivate()` (soft-delete — see Decisions) + `UpdateAsync`.
9. `Program.cs`: DI registration mirroring Device Service — `Cosmos:UsersContainerName` container for `IIdentityRepository`, a second `homes` container reference for `IHomeOwnershipRepository`, `ServiceBusClient` using the new `IdentityServiceSend` connection string, Application Insights/OpenTelemetry wiring.

**Phase C — Unit tests**
10. `SmartNest.IdentityService.Tests`: domain tests (`HomeMembership` invariants — invite/assign-role/deactivate raise the correct events) and handler tests (mock `IIdentityRepository`, `IHomeOwnershipRepository`, `IEventPublisher`) covering: success paths, non-Owner-caller 403, caller-doesn't-own-home 403, duplicate-invite conflict, membership-not-found 404, home-not-found 404.

**Phase D — Infra additions**
11. **New** (not pre-provisioned, unlike Device Service): add an `IdentityServiceSend` (Send-only) authorization rule to `infra/modules/service-bus.bicep`, alongside the existing `DeviceServiceSend`/`AuditServiceListen`/`FunctionsRoot` rules; add its connection string as a new secure output.
12. `infra/modules/key-vault.bicep`: add `serviceBusIdentitySvcSendConnectionString` param + `servicebus-identitysvc-send-connection-string` secret + `serviceBusIdentitySvcSendSecretUri` output, mirroring the existing Device/Audit secret wiring.
13. `infra/main.bicep`: add `identityServiceFunctionAppName`/`identityServiceHostingPlanName` params; instantiate `identityFunctionAppModule` (`function-app.bicep`, `serviceName: 'identity'`, `serviceBusConnectionStringSecretUri` → the new `IdentityServiceSend` secret, `additionalAppSettings: { 'Cosmos:UsersContainerName': 'users', 'Cosmos:HomesContainerName': 'homes' }`); KV role assignment for its managed identity; `identity-svc-function-key` secret + `identityApiModule` (`apim-api.bicep`) — both **conditional on `deployApim`**, matching the now-established Home/Device pattern; outputs.
14. No changes needed to `infra/parameters/*.parameters.json`.

**Phase E — CI/CD**
15. Extend `azure-pipelines-code.yml`: add `identityServiceProject` variable, `dotnet publish`/`ArchiveFiles@2`/`publish` steps producing an `identity-service-package` artifact, and a `download` + `AzureFunctionApp@2` (`appType: functionApp`, matching the current Windows Consumption plan) step in all three deploy stages, mirroring Home/Device Service exactly.

**Relevant files**
- `services/identity-service/SmartNest.IdentityService/**`, `services/identity-service/SmartNest.IdentityService.Tests/**` — new
- `services/SmartNest.sln` — new projects added
- `infra/modules/service-bus.bicep` — add `IdentityServiceSend` authorization rule + output
- `infra/modules/key-vault.bicep` — add Identity secret param/secret/output
- `infra/main.bicep` — add `identityFunctionAppModule` + KV role assignment + function-key secret (conditional) + `identityApiModule` instance (conditional) + outputs
- `infra/modules/function-app.bicep`, `infra/modules/apim-api.bicep`, `infra/modules/cosmos-db.bicep` — reference only, already generic/provisioned
- `azure-pipelines-code.yml` — extend build/test job + add Identity Service deploy steps

**Verification**
1. `dotnet test services/SmartNest.sln` passes locally (all four services' unit tests).
2. `az bicep build --file infra/main.bicep` succeeds.
3. Manual: invite a Technician to a home as the Owner → `201`; same call as a non-Owner → `403`; duplicate invite → `409`/error.
4. Manual: `PUT /users/{id}/role` changing an existing membership's role as the home's Owner → `200`; as a different (non-owning) Owner → `403`.
5. Manual: `DELETE /homes/{homeId}/users/{userId}` → `204`; confirm the Cosmos document's `Status` becomes `Deactivated` (not physically removed).
6. Confirm `UserInvited`/`RoleAssigned`/`UserDeactivated` messages land on `user-events` (`audit` subscription), matching the standard envelope schema.

**Decisions**
- Named the aggregate `HomeMembership`, not `User` (smartnest-plan.md's literal wording) — Entra ID is the actual system of record for user identity; this service only ever stores a per-home role-assignment record keyed by the Entra `oid`. Avoids implying this service manages credentials/profiles.
- `RemoveUser` is a **soft-delete** (`Deactivate()`, `Status` field), not a hard delete — consistent with the event being named `UserDeactivated` (not `UserRemoved`) and with keeping a full audit trail for the Audit Service (Task 7), unlike Home Service's hard-delete `DeleteHome`.
- `InviteUser` takes the target's Entra **object id directly** (`TargetUserId`), not an email address. A real "invite by email" flow would need Microsoft Graph API integration (resolve email/UPN → `oid`) plus handling for users who don't have an account yet — out of scope for this POC; the caller (an Owner who already knows their household members' object ids, or a thin admin tool) supplies the id directly. Flagged in Further Considerations.
- Mirrors ADR-009/ADR-010 (Tasks 2-3): Azure Functions HTTP triggers, handler-per-operation, no CQRS/mediator.
- Reuses the exact ownership-check architecture from Device Service (own copy of `IHomeOwnershipRepository` reading the `homes` container directly) rather than introducing synchronous service-to-service HTTP calls — keeps every service's authorization check a local Cosmos read, no new coupling/availability dependency between Function Apps.
- Unlike Device Service (which had its Service Bus send-only rule pre-provisioned in Task 1), Identity Service's `IdentityServiceSend` rule doesn't exist yet — this plan adds it in Phase D rather than reusing `FunctionsRoot` (which is broader than needed — Identity Service only ever publishes, never listens, same reasoning as Device Service's least-privilege setup).

**Further Considerations**
- **This does not retroactively fix Home/Device Service's authorization gap.** Both currently check only against `Home.OwnerId` (a single user) — a real Technician/Guest who isn't literally the creator still gets `403` there today. Making Home/Device Service actually consult Task 4's `users` container (checking for an active `HomeMembership` with an appropriate role, not just `OwnerId` equality) is explicitly a **follow-up task**, not part of Task 4 itself — scoping it here would require touching Home and Device Service's `AuthorizationGuard` usage and is a coordinated change better done as its own unit of work once Task 4's data model is stable.
- Email-based invitation (Graph API lookup, invite links for users without an account yet) is deferred — see Decisions.
- No "accept invite" step exists in this plan (matching smartnest-plan.md's Task 4 scope, which lists no such endpoint) — `InviteUser` creates an `Active` membership immediately. If a pending/accept-later flow is wanted later, add a `PendingInvite` status and an `POST /users/{id}/accept` endpoint as a follow-up.
- Consider whether `Owner` should even be an assignable role via this service, given `Home.OwnerId` already designates the primary/original owner — this plan allows it (representing co-owners), but the two concepts (Home Service's single `OwnerId` vs. Identity Service's membership roles) intentionally remain separate data points; reconciling them (e.g., should a `HomeMembership` with role `Owner` also satisfy Home Service's `RequireOwnership`?) is part of the same follow-up task noted above.
