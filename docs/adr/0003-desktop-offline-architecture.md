# ADR 0003: Desktop and Offline Architecture

## Status

Accepted

## Context

DumpTether should eventually support a standalone desktop app, but the MVP remains a web-first personal task-and-note system. The desktop direction must not create a second product with duplicated rules, duplicated validation, or a different API shape.

The desired experience is one DumpTether application that can run in two modes:

- Hosted web app with PostgreSQL.
- Local desktop app with SQLite.

## Decision

DumpTether will keep shared business logic in C#:

- `src/DumpTether.Domain` for domain rules.
- `src/DumpTether.App` for application use cases.
- `src/DumpTether.Api` for the HTTP API shape used by both web and desktop.

The React frontend in `apps/web` should be reused by the future desktop app. A future `apps/desktop` project may wrap the same UI with Tauri.

The shared UI should eventually show a small connection/sync status indicator so a desktop user can tell whether they are local-only, connected to the hosted server, signed in, or syncing. That indicator is future UX; it must not create a second auth or sync implementation.

The preferred desktop design is:

```text
Tauri desktop shell
  -> starts local .NET sidecar API on localhost
  -> loads the shared React UI
  -> React talks to the local API
  -> local API stores data in SQLite
```

The hosted web design is:

```text
Browser
  -> loads the shared React UI
  -> React talks to the hosted API
  -> hosted API stores data in PostgreSQL
```

The data projects may be split later to make this explicit:

```text
src/
  DumpTether.Domain/
  DumpTether.App/
  DumpTether.Api/
  DumpTether.Data/
  DumpTether.Data.Postgres/
  DumpTether.Data.Sqlite/
  DumpTether.Sync/

apps/
  web/
  desktop/
```

`DumpTether.Data` should contain shared EF Core abstractions and mapping decisions where practical. Provider-specific setup should move into `DumpTether.Data.Postgres` and `DumpTether.Data.Sqlite` when SQLite support begins.

## Local State

SQLite is the primary local application state database for the desktop app. JSON is not the primary live save format.

Expected local database locations:

```text
Windows: %APPDATA%\DumpTether\dumptether.db
Linux:   ~/.local/share/DumpTether/dumptether.db
```

Small JSON files may be used for user settings, window preferences, and lightweight local configuration.

Attachments should live in the application data folder and be referenced by database records.

## Export and Backup

A future `.dumptether` export may be a zipped bundle:

```text
my-backup.dumptether
  manifest.json
  database.sqlite
  attachments/
```

`manifest.json` should describe the export version, source app version, export timestamp, and any integrity metadata needed to validate the bundle.

## Future Sync

Login and sync are future work, not part of the MVP.

When sync is introduced, persisted entities should move toward sync-friendly metadata:

- Stable IDs.
- `CreatedAt`.
- `UpdatedAt`.
- `DeletedAt`.
- `Version`.
- `DeviceId`.
- Append-only timeline entries where possible.

The timeline should remain evidence-oriented. Deletions and edits that matter to sync should preserve enough metadata to reason about conflicts and history.

## Packaging

Tauri can later build Windows desktop installers on a Windows machine. Future targets may include:

- `.exe` installer.
- `.msi` installer.
- Linux AppImage, deb, or rpm packages depending on target needs.

Signing and installer hardening are future release-engineering concerns.

## Non-Goals

The MVP does not include the desktop app, offline sync, login, sharing, AI, email, calendar, or MCP integrations.

AI, email, calendar, and MCP integrations remain future extensions after the core task, template, view, archive, and sync foundations are stable.

## Consequences

- The web app and desktop app can share one UI.
- Domain rules and validation stay in C# instead of being copied into TypeScript, Rust, or another desktop backend.
- The API remains the product contract for both hosted and local runtimes.
- SQLite can be added later without changing the user-facing model.
- Desktop work can start as an online wrapper before offline storage and sync are introduced.
