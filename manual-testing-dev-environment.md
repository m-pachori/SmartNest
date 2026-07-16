# Manual Testing Plan — SmartNest DEV Environment (all services + Service Bus + end-to-end workflow)

## 0. What's deployed (resource group `smartnest-rg-dev`)

| Function App | Hostname |
|---|---|
| Home Service | `smartnest-home-svc-dev.azurewebsites.net` |
| Device Service | `smartnest-device-svc-dev.azurewebsites.net` |
| Identity Service | `smartnest-identity-svc-dev.azurewebsites.net` |
| Platform Service (Automation/Alert/Audit/Summary/Media) | `smartnest-platform-svc-dev.azurewebsites.net` |

Shared backing resources: Cosmos DB `smartnest-cosmos` (db `smartnest-db`), Service Bus `smartnest-bus`,
Storage `smartneststorage`, Key Vault `smartnest-kv-dev`, App Insights `smartnest-insights`. **APIM is
not deployed** (`deployApim=false`), so every call goes directly to the Function App with a function
key — no gateway JWT signature validation happens anywhere in DEV either.

## 1. Prerequisites

- Azure CLI logged in (`az login`) with the `smartNest` subscription selected.
- A REST client (curl/Postman/Thunder Client).
- Retrieve each app's function key on demand (don't hardcode/commit these):
  ```powershell
  az functionapp keys list -g smartnest-rg-dev -n smartnest-home-svc-dev --query functionKeys.default -o tsv
  az functionapp keys list -g smartnest-rg-dev -n smartnest-device-svc-dev --query functionKeys.default -o tsv
  az functionapp keys list -g smartnest-rg-dev -n smartnest-identity-svc-dev --query functionKeys.default -o tsv
  az functionapp keys list -g smartnest-rg-dev -n smartnest-platform-svc-dev --query functionKeys.default -o tsv
  ```
  Append `?code={key}` to every URL below.
- Azure Portal access to: Application Insights `smartnest-insights` (Live Metrics / Transaction Search)
  to watch Service Bus trigger executions, the Service Bus namespace (`smartnest-bus`) Explorer (for
  isolated negative tests), and Cosmos DB Data Explorer (to inspect written documents).

## 2. Create a test JWT

Same trick as local testing — `CurrentUser.FromAuthorizationHeader` only decodes the JWT, it never
validates the signature (APIM would normally do that, and it's disabled). Use
[jwt.io](https://jwt.io) with any signing key and payload:

```json
{ "oid": "test-owner-1", "roles": ["SmartNest.Owner"] }
```

Send as `Authorization: Bearer <token>` on every call. Create a second token with
`"roles": ["SmartNest.Guest"]` (same `oid`) for negative-path checks.

---

## 3. Per-service endpoint reference

### Home Service

| Function | Request | Role required |
|---|---|---|
| CreateHome | `POST /api/homes`<br>`{"name":"Test Home","street":"1 Main St","city":"Springfield","state":"IL","postalCode":"62701","country":"USA","timeZone":"America/Chicago","temperatureUnit":0}` | Owner |
| GetHome | `GET /api/homes/{homeId}` | Owner/Technician/Guest + ownership |
| UpdateHome | `PUT /api/homes/{homeId}` (same body shape) | Owner + ownership |
| AddRoom | `POST /api/homes/{homeId}/rooms`<br>`{"name":"Living Room","roomType":"living"}` | Owner + ownership |
| RemoveRoom | `DELETE /api/homes/{homeId}/rooms/{roomId}` | Owner + ownership |
| DeleteHome | `DELETE /api/homes/{homeId}` | Owner + ownership |

`temperatureUnit`: `0`=Celsius, `1`=Fahrenheit (no string-enum converter registered).

### Device Service

| Function | Request | Role required |
|---|---|---|
| RegisterDevice | `POST /api/homes/{homeId}/devices`<br>`{"name":"Thermostat","deviceType":"thermostat","manufacturer":"Acme","model":"T100"}` | Owner/Technician + ownership |
| GetDevice | `GET /api/devices/{deviceId}` | Owner/Technician/Guest + ownership |
| UpdateDeviceState | `PATCH /api/devices/{deviceId}/state`<br>`{"property":"temperature","value":{"type":1,"numericValue":35,"unit":"celsius"}}` | Owner/Technician + ownership |
| RemoveDevice | `DELETE /api/devices/{deviceId}` | Owner/Technician + ownership |

`value.type`: `0`=Boolean, `1`=Numeric, `2`=Text.

### Identity Service

| Function | Request | Role required |
|---|---|---|
| InviteUser | `POST /api/homes/{homeId}/users/invite`<br>`{"targetUserId":"test-guest-1","role":"SmartNest.Guest"}` | Owner + ownership |
| UpdateUserRole | `PUT /api/users/{membershipId}/role`<br>`{"role":"SmartNest.Technician"}` | Owner + ownership |
| RemoveUser | `DELETE /api/homes/{homeId}/users/{userId}` | Owner + ownership |

### Platform Service (Tasks 5-9)

| Function | Request | Role required |
|---|---|---|
| CreateRule | `POST /api/homes/{homeId}/rules`<br>`{"deviceId":"{deviceId}","name":"Hot Alert","condition":{"field":"temperature","operator":0,"value":"30"},"action":{"type":1,"alertSeverity":"Warning","alertMessage":"Too hot"}}` | Owner + ownership |
| GetRule | `GET /api/homes/{homeId}/rules/{ruleId}` | ownership only |
| UpdateRule | `PUT /api/homes/{homeId}/rules/{ruleId}` | Owner + ownership |
| DeleteRule | `DELETE /api/homes/{homeId}/rules/{ruleId}` | Owner + ownership |
| GetAlerts | `GET /api/alerts?homeId={homeId}` | ownership only |
| AcknowledgeAlert | `POST /api/homes/{homeId}/alerts/{alertId}/acknowledge` | Owner/Technician + ownership |
| GetAuditLog | `GET /api/audit/{aggregateId}?from=0` | Owner only |
| ReplayEvents | `POST /api/audit/replay/{aggregateId}` | Owner only |
| GetDailySummary | `GET /api/summaries/{homeId}?date=yyyy-MM-dd` | ownership only |
| UploadMedia | `POST /api/media?deviceId={deviceId}`, raw binary body, `Content-Type: image/jpeg` | none (write-only) |

`condition.operator`: `0`=GreaterThan,`1`=LessThan,`2`=Equals. `action.type`: `0`=ChangeDeviceState,`1`=RaiseAlert.

---

## 4. Full end-to-end workflow (drives every service + every Service Bus subscription naturally)

Do these **in order**, using the **same** `oid` (`test-owner-1`) throughout so ownership checks line
up automatically (no manual Cosmos seeding needed — everything is created through the real APIs):

1. **CreateHome** → save `homeId`.
2. **RegisterDevice** under that home → save `deviceId`. *(publishes `DeviceRegistered` →
   `device-events` → `audit` sub → `AppendDeviceEvents`)*
3. **InviteUser** (`test-guest-1`, role `SmartNest.Guest`) → save `membershipId`. *(publishes
   `UserInvited` → `user-events` → `audit` sub → `AppendUserEvents`)*
4. **UpdateUserRole** on that membership to `SmartNest.Technician`. *(`RoleAssigned` →
   `AppendUserEvents` again)*
5. **CreateRule**: `deviceId` scoped, condition `temperature > 30`, action `RaiseAlert` (Warning).
6. **UpdateDeviceState** on that device: `temperature = 35`. This single call fans out to all three
   `device-events` subscriptions:
   - `automation` → `EvaluateRules` matches the rule → raises an alert **in-process** and publishes
     `AutomationExecuted`.
   - `alert` → `DispatchAlerts`'s own built-in `temperature > 30` threshold also matches → raises a
     **second, independent** alert (expected — verify you see 2 alerts, not a bug).
   - `audit` → `AppendDeviceEvents` logs `DeviceStateChanged`, and subsequently the
     `AutomationExecuted`/`AlertRaised`/`AlertDelivered` events (all published back onto
     `device-events`) get logged too.
7. **GetAlerts** `?homeId={homeId}` → expect 2 alerts. Save one `alertId`.
8. **AcknowledgeAlert** on that alert → verify `acknowledged:true`.
9. **GetAuditLog** `/api/audit/{deviceId}?from=0` → expect an ordered stream: `DeviceStateChanged`,
   `AutomationExecuted`, `AlertRaised` ×2, `AlertDelivered` ×2 — with strictly increasing
   `sequenceNumber` (validates the transactional-batch idempotency fix).
10. **ReplayEvents** `/api/audit/replay/{deviceId}` (Owner-only) → same data as step 9.
11. **UploadMedia** `?deviceId={deviceId}` with a small JPEG → 202 + `blobName`. Wait a few seconds,
    then confirm in the Storage account that the blob moved from `media-uploads` to
    `processed-media`, and check the `media-metadata` Cosmos container for the new document.
    *(publishes `DocumentProcessed` → `device-events` → `audit` sub → another `AppendDeviceEvents`
    entry, visible via GetAuditLog again)*.
12. **GenerateDailySummary** — don't wait for midnight; invoke directly:
    ```powershell
    az functionapp function keys list -g smartnest-rg-dev -n smartnest-platform-svc-dev --function-name GenerateDailySummary --query default -o tsv
    curl -X POST "https://smartnest-platform-svc-dev.azurewebsites.net/admin/functions/GenerateDailySummary?code={key}" -H "Content-Type: application/json" -d "{}"
    ```
    (Admin endpoints need the **admin/system key**, not the function key — the command above
    retrieves it. If all your audit entries are from today, temporarily test with `occurredAt` in
    the past, or just verify yesterday's window is empty and re-test after adjusting dates.)
13. **GetDailySummary** `/api/summaries/{homeId}?date=...` → verify event counts match what you
    generated.
14. Cleanup: **DeleteRule**, **RemoveUser**, **RemoveDevice**, **DeleteHome**.

## 5. Service Bus-specific verification

- **Application Insights Live Metrics / Transaction Search** (on `smartnest-insights`) — filter by
  operation name (`EvaluateRules`, `DispatchAlerts`, `AppendDeviceEvents`, etc.) to confirm each
  trigger fired exactly once per event and check for exceptions.
- **Service Bus Explorer** (Portal → `smartnest-bus` → `device-events` topic → a subscription →
  "Peek"/"Service Bus Explorer") — inspect delivery count and dead-letter queue for each of
  `automation`/`alert`/`audit` subscriptions; a healthy run shows 0 in the DLQ.
- **Isolated negative test** (exercises the null-payload fix): use Service Bus Explorer to send a
  malformed message directly to `device-events` with `"payload": null` — confirm in Application
  Insights that `EvaluateRules`/`DispatchAlerts` complete without exception and don't dead-letter the
  message.

## 6. Negative-path / security checklist

- Every ownership-guarded endpoint: call with the `SmartNest.Guest` token for a home you don't own →
  expect 403.
- `CreateRule`/`UpdateRule` with `action.alertSeverity: "SuperUrgent"` → expect 400 (validates the
  enum-validation fix).
- `UploadMedia` with `deviceId=foo/bar` → expect 400 (path-sanitization fix).
- `UploadMedia` with `Content-Type: image/jpeg; charset=binary` → expect 202 (MIME-parameter fix —
  should now succeed, not be rejected).
- `UploadMedia` via curl with chunked transfer / no `Content-Length`
  (`curl -X POST ... --data-binary @file.jpg -H "Transfer-Encoding: chunked"`) → expect 411, not a
  silent bypass.
- `UploadMedia` with an 11MB file → expect 413.
- `GetAuditLog`/`ReplayEvents` with the Technician/Guest token → expect 403 (Owner-only).
- Unknown `homeId`/`deviceId`/`ruleId`/`alertId` on any Get/Update/Delete → expect 404.

## 7. Cleanup

Delete any leftover test documents (`homes`, `devices`, `users`/memberships, `rules`, `alerts`,
`audit-log`, `summaries`, `media-metadata`) via Cosmos Data Explorer, and remove the test blob from
`processed-media` if step 11 was run more than once.
