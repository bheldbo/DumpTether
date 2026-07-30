# Refactor Backlog

This file collects historical seams, legacy naming and cleanup ideas found while working on active features.

Use it when a cleanup is real but not worth derailing the current task.

## Candidates

### Shared clock lives under `DumpTether.App.Tasks`

- Status: noted
- Context: `IClock` and `SystemClock` are shared application infrastructure, but currently live in `src/DumpTether.App/Tasks/`.
- Why it can wait: the type is small and already wired correctly through dependency injection.
- Future cleanup: move clock abstractions to a shared app infrastructure namespace/folder and update imports.

### Project remains as legacy product terminology

- Status: owner confirmed removal from the product model
- Context: current product hierarchy is `User -> Boards -> Tasks`, with tasks
  having multiple categories. Project entities, controllers, DTOs, filters, and
  foreign keys still exist in parts of the implementation.
- Why it can wait: removing persisted concepts safely requires a repository-wide
  inventory, provider migrations, compatibility decisions for existing data,
  and API/frontend updates.
- Future cleanup: produce a dedicated work packet to migrate useful project
  values into categories where needed, remove the separate project surface, and
  verify PostgreSQL/SQLite parity.

### SavedViews outlive the current filter direction

- Status: deferred product concept
- Context: personal workflows currently use temporary or reusable board filters.
  Organization-managed saved views are deferred until enterprise assignment and
  team queues are deliberately designed. SavedView entities, API endpoints, and
  frontend code still exist.
- Why it can wait: immediate deletion could discard user data and may remove
  useful query/filter work before the personal replacement is settled.
- Future cleanup: inventory actual usage, preserve reusable filter behavior, and
  either migrate SavedViews into a personal saved-filter model or retire them
  with explicit provider migrations.

### Synced permanent deletion needs tombstones

- Status: blocks bulk archive cleanup
- Context: permanent local task deletion can leave a sync mapping without a
  deletion tombstone. A later pull could recreate a task the user believed was
  permanently removed.
- Why it can wait: board deletion is already owner-only and confirmed, while
  archive retention can remain non-destructive until deletion sync is designed.
- Future cleanup: add task deletion tombstones or an explicit unlink/delete
  protocol, cover both PostgreSQL and SQLite, then enable bulk archive cleanup.

### Desktop device identity and cloud session health

- Status: deferred hardening
- Context: desktop cloud sessions currently use a human-readable device name and
  sync roots still use a default local device identifier. Cloud tokens are
  protected locally and verified when sync runs, but the UI does not yet
  distinguish an unreachable cloud from a remotely revoked session at startup.
- Why it can wait: local startup must remain independent of the cloud, and sync
  already rejects invalid hosted sessions authoritatively.
- Future cleanup: generate a random per-install DeviceId in protected app data,
  add a non-blocking cloud-session probe with explicit offline/revoked states,
  and disconnect SignalR promptly when hosted sessions are revoked. Do not use
  MAC addresses or bind sessions to changing IP addresses.

### Authentication metadata semantics

- Status: deferred hardening
- Context: `UserSession.LastSeenAt` is stored but is not refreshed by ordinary
  authenticated requests, and the current IP hash is useful only as audit
  metadata.
- Why it can wait: expiry and revocation remain authoritative and the UI no
  longer presents LastSeenAt as live device activity.
- Future cleanup: implement a throttled last-seen update if product value
  justifies the writes, and use a keyed HMAC or omit IP metadata rather than a
  plain deterministic hash.

### Desktop remote live-update relay

- Status: durability fallback implemented; live relay deferred
- Context: hosted web clients receive SignalR invalidations immediately.
  Desktop keeps hosted credentials protected in the local C# sidecar, so React
  cannot safely connect to the hosted hub directly. The desktop currently
  reconciles cloud boards and polls linked roots every few seconds while active.
- Why it can wait: durable pull/push and catalog reconciliation recover missed
  changes, including after sleep or reconnect. A relay adds connection
  lifecycle and session-revocation behavior that deserves focused tests.
- Future cleanup: add a desktop-only hosted SignalR client in the sidecar,
  translate remote workspace IDs through `SyncRoot`, trigger authoritative sync,
  then emit local invalidation events. Keep polling as recovery.

### Cloud-imported cache authorization in the local sidecar

- Status: frontend enforces hosted role; hosted API remains authoritative
- Context: imported cloud boards are cached in SQLite under the durable local
  identity. Their `SyncRoot` records preserve hosted role/access provenance and
  prevent disallowed pushes, while the UI hides owner controls for read-only or
  task-share access.
- Why it can wait: modifying local software or SQLite never grants hosted
  access, and the cloud rejects unauthorized writes. The remaining concern is
  preventing misleading local-only edits to a read-only cache.
- Future cleanup: add a desktop cache access policy in application services so
  local mutation endpoints also consult imported-root role/access metadata.
