# DumpTether Product Brief

## Status

Owner-confirmed direction as of July 2026.

## Product

DumpTether is a local-first personal working-memory system for tasks that
accumulate context. It should feel like a wall of color-coded sticky notes
backed by a structured database, not like a project-management suite.

The product hierarchy is:

```text
User
  -> Boards
      -> Tasks
          -> Categories
          -> Structured fields
          -> Structured entries
```

A task belongs to one board and may have multiple categories. Projects are
legacy terminology and are not a separate product concept.

`All Tasks` and `Archive` are built-in aggregate surfaces, not ordinary boards.
All Tasks includes owned and legitimately shared tasks across boards. It cannot
be deleted, but a user may hide it through personal display settings.

## Core Experience

Users can:

- capture a task with little ceremony
- scan tasks as colored units on a board
- add compact notes or template-defined structured entries
- start from protected `Basic Task` and `ToDo Task` templates that are provisioned
  for every account and repaired when an older account first loads templates
- check items from a ToDo task directly on the wall without opening the task
- organize with categories, status, color, follow-up dates, and custom fields
- filter temporarily or reuse a personal filter
- archive without losing useful history
- work locally without an account on supported native clients
- sign in and synchronize selected tasks or whole boards with the backend for
  which the client was built
- access synchronized work from browser, desktop, and future mobile clients
- share boards or individual tasks when online

## Filters And Future Views

Current filtering is board-scoped or applies to the All Tasks aggregate.
Filters may be temporary or reusable by the individual user.

Organization-managed saved views are deferred. They may later support team
queues such as active tasks assigned to a person, role, or identity-provider
group. Enterprise views must remain an optional layer and must not make the
personal product feel like Jira.

## Local-First And Sync

A task is the synchronization unit. A board is the synchronization scope and
destination.

Users may select individual tasks for synchronization or enable whole-board
synchronization. Whole-board mode includes existing and future tasks in that
board.

Routine synchronization must not freeze editing. Local edits remain available,
versioned changes are queued, and a task that changes during a sync pass remains
pending for the next pass. The UI should distinguish:

- local only
- pending
- syncing
- synced
- offline
- failed
- conflict
- access revoked

Failed synchronization must leave local work intact. Shared cloud data remains
associated with the authenticated cloud profile and must be re-authorized when
connectivity returns.

## Archive Policy

Archive behavior is policy-driven:

1. The owning board's archive policy applies when it has an override.
2. Otherwise, a personal board uses its owner's user default.
3. The system fallback does not require a resolution and treats the note as
   optional.

The board owner controls a shared board's archive policy. A task shared from a
board keeps the originating board's policy. All Tasks has no archive policy of
its own because every task resolves policy from its real board.

Resolution requirement and archive-note requirement are separate settings. A
specific resolution may still require an explanation. Historical archive
evidence must remain understandable if a resolution is later renamed or
deactivated.

## Product Family

DumpTether is one product with multiple clients:

- Hosted web: React UI, hosted ASP.NET Core API, PostgreSQL.
- Desktop: Tauri shell, local ASP.NET Core sidecar, SQLite, optional cloud sync.
- Future mobile: offline-capable client using the same hosted API and sync
  contract.

C# domain and application rules remain authoritative. React, Rust, and future
mobile code must not duplicate business rules.

DumpTether supports a public hosted deployment and self-hosted deployments.
Web, desktop, and future mobile artifacts are built for a configured backend.
Backend selection is deployment configuration, not an everyday in-product
server switcher.

## Explicitly Deferred

- organization and tenant administration
- AD or identity-provider group assignment
- team-lead task distribution and reporting
- organization-managed saved views and work queues
- advanced enterprise archive governance
- AI, MCP, calendar, and email-derived task features

These remain possible extensions after the personal web, desktop, mobile, sync,
identity, and deployment paths are functional and safe.

