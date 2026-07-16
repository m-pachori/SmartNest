# Manual Testing Plan — `SmartNest.PlatformService` (localhost:7244)

> ⚠️ `local.settings.json` for this project contains **live secrets** (real Cosmos DB
> primary key, a Service Bus **root** `RootManageSharedAccessKey` connection string, and
> the Device Service function key). It's already covered by `.gitignore` — never commit
> or share it. Because it points at the **real dev Cosmos DB / Service Bus namespace**,
> any writes made while testing land in real dev data — clean up test documents afterward
> (see [Cleanup](#9-cleanup)).

## 1. Prerequisites

- Azure Functions Core Tools v4 (`func --version`) and .NET 8 SDK installed.
- **Azurite** (storage emulator) — `AzureWebJobsStorage` is `UseDevelopmentStorage=true`,
  so blob upload/trigger tests run against Azurite, not the real storage account:
  `npm install -g azurite` then run `azurite --silent --location .azurite` in its own
  terminal (keep it running).
- Your machine can reach the real Cosmos DB account and Service Bus namespace (no
  firewall block).
- A REST client (curl/Postman/Thunder Client) and, ideally, **Azure Storage Explorer**
  (to inspect Azurite blobs) plus the **Cosmos DB Data Explorer** and **Service Bus
  Explorer** in the Azure Portal (to seed/inspect data and send raw messages).

## 2. Start the function host

```powershell
cd services/platform-service/SmartNest.PlatformService
func start
```

Confirm the console lists all 17 functions with no binding errors (a Service
Bus/Cosmos connection failure shows up here immediately).

> Note: `AuthorizationLevel.Function` is **not enforced** by Core Tools when running
> locally — you don't need a `?code=` query param on any URL below.

## 3. Seed Cosmos DB test data

Ownership checks read the **`homes`**/**`devices`** containers directly (not the JWT),
so insert via Data Explorer:

- `homes` container: `{ "id": "home-1", "homeId": "home-1", "ownerId": "test-owner-1", ... }`
- `devices` container: `{ "id": "device-1", "homeId": "home-1", ... }` (needed for
  Media's device→home lookup and Automation's ChangeDeviceState action)

## 4. Create a test JWT

`CurrentUser.FromAuthorizationHeader` only **decodes** the JWT (APIM normally validates
the signature) — signature doesn't matter locally. Use [jwt.io](https://jwt.io)
Debugger with any signing key and this payload:

```json
{ "oid": "test-owner-1", "roles": ["SmartNest.Owner"] }
```

Send the resulting token as `Authorization: Bearer <token>` on every request except
`UploadMedia` (no auth check).

> **Enum gotcha**: `ConditionOperator`/`RuleActionType` serialize as **numbers** (no
> `JsonStringEnumConverter` registered) — use `GreaterThan=0, LessThan=1, Equals=2` and
> `ChangeDeviceState=0, RaiseAlert=1`.

## 5. HTTP endpoints

| Function | Request | Notes |
|---|---|---|
| **CreateRule** | `POST /api/homes/home-1/rules`<br>`{"deviceId":null,"name":"Hot Alert","condition":{"field":"temperature","operator":0,"value":"30"},"action":{"type":1,"alertSeverity":"Warning","alertMessage":"Too hot"}}` | 201 + `RuleResponse`. Save `ruleId`. |
| **GetRule** | `GET /api/homes/home-1/rules/{ruleId}` | 200. No role required, ownership only. |
| **UpdateRule** | `PUT /api/homes/home-1/rules/{ruleId}`<br>`{"name":"Hot Alert v2","condition":{...},"action":{...},"enabled":true}` | 200, Owner role required. |
| **DeleteRule** | `DELETE /api/homes/home-1/rules/{ruleId}` | 204. Re-run `GetRule` → expect 404. |
| **GetAlerts** | `GET /api/alerts?homeId=home-1` | 200, array (empty until an alert exists — see §6). |
| **AcknowledgeAlert** | `POST /api/homes/home-1/alerts/{alertId}/acknowledge` | Needs an existing alert id from §6. 200 with `acknowledged:true`. |
| **GetAuditLog** | `GET /api/audit/device-1?from=0` | Owner role only. Empty until §6 populates the audit log. |
| **ReplayEvents** | `POST /api/audit/replay/device-1` | Same data as GetAuditLog, Owner-only. |
| **GetDailySummary** | `GET /api/summaries/home-1?date=2026-07-15` | 404 until §7 generates a summary. |
| **UploadMedia** | `POST /api/media?deviceId=device-1`<br>Body: raw binary file, header `Content-Type: image/jpeg` | 202 + `blobName`. No Authorization header needed. |

Negative-path checks worth doing per role/ownership-guarded endpoint: wrong `oid` (not
the home's owner) → 403; missing `SmartNest.Owner` role where required → 403; unknown id
→ 404.

## 6. Triggering the Service Bus functions (EvaluateRules / DispatchAlerts / AppendDeviceEvents/HomeEvents/UserEvents)

These listen on the **real** namespace's existing subscriptions. Easiest path: open the
Service Bus namespace in the Azure Portal → `device-events` topic → **Service Bus
Explorer** → "Send messages" → send this body (fans out to `automation`, `alert`, and
`audit` subscriptions in one send, exercising `EvaluateRules`, `DispatchAlerts`, and
`AppendDeviceEvents` together):

```json
{
  "eventId": "11111111-1111-1111-1111-111111111111",
  "eventType": "DeviceStateChanged",
  "aggregateId": "device-1",
  "aggregateType": "Device",
  "occurredAt": "2026-07-16T00:00:00Z",
  "actorId": "test-owner-1",
  "homeId": "home-1",
  "correlationId": "22222222-2222-2222-2222-222222222222",
  "payload": { "deviceId": "device-1", "homeId": "home-1", "property": "temperature", "oldValue": "20", "newValue": "35", "unit": "celsius" }
}
```

Watch the `func start` console for all three functions executing. Then re-run
`GetAlerts`/`GetAuditLog` — an alert and audit entries should now appear. Repeat with
`eventType` like `"HomeCreated"`/`"UserInvited"` sent to `home-events`/`user-events`
topics to exercise `AppendHomeEvents`/`AppendUserEvents`.

Alternative: exercise it end-to-end by calling the real deployed Device Service's
`PATCH /devices/{id}/state` (its base URL is already in your settings), which publishes
a genuine `DeviceStateChanged` message.

## 7. GenerateDailySummary (timer trigger)

Don't wait for midnight — invoke it directly via the admin API:

```powershell
curl -X POST http://localhost:7244/admin/functions/GenerateDailySummary -H "Content-Type: application/json" -d "{}"
```

Make sure §6's audit entries have an `occurredAt` within the last 24h first, then check
the `summaries` Cosmos container and re-run `GetDailySummary`.

## 8. ProcessMedia (blob trigger)

Fires automatically a few seconds after `UploadMedia` writes to Azurite's
`media-uploads` container. Confirm via console logs, then check in Azure Storage
Explorer (connect to Azurite) that the blob moved to `processed-media` and was removed
from `media-uploads`, and that a `media-metadata` Cosmos document was created.

## 9. Cleanup

Delete the test `rules`/`alerts`/`audit-log`/`summaries`/`media-metadata`/`homes`/`devices`
documents you created, since they live in the real dev database.
