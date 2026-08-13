# ADR 0011: Task and subtask hierarchy

## Status

Accepted.

## Context

Most DumpTether work is a single task. Some tasks need a handful of smaller
steps, but turning the main wall into a tree or a kanban board would add visual
and behavioral weight to the common case.

## Decision

- A `TaskItem` may reference one parent `TaskItem` in the same board.
- Hierarchy is intentionally one level deep. A subtask cannot contain another
  subtask.
- Normal board queries return root tasks only. Subtasks are loaded when their
  parent is opened and are displayed as a compact task wall inside the detail
  view.
- A subtask remains a complete `TaskItem`: it uses the same fields, notes,
  timeline, archive rules, sharing checks and sync model as any other task.
- Creating a subtask touches its parent and leaves timeline evidence on both
  records.
- Sync transports the parent ID and processes parents before children.
- Copying a selected parent automatically copies every readable direct child
  and remaps the copied parent ID. Copying a child by itself creates a root task
  in the destination.
- Permanent deletion of an archived parent expands to its archived children.
  It is rejected while any child remains active, so hidden active work is never
  erased as a side effect.
- Parent assignment is immutable after creation. Moving or re-parenting tasks
  is deferred until a user workflow justifies the added conflict and timeline
  rules.

## Consequences

The wall stays flat and scannable while complex tasks can be decomposed without
a second model or duplicated validation. Provider-specific migrations add the
self-reference for both PostgreSQL and SQLite. Future mobile and desktop clients
can use the same endpoints and DTOs.
