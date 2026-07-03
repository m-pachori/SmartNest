# SmartNest

> Cloud-native smart home management backend built entirely on Microsoft Azure Free Tier services.

## Overview

SmartNest is a serverless microservices backend for smart home management. It follows **Domain-Driven Design** principles and uses **Event Sourcing** for full state auditability. All compute runs on **Azure Functions**, with messaging via **Azure Service Bus**, document storage in **Azure Cosmos DB Serverless**, and centralised identity through **Azure AD (Entra ID)**.

**In-scope:** Backend only. No UI. All services deployed on Azure Free Tier.  
**Out-of-scope:** Mobile apps, web frontends, third-party integrations.

## Key Services

| Azure Service | Purpose |
|---|---|
| Azure Functions | All microservice compute (HTTP, timer, message, blob triggers) |
| Azure Cosmos DB | Document store — one logical database per bounded context |
| Azure Service Bus | Topic/subscription pub-sub + dedicated queues |
| Azure Blob Storage | Snapshots, documents, media uploads |
| Azure AD (Entra ID) | Centralised identity, JWT issuance, App Roles |
| Azure API Management | Single ingress gateway, JWT validation policy |
| Azure Application Insights | Distributed tracing, structured logging, custom metrics |

## Bounded Contexts

| Context | Aggregates | Domain Events |
|---|---|---|
| Home | Home, Room | HomeCreated, RoomAdded, HomeDeleted |
| Device | Device, DeviceState | DeviceRegistered, DeviceStateChanged, DeviceRemoved |
| Identity/Access | User, RoleAssignment | UserInvited, RoleAssigned, UserDeactivated |
| Automation | Rule, Trigger | RuleCreated, RuleTriggered, AutomationExecuted |
| Alert | Alert, Channel | AlertRaised, AlertDelivered, AlertAcknowledged |
| Audit | AuditEntry | *(append-only — consumes all domain events)* |
| Summary | DailySummary | SummaryGenerated |
| Media | Snapshot, Document | SnapshotUploaded, DocumentProcessed |

## Architecture

The full architecture plan, implementation tasks, ADRs, and deployment checklist are documented in [`smartnest-plan.md`](smartnest-plan.md).
