# ADR 0006: Local Offline Runtime and Future Sync

## Status

Accepted

## Context

DumpTether should run as:

- a hosted website backed by PostgreSQL
- a local desktop app backed by SQLite

The local app must not become a second implementation of task rules, template validation, sharing rules, archive rules, or note/timeline behavior.

## Decision

DumpTether will use one product contract and two runtime shapes.

Hosted runtime:

```text
React UI
  -> hosted ASP.NET Core API
  -> PostgreSQL
```

Local runtime:

```text
Tauri shell
  -> bundled local ASP.NET Core sidecar API
  -> SQLite database
  -> same React UI
```

The first implementation step is not the Tauri shell. The first step is making `DumpTether.Api` run against SQLite with `Database:Provider=Sqlite`.

The desktop project should later live in `apps/desktop`. It should package the built React UI and start the local .NET API sidecar on localhost.

## Source Code Boundary

This is the intended ownership:

- `DumpTether.Domain`: task/domain invariants
- `DumpTether.App`: use cases and validation
- `DumpTether.Api`: HTTP API used by web and desktop
- `DumpTether.Data`: EF Core repositories and provider selection
- `apps/web`: shared React UI
- `apps/desktop`: future Tauri shell only
- `DumpTether.Sync`: future sync engine

The desktop shell must not reimplement business logic in Rust or TypeScript.

## SQLite

SQLite is the primary local state database.

Default local database paths:

```text
Windows: %APPDATA%\DumpTether\dumptether.db
Linux:   ~/.local/share/DumpTether/dumptether.db
```

JSON remains appropriate for lightweight app preferences and export/import metadata, not the live task database.

## Packaging

The expected desktop packaging path is:

- Tauri development shell first
- Windows NSIS `.exe` installer for ordinary installs
- Windows `.msi` installer when enterprise/MSI deployment is needed
- Linux AppImage/deb/rpm depending on distribution needs
- code signing after the build pipeline is stable

Do not hand-roll WiX first. Let Tauri own the installer pipeline until there is a concrete deployment need it cannot satisfy. The NSIS `.exe` installer is the MVP path. MSI/WiX is optional and can fail on developer machines if the Windows Installer/WiX validation environment is not healthy.

## Local Identity

The local desktop API still uses the same session/authorization model as the hosted API, but the desktop session is local-only.

On first desktop launch, the local API creates or reuses a local SQLite `AppUser` and an owner workspace. This is not the cloud account. It is the identity the local API uses to keep authorization, workspace ownership and task scoping consistent while offline.

The session token can expire or be replaced. The durable local identity is the SQLite `AppUser`, plus future sync metadata such as `DeviceId`.

## Login and Sync Mapping

Login/sync is future work.

A local user can use DumpTether without cloud login. Local boards and tasks are born in SQLite and should show as local-only/not-synced when sync UI exists.

When the user logs in to the hosted DumpTether service, the app should not silently merge everything. It should use a OneDrive-like mapping flow:

1. keep local data available
2. connect to the hosted API
3. identify the local device
4. let the user mark a local board/task set for sync, or keep it local-only
5. create or choose the matching hosted board/task container
6. store a local mapping such as `LocalWorkspaceId -> RemoteWorkspaceId`
7. sync local-owned boards/tasks using stable IDs and checkpoints
8. fetch shared boards/tasks available to that cloud user
9. show clear status: local-only, not synced, offline, connected, syncing, sync error

The local session is not the sync relationship. Cloud login creates the cloud authority; sync maps local SQLite records to hosted PostgreSQL records deliberately.

Shared tasks and shared boards are server-side concepts. They are visible in the local app only after login and successful sync.

If the user loses the SQLite database, any local-only data and local sync mappings are lost. Already-synced cloud data can be downloaded again after login because the cloud keeps remote IDs and membership. Unsynced local-only tasks cannot be recovered without a backup/export. After reinstall, the app should rebuild local mappings from downloaded cloud records or ask the user to link local boards again if local data still exists.

The sync foundation uses explicit metadata:

```text
SyncRoot
  LocalWorkspaceId
  RemoteWorkspaceId
  CloudUserId
  DeviceId
  Status
  LastSyncedAt

SyncMapping
  SyncRootId
  EntityType
  LocalId
  RemoteId
  LastRemoteVersion
  Status
  LastSyncedAt
```

`SyncRoot` represents a board-level sync relationship. `SyncMapping` represents individual local-to-remote entity links. These records prevent duplicate uploads because retries can resolve "this local thing already became that remote thing" before creating new remote rows.

Future AD, Google, Microsoft and other identity providers should attach to the cloud identity layer. They should not replace the local SQLite identity. A local desktop user can later link to a cloud account from any supported provider, then sync mappings decide what data moves.

## Sync Risks To Design Around

The risky parts are not just "upload local rows":

- A local-only user and a logged-in cloud user need a clear identity transition. The app must not silently merge two people just because they used the same machine.
- Shared tasks/workspaces should never be cached as permanently local ownership. They should be visible only while the user is logged in and the server confirms access.
- Revoked sharing must remove local visibility on next sync. Local caches need access checks, not only data freshness checks.
- The sidecar API port should eventually be allocated safely instead of assuming a fixed port forever.
- Hard deletes need tombstones or delayed cleanup so another device can learn that a record was deleted.
- Sync logs must avoid storing raw tokens, passwords, or full secret-bearing request headers.
- Conflict UI should be quiet by default, but obvious when the same field was edited in two places.

## Sync Metadata

Syncable records should move toward:

- stable IDs
- `CreatedAt`
- `UpdatedAt`
- `DeletedAt`
- `Version`
- `DeviceId`
- `LastSyncedAt` or equivalent sync checkpoint metadata

Permanent deletion should be delayed until tombstones have synced. Archive and soft-delete are safer than immediate hard delete.

## Conflict Handling

DumpTether should avoid noisy conflicts, but not silently destroy user work.

Recommended policy:

- Append-only timeline/note entries merge by stable ID and timestamp.
- Independent task fields merge field-by-field.
- If local and server changed different fields, both changes are kept.
- If local and server changed the same scalar field since the last sync base, keep both values and mark the task as conflicted.
- For text body edits on the same note/field, preserve both versions and ask the user to choose or merge.
- For archive/delete versus edit, keep the archive/delete state but preserve the edit as conflict evidence.

Last-write-wins is acceptable only when one side changed a field. When both sides changed the same field, create a visible conflict marker instead of guessing.

## Consequences

- The first offline milestone can be a local API with SQLite before a packaged desktop shell exists.
- The web UI remains reusable.
- Future sync can be implemented as a C# service instead of UI glue.
- Conflict handling stays task-centered and evidence-oriented.
