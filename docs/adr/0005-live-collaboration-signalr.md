# ADR 0005: Live collaboration preparation with SignalR

## Status

Planned.

## Context

DumpTether is moving toward shared boards and shared tasks, but the backend must stay authoritative. The web app should feel current when multiple people edit the same board, while the future desktop/offline app must still be able to run against a local sidecar API without requiring cloud connectivity.

The current MVP should not overbuild full realtime sync, conflict resolution, or offline merge behavior. It should define the shape of live updates so later implementation does not duplicate business rules in React or create a second sync model.

## Decision

DumpTether will use SignalR for future live collaboration events from the ASP.NET Core API. Clients will treat events as invalidation hints and fetch fresh data through the existing API shape.

No SignalR hub or package is required in this step. ASP.NET Core can host SignalR later when the live-update implementation is added.

The initial event names should be:

- `TaskCreated`
- `TaskUpdated`
- `NoteAdded`
- `NoteEdited`
- `NoteDeleted`
- `TaskShared`
- `WorkspaceInviteAccepted`

Future event payloads should stay small and contain identifiers, timestamps, and version hints rather than full business objects:

- `eventId`
- `workspaceId`
- `taskItemId` when relevant
- `timelineEntryId` when relevant
- `actorUserId`
- `occurredAt`
- `updatedAt`
- `version` when available

## Authorization

The backend remains authoritative. SignalR connections should be authenticated when login is required. Hub groups should be scoped by workspace membership and task-share access. Events must not leak tasks, notes, emails, tokens, connection strings, or other secrets to clients without access.

## Desktop and offline impact

The future desktop app can use the same event names locally from the .NET sidecar API. Offline sync remains separate future work. When sync is implemented, it should use stable IDs, `CreatedAt`, `UpdatedAt`, `DeletedAt`, `Version`, `DeviceId`, and append-only timeline entries where possible.

SignalR events should not become the source of truth. If a desktop client is offline, it can miss events and still recover by syncing/refetching later.

## Consequences

This keeps the app responsive for shared boards without turning the frontend into a business-rule engine. It also keeps the hosted web runtime and future desktop runtime aligned around the same C# application services and API contracts.

## Non-goals

- No full live sync implementation yet.
- No offline conflict resolution yet.
- No websocket-only API behavior.
- No AI, MCP, email, calendar, desktop, SQLite, or sync implementation in this step.
