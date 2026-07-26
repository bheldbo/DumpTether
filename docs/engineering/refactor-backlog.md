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
