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
