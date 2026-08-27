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
- Normal board queries return root tasks only. Root summaries include a bounded
  set of compact subtask previews and a total active-child count. The full child
  list is loaded when its parent is opened and displayed as a compact task wall
  inside the detail view.
- A subtask remains a complete `TaskItem`: it uses the same fields, notes and
  sync model as any other task. It cannot be shared directly. Board access or a
  share on its root parent task governs access to the subtask, so sharing remains
  a board/root-task concept instead of adding another hierarchy layer. Its lifecycle is intentionally
  simpler: status (for example `Done`) expresses completion and an owner-confirmed
  Delete action permanently removes the subtask instead of archiving it.
- Creating a subtask touches its parent and leaves timeline evidence on both
  records. Permanently deleting a subtask leaves deletion evidence on the parent
  before removing the child, its fields, notes, history and shares.
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
- Every focused task shows its hierarchy path. Root tasks show `Board → Task`;
  subtasks show `Board → Parent task → Subtask`. The board and parent segments
  are explicit navigation targets. The Back action preserves the surface that
  opened the subtask (board wall or parent detail), while deletion always returns
  to the surviving parent.

## Consequences

The wall stays flat and scannable while complex tasks can be decomposed without
a second model or duplicated validation. Provider-specific migrations add the
self-reference for both PostgreSQL and SQLite. Future mobile and desktop clients
can use the same endpoints and DTOs.
