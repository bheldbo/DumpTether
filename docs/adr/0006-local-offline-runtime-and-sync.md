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

The first implementation step was making `DumpTether.Api` run against SQLite with `Database:Provider=Sqlite`. The current local runtime also has a Tauri development shell scaffold and a deliberately small first cloud sync pass.

The desktop project lives in `apps/desktop`. It packages the built React UI and
starts the local .NET API sidecar on a randomly allocated loopback port.

## Source Code Boundary

This is the intended ownership:

- `DumpTether.Domain`: task/domain invariants
- `DumpTether.App`: use cases and validation
- `DumpTether.Api`: HTTP API used by web and desktop
- `DumpTether.Data`: EF Core repositories and provider selection
- `DumpTether.Data.Sqlite`: SQLite provider-specific EF migrations
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

SQLite uses provider-specific EF Core migrations in `DumpTether.Data.Sqlite`. The local API should apply migrations at startup in desktop/local mode, and the database maintenance shell can run the same migrations explicitly. Do not use `EnsureCreated` for real local desktop data upgrades because it bypasses migration history.

## Packaging

The expected desktop packaging path is:

- Tauri development shell first
- Windows NSIS `.exe` installer for ordinary installs
- Windows `.msi` installer when enterprise/MSI deployment is needed
- Linux AppImage/deb/rpm depending on distribution needs
- code signing after the build pipeline is stable

The packaged desktop sidecar is a self-contained .NET executable. Tauri starts
it with an allow-listed command-line profile that selects loopback HTTP, SQLite,
local desktop identity, closed registration, disabled server email/OAuth, and
one trusted webview origin. Each launch uses a random port and 256-bit bootstrap
token injected into the webview before React runs. The API requires that token
on local HTTP requests. The installer does not place an editable
`appsettings.Desktop.json` beside the binaries. PostgreSQL and a system-wide
.NET runtime are not desktop prerequisites. Closing Tauri terminates the
sidecar process.

Do not hand-roll WiX first. Let Tauri own the installer pipeline until there is a concrete deployment need it cannot satisfy. The NSIS `.exe` installer is the MVP path. MSI/WiX is optional and can fail on developer machines if the Windows Installer/WiX validation environment is not healthy.

Linux server deployment and Linux desktop publishing are separate:

- Hosted DumpTether on Linux is the normal Docker Compose production shape: ASP.NET Core API container, PostgreSQL container and reverse proxy.
- Linux desktop bundles should be built on a Linux host or Linux CI runner so Tauri can produce native AppImage/deb/rpm artifacts with the correct system toolchain.
- Cross-building Linux desktop installers from Windows is not a current requirement.

## Local Identity

The local desktop API still uses the same session/authorization model as the hosted API, but the desktop session is local-only.

On first desktop launch, the local API creates or reuses a local SQLite `AppUser` and an owner workspace. This is not the cloud account. It is the identity the local API uses to keep authorization, workspace ownership and task scoping consistent while offline.

The session token can expire or be replaced. The durable local identity is the
SQLite `AppUser`, plus future sync metadata such as `DeviceId`. `DesktopLocal`
sessions cannot be logged out or revoked through the normal session API. If the
browser token is missing, desktop startup creates a replacement session for the
same durable local user instead of stranding the local database.

Local desktop login must be explicitly enabled with `Auth:EnableLocalDesktopLogin` and is only valid when `Database:Provider=Sqlite`. Hosted PostgreSQL deployments must not expose the local desktop login endpoint.

The packaged desktop app should normally talk to its local sidecar API. A configurable remote API base URL would be an online-client mode, not the default offline runtime.

For sync/login, the desktop UI uses the hosted API URL configured before the
client starts. Packaged desktop and web builds take public metadata from a
selected `deploy/targets/*.json` file. `scripts/configure-client.mjs` generates
the React target and synchronizes Tauri, npm and Cargo metadata. The
server/backend URL is deployment configuration, not an in-app user setting.

Deployment targets do not own local sidecar security behavior. The source
`appsettings.Desktop.json` remains a developer-readable profile for direct API
runs, while packaged Tauri launches pin the equivalent critical values as
allow-listed arguments. Secrets never enter deployment targets or the desktop
package.

Hosted authentication remains a remote-server concern. The desktop package does
not expose SMTP, email confirmation, MFA, OAuth, registration, or PostgreSQL
settings. A selected deployment target provides the non-secret hosted API URL;
the local sidecar stores a protected cloud session after successful login.
Desktop startup always restores the local session first. Cloud credentials are
reused for later sync, and the hosted session is verified during sync so a cloud
outage never blocks local SQLite access.

Disconnecting the cloud account is local-first: DumpTether attempts to revoke
the hosted `DesktopCloud` session and always erases the protected token from
SQLite. If the hosted server is unreachable, its hashed session record expires
according to the server retention policy and the desktop no longer retains the
credential.

The local machine owner can always modify locally installed software. DumpTether
does not treat the desktop binary or SQLite database as a cloud authorization
boundary. The hosted API independently validates the cloud session, role,
workspace membership, and requested resource on every cloud operation. Editing
or replacing local files cannot grant additional cloud access.

The packaged Tauri webview has origin `http://tauri.localhost`, while the sidecar
listens on a random `127.0.0.1` port; browsers therefore apply cross-origin
rules even though both are local. Desktop CORS permits only the exact packaged
origin (or the Vite origin in Tauri development). The bootstrap token prevents
unrelated local clients from using the legitimate sidecar. The random port also
removes the predictable pre-bind target. The sidecar remains loopback-only and
does not accept LAN clients.

## Login and Sync Mapping

Login-driven sync is being built incrementally. The first board sync pass proves
that the local API can push and pull task state through the same hosted API
contract. Linked boards now retry in the background and immediately after local
task/note changes. The explicit sync action remains available as a user-controlled
retry and recovery surface.

A local user can use DumpTether without cloud login. Local boards and tasks are born in SQLite and should show as local-only/not-synced when sync UI exists.

When the user logs in to the hosted DumpTether service, the app should not silently merge everything. It should use a OneDrive-like mapping flow:

1. keep local data available
2. connect to the hosted API
3. identify the local device
4. let the user enroll selected tasks or a whole board, or keep them local-only
5. create or choose the matching hosted board/task container
6. store a local mapping such as `LocalWorkspaceId -> RemoteWorkspaceId`
7. sync enrolled local-owned tasks using stable IDs and checkpoints
8. fetch shared boards/tasks available to that cloud user
9. show clear status: local-only, not synced, offline, connected, syncing, sync error

The local session is not the sync relationship. Cloud login creates the cloud authority; sync maps local SQLite records to hosted PostgreSQL records deliberately.

Shared tasks and shared boards are server-side concepts. They are visible in the local app only after login and successful sync.

Future organization/teams features should follow the same rule. Tasks assigned by a manager or another server-side actor are cloud-owned/shared data. The desktop app should fetch them only after cloud login succeeds, show a clear notification when they arrive, and keep local-only data separate from assigned/shared server data.

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
  LastAttemptedAt
  LastSyncedAt
  LastError
```

`SyncRoot` represents a board-level sync relationship. `SyncMapping` represents individual local-to-remote entity links. These records prevent duplicate uploads because retries can resolve "this local thing already became that remote thing" before creating new remote rows.

## Sync Unit And Enrollment

A task is the synchronization unit. A board supplies its owning context and
remote destination.

`SyncRoot` should support:

- `SelectedTasks`: only explicitly enrolled tasks synchronize.
- `WholeBoard`: existing and future tasks in the board synchronize.

Individual enrollment records should make task selection explicit without
duplicating the local/remote ID mappings already owned by `SyncMapping`.

Routine synchronization must not freeze task editing. A sync pass works from a
versioned snapshot. If the user edits the task during that pass, the task
remains pending and another pass synchronizes the newer version. Temporary
locking is reserved for mapping changes and explicit conflict resolution.

Visible states should include:

- local only
- pending
- syncing
- synced
- offline
- failed
- conflict
- access revoked

Shared data may be cached for offline use, but remains associated with the
authenticated cloud profile. It must be hidden on logout and re-authorized when
connectivity returns.

## First Cloud Sync Pass

The first implemented cloud sync pass is intentionally narrow:

- It is available only in the desktop/local runtime.
- It requires a local owner session for the board being synced.
- The user connects a cloud account against the configured hosted API URL.
- The local API stores only a protected cloud session token, never a raw token.
- If no remote board is mapped, the sync service can create one.
- If the cloud account already has exactly the same board name, the first sync
  maps that existing board instead of attempting to create a duplicate.
- Local task header fields can be pushed: title, status, category, color and follow-up date.
- Remote task header fields can be pulled into the local SQLite board.
- Local task templates used by synced tasks can be created or updated in the cloud for that sync root.
- Local task header field values can be pushed to the cloud when the task template is synced first. Field IDs are mapped through template field key/scope because local SQLite and hosted PostgreSQL generate different field IDs.
- Cloud task templates and header field values can be imported into the local SQLite board when pulling new cloud tasks.
- New local tasks can push their first note/timeline entries and entry-level field values when they are first created in the cloud.
- New remote tasks can pull their first note/timeline entries and entry-level field values when they are first created locally.
- `SyncMapping` stores the remote task ID and remote version after successful sync.
- If both local and remote changed the same task header since the previous sync checkpoint, the mapping is marked `Conflict` and both records are left intact.
- Failed task sync attempts are marked `SyncFailed` with a short user-visible error.

Not included in the first pass:

- later edits/deletes to already-synced note/timeline entries
- updating already-mapped local templates from later cloud template edits
- updating already-synced entry-level field values
- archive/delete/tombstone sync
- shared-board/task download
- field-level merge UI

This keeps the implementation honest while proving the core mapping path.

Microsoft Entra ID and any future identity providers attach to the cloud
identity layer. They do not replace the local SQLite identity. A local desktop
user can later link to a cloud account, then sync mappings decide what data
moves.

## Sync Risks To Design Around

The risky parts are not just "upload local rows":

- A local-only user and a logged-in cloud user need a clear identity transition. The app must not silently merge two people just because they used the same machine.
- Shared tasks/workspaces should never be cached as permanently local ownership. They should be visible only while the user is logged in and the server confirms access.
- Revoked sharing must remove local visibility on next sync. Local caches need access checks, not only data freshness checks.
- Sidecar endpoint ownership should continue to be reviewed as desktop threat
  modeling matures; current launches use a random port and per-launch token.
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

The MVP sync strategy is best-effort and status-first:

- Local-only tasks stay local until the user deliberately maps/syncs a board.
- Successful sync stores the remote ID/version in `SyncMapping`.
- Failed sync stores a short failure reason and the last attempt time.
- The UI should make failed/conflicted sync obvious with a small cloud/status indicator.
- If a task cannot be pushed safely, leave the local task intact and show the failure instead of guessing.
- If a duplicate is created during an early sync implementation, prefer user-visible recovery over destructive automatic merge.

Full field-level conflict UI is future work. The state-of-the-art target remains:

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
