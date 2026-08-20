# Refactor Backlog

This file collects historical seams, legacy naming and cleanup ideas found while working on active features.

Use it when a cleanup is real but not worth derailing the current task.

## Candidates

### Repository-wide formatter baseline

- Status: noted
- Context: `dotnet format DumpTether.sln --verify-no-changes` currently reports
  historical encoding, line-ending, import-order, and whitespace differences
  across old migrations and configuration files.
- Why it can wait: normalizing generated migration history and unrelated source
  would create broad review noise in feature pull requests. New and touched C#
  files can still be formatted with a scoped `--include` pass.
- Future cleanup: establish one encoding and line-ending policy, normalize the
  repository in a dedicated mechanical pull request, then add the formatter as
  a CI gate.

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

- Status: initial live relay implemented; lifecycle verification remains
- Context: hosted web clients receive SignalR invalidations immediately.
  Desktop keeps hosted credentials protected in the local C# sidecar, so React
  cannot safely connect to the hosted hub directly. The sidecar now maintains
  the hosted connection, translates events to local invalidations, and retains
  slow reconciliation for missed events.
- Why it can wait: durable pull/push and catalog reconciliation recover missed
  changes, including after sleep or reconnect. Remaining lifecycle behavior is
  tracked by GitHub issue #77.
- Future cleanup: add explicit reconnect, hosted authorization-loss, logout,
  and remote-session-revocation tests; stop already-connected hosted sessions
  promptly after revocation; avoid reconnecting the web hub for ordinary view
  selection changes. Keep polling as recovery rather than the primary path.

### Cloud-imported cache authorization in the local sidecar

- Status: authorization defect tracked by GitHub issue #109
- Context: imported cloud boards are cached in SQLite under the durable local
  identity. Their `SyncRoot` records preserve hosted role/access provenance and
  prevent disallowed pushes, while the UI hides owner controls for read-only or
  task-share access.
- Why it can wait: modifying local software or SQLite never grants hosted
  access, and the cloud rejects unauthorized writes. However, revoked or
  disconnected shared content can remain locally readable/writable through the
  imported membership, so this is not merely presentation debt.
- Future cleanup: add a desktop cache access policy in application services so
  local read and mutation endpoints consult imported-root role/access metadata;
  purge or disable cloud-owned cache access after hosted revocation or account
  disconnect while preserving unrelated local-only data.

### Transactional account email outbox

- Status: deferred production hardening
- Context: registration and password-recovery email delivery are transactionally
  coupled to their tokens. Scheduled deletion reminders are claimed durably, but
  a process crash after provider acceptance can still cause a duplicate retry.
- Why it can wait: reset links remain one-time and deletion timing remains
  authoritative; a repeated reminder is inconvenient rather than destructive.
- Future cleanup: add a transactional email outbox with provider message IDs,
  idempotent dispatch, retry policy, and delivery observability.

### Account export and lifecycle transaction naming

- Status: deferred privacy and naming cleanup
- Context: self-service delayed deletion is implemented, but GDPR export remains
  operator-assisted. `IRegistrationTransaction` now also protects recovery token
  delivery and no longer describes its full responsibility accurately.
- Why it can wait: the transaction boundary is correct, and operator tooling can
  service export requests while the product is small.
- Future cleanup: add a user-readable export package and rename the shared
  transaction abstraction in a focused cross-module cleanup.
