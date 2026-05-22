# DumpTether Product Principles

DumpTether should feel like a plain personal task wall with structured notes inside each task and powerful filtering when needed.

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

Saved views should eventually support filters such as:

- Status equals a user-defined status.
- Active or archived.
- Project or category.
- Text contains `Jan`.
- Custom field `People` contains `Jan`.
- Custom field `People` contains any of `Jan` or `Lars`.
- Follow-up date windows.
- Not viewed or not touched recently.

Template and view creation should include small help affordances for examples like these.

## Non-Goals

DumpTether should not feel like:

- A Jira clone.
- A timeline-heavy audit system.
- A complex import or parser tool.
- A kanban-first project manager.
- A generic notes app.

History still matters, but it should support trust and recovery quietly. It should not dominate the product.
