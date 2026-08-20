# ADR 0007: Effective Archive Policy

## Status

Superseded on 2026-08-20 by direct archive behavior.

## Context

An earlier design proposed configurable user and board archive policies,
resolution lists, and explanation requirements. That model added configuration,
validation, persistence, and UI ceremony to an action that DumpTether users need
to perform quickly.

## Superseding Decision

Archiving is direct:

- an authorized user can archive a task without a resolution or note
- archiving sets `ArchivedAt` and creates a `TaskTimelineEntry`
- prior timeline entries, notes, fields, and task structure are retained
- archived tasks are excluded from active walls
- reopening clears `ArchivedAt` and creates a new timeline entry
- archive and reopen authorization remains backend-authoritative

Status and ordinary notes provide context when the user wants to record it.
There is no `ArchiveResolution` entity, task foreign key, archive-policy setting,
or resolution-management API.

## Consequences

- The archive flow stays fast and consistent across web, desktop, and future
  mobile clients.
- Historical task evidence remains available without requiring audit ceremony.
- The database and API no longer carry archive-resolution configuration.
- If organization-level governance is reconsidered later, it must be proposed as
  a new decision and must not silently complicate the personal task-wall flow.
