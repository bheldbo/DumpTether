# Task Wall UI Principles

DumpTether should feel like a lightweight personal sticker wall backed by a structured database: quick to dump into, quick to scan, and powerful to filter when the wall gets noisy.

## Task Wall

- The task wall is the default working surface.
- The top header should identify the current board and active filters without feeling like a dashboard.
- Board and task colors are lightweight user-defined grouping cues.
- Task cards should look and behave like plain post-it notes.
- The task's chosen color belongs to the whole card, not a small label or category strip.
- The wall card is for scanning, not editing.
- A card should show title, latest note content, small status/category/date signals, follow-up date when set, whether notes exist, and up to three tiny subtask notes with an `… +N` overflow cue.
- Clicking a card should focus that task and let it fill the workspace.
- Color, status, category, title, dates, fields and notes are edited in the focused task surface.
- User-configured statuses may have colors; use the color consistently on compact status chips without competing with the task card color.
- A focused subtask must retain visible Board → Parent → Subtask context and feel like a document nested under its parent.
- Subtasks use status such as `Done` for completion. Their destructive action is a confirmed permanent Delete, not Archive.
- Creating a task should start from a compact `+` action so the wall keeps the screen.
- Pressing Enter in quick-create creates the task immediately.
- The quick-create input should stay focused after creation.
- Quick syntax such as `Call Jan #Waiting` or `Order trackers @Procurement` is future work, not MVP behavior yet.

## Notes

- Notes should feel like a simple email/message track inside a task.
- Pressing Enter in the note box adds the note.
- Shift+Enter inserts a newline.
- After adding a note, the note box clears and keeps focus.
- Default note rendering should show date/time and content.
- Avoid noisy labels such as `Note created`, `Timeline entry created`, and `Field changed` in the default note view.
- Existing notes can be edited or deleted with small inline controls.
- Deleting a note should use a small inline confirmation or an undo pattern.

## Filters

- Temporary filters should not require saving a view.
- The wall should have compact filters for text, status, category, color, board, follow-up, and not-touched age.
- Color filters should operate on the user's chosen task color, whatever that color means to them.
- Color filters should show both a swatch and the color code.
- Suggested view colors should come from colors that exist on active or archived tasks.
- Reset filters should be obvious when filters are active.
- Personal filters may be temporary or reusable.
- `All Tasks` and `Archive` are system surfaces. All Tasks may be hidden through personal display settings but cannot be deleted.
- Organization-managed saved views are deferred until enterprise team workflows are deliberately designed.
- Do not build a full advanced query builder yet.

## Localization

- The UI should support English and Danish first.
- Language selection belongs in sidebar settings.
- Localization should cover the main navigation and task-wall controls before deeper admin text.

## Templates and Fields

- Basic task creation should not force template choice.
- Quick-create should use the configured default template.
- Every account has one protected `Basic Task` template. Its stable built-in
  identity, rather than its display name, is used by sync.
- `Basic Task` provides a task-level description and leaves structured entry
  rows empty. Notes remain available on every task.
- Header and entry regions are optional. A region with no fields has no layout
  rows and does not render an empty panel on a task.
- Subtasks sit directly below the parent header as compact nested sticky notes.
  They use the normal task model rather than a special checklist projection.
- Built-in templates cannot be renamed, edited, or deleted. Users create a
  custom template when they need a different structure.
- Custom fields should be available in task detail without dominating the screen.
- Template help should explain that fields add structure and can later be used by filters.
- Useful examples include `People contains Jan`, `Status = Active and People contains Jan/Lars`, and `Not touched in 14 days`.

## Product Tour

- Signed-out users should be able to open a localized, client-only example tour without creating an account.
- Example boards and tasks must remain static in browser memory and must never be written to the API or a user's account.
- The tour should demonstrate boards, templates, categories, temporary filters, follow-ups, task colors, structured fields, and compact note history through realistic scenarios.
- Tour controls should behave like the product where practical: board switching, filtering, and opening a task should be interactive.
- The page must clearly label example data and provide a direct route back to login or the user's task wall.

## Intentionally Deferred

- Organization-managed saved views.
- AD/group task assignment.
- Calendar or email scanning.
- AI.
- MCP.
- Pasted-note import.
- WiX, MSI packaging, or installer signing.
- Complex kanban.
- Full advanced query builder.
