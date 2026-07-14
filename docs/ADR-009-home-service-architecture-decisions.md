# ADR-009 & ADR-010 — Home Service (Task 2) Architecture Decisions

Reference notes for Task 2 (Home Service) implementation. Captures the reasoning behind
two decisions made during planning: (1) Azure Functions vs. a traditional Web API project
for the HTTP endpoints, and (2) handler-per-operation vs. full CQRS for the Home bounded
context. Kept separate from `smartnest-plan.md`'s ADR-001..008 for quick reference during
implementation review.

---

## ADR-009: Azure Functions (HTTP Trigger) over ASP.NET Core Web API for Home Service

**Decision:** Home Service's HTTP endpoints (`POST /homes`, `GET /homes/{id}`, `PUT /homes/{id}`,
`DELETE /homes/{id}`, `POST /homes/{id}/rooms`, `DELETE /homes/{id}/rooms/{roomId}`) are
implemented as Azure Functions HTTP triggers (.NET 8 Isolated), not as a separate ASP.NET
Core Web API hosted on App Service. This is consistent with `smartnest-plan.md` ADR-001
(Azure Functions over Azure App Service for all compute).

**Reasoning:**

1. **An HTTP-triggered Function *is* the API layer.** `[Function("CreateHome")]` +
   `[HttpTrigger]` is a full HTTP endpoint on its own — a second Web API host would just
   duplicate what the Function App already provides.
2. **Cost.** Functions Consumption plan is free up to 1M executions + 400,000 GB-s/month
   and scales to zero when idle. An App Service Web API either sits on the very limited,
   subscription-wide F1 free tier or starts costing money immediately on B1+ (~$13/month,
   billed 24/7 regardless of traffic). For a low-traffic POC, Functions Consumption is
   effectively free; App Service is not.
3. **Performance is comparable once warm.** Both run the same .NET runtime and the same
   Cosmos DB / Service Bus SDKs. The only meaningful difference is cold start (Functions
   Consumption: ~1–3s after idle) vs. an always-on Web API (no cold start, but "always on"
   is exactly what costs money). For manual/demo-level POC testing, occasional cold starts
   are a non-issue.
4. **Structural consistency across the platform.** Every other service in the plan
   (Automation, Alert, Audit, Summary, Media) mixes HTTP triggers with Service Bus/Timer/Blob
   triggers in the same Function App. Keeping Home Service as Functions keeps all services
   on one deployable/binding model instead of introducing a second hosting pattern
   (App Service) that only Home Service would use.
5. **APIM is already the API-facing layer regardless of hosting choice.** Azure API
   Management is the single ingress, applies JWT validation, and fronts the backend. The
   Function App is just compute; APIM is what plays the traditional "API project" role from
   the client's perspective either way.

**When this would not apply:** if the platform needed guaranteed low-latency
(<100ms, no cold starts) under sustained production load, App Service (or Functions Premium
plan) would be justified. Not applicable to a backend POC.

---

## ADR-010: Handler-per-Operation over Full CQRS for Home Service

**Decision:** Home Service uses a simple handler-per-operation design (`CreateHomeHandler`,
`GetHomeHandler`, `UpdateHomeHandler`, `DeleteHomeHandler`, `AddRoomHandler`,
`RemoveRoomHandler`) called directly from thin Function triggers. It does **not** implement
full CQRS (no Command/Query DTOs, no mediator/dispatcher, no separate read model).

**Reasoning:**

1. **Small, simple aggregate.** `Home`/`Room` has 6 straightforward CRUD operations with no
   divergent read/write scaling needs or complex query shapes. CQRS earns its complexity
   when read and write models genuinely diverge — that isn't the case here.
2. **No event-sourced read-side projection exists (or is planned) for Home Service.**
   The platform does have event sourcing via the Audit Service (Task 7), but Home Service's
   `GetHome` reads current aggregate state directly from the same `homes` Cosmos container
   used for writes (via the same `IHomeRepository`). Real CQRS value typically comes from a
   denormalized read model fed by a projection — retrofitting that for Home Service alone,
   while no other service does the same, would create inconsistent architecture across the
   platform for no shared benefit.
3. **Avoids speculative generality.** A mediator library, Command/Query message types, and
   (if taken further) a second data store/projection are all extra code to write, test, and
   maintain that the current CRUD surface doesn't justify — consistent with the project's
   "unit-tests-only, keep Task 2 lean" scope decisions.
4. **Already gets the useful part of CQRS "for free."** Splitting each operation into its
   own handler class gives clean separation of concerns and testability (mockable
   `IHomeRepository`/`IEventPublisher` per handler) without the message-dispatch machinery.

**When this should be revisited:**
- If Home/Room queries need denormalized shapes (e.g., a home summary combining device and
  room counts) that are expensive to compute live — a read-model/projection built from the
  Audit Service's event stream would then pay for itself.
- If a mediator pattern is adopted as a platform-wide convention starting with a later
  service (Task 3+), it should be applied consistently rather than retrofitted onto Home
  Service alone.

---

*See also: `smartnest-plan.md` (ADR-001..008), `plan-homeService.prompt.md` (Task 2
implementation plan).*
