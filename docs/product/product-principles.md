# DumpTether Product Principles

DumpTether should feel like a plain personal task wall with structured notes inside each task and powerful filtering when needed.

The canonical hierarchy is `User -> Boards -> Tasks`. A task belongs to one
board and may have multiple categories. Projects are legacy terminology.

`All Tasks` and `Archive` are built-in aggregate surfaces rather than ordinary
boards.

## Feel

The first screen should be a task wall, not a project-management dashboard. Tasks should feel like small, movable, color-coded units that can be scanned quickly.

Users should be able to:

- Capture a task quickly.
- Add a note or field value without ceremony.
- Press Enter to add a new note where that interaction makes sense.
- Click a task to expand it.
- Give tasks their own colors.
- Create their own statuses.
- Create their own categories.
- Filter hard when the wall gets noisy.

## Structured Notes

Each task can have structured fields and notes, but the note experience should stay lightweight. A note list should feel closer to an email thread than an audit log.

Prefer compact note entries:

- Date.
- Author or device later, when needed.
- Note body.
- Small edit/delete affordances where allowed.

Avoid noisy default labels such as "Note created" when the note body and date already explain what happened.

## Filtering

Filtering is a power tool, not the default mood of the app. It should become useful when the task wall grows.

Temporary and reusable personal filters should support criteria such as:

- Status equals a user-defined status.
- Active or archived.
- Project or category.
- Text contains `Jan`.
- Custom field `People` contains `Jan`.
- Custom field `People` contains any of `Jan` or `Lars`.
- Follow-up date windows.
- Not viewed or not touched recently.

Template and filter creation should include small help affordances for examples
like these.

Organization-managed saved views are a future enterprise feature for team or
role queues. They are not part of the current personal product.

## Local-First

Desktop and future mobile clients should remain useful without connectivity.
Users may sign in to the backend configured for that client and synchronize
selected tasks or whole boards. Sync status and failure must be visible without
blocking ordinary editing or risking local work.

## Non-Goals

DumpTether should not feel like:

- A Jira clone.
- A timeline-heavy audit system.
- A complex import or parser tool.
- A kanban-first project manager.
- A generic notes app.

History still matters, but it should support trust and recovery quietly. It should not dominate the product.
