# Task Wall UI Principles

DumpTether should feel like a lightweight personal sticker wall backed by a structured database: quick to dump into, quick to scan, and powerful to filter when the wall gets noisy.

## Task Wall

- The task wall is the default working surface.
- The top header should identify the current workspace, project context, and saved view without feeling like a dashboard.
- Workspace and project colors are lightweight grouping cues.
- Task cards should look and behave like plain post-it notes.
- The task's chosen color belongs to the whole card, not a small label or category strip.
- The wall card is for scanning, not editing.
- A card should show title, latest note content, small status/category/date signals, follow-up date when set, and whether notes exist.
- Clicking a card should focus that task and let it fill the workspace.
- Color, status, category, title, dates, fields and notes are edited in the focused task surface.
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
- The wall should have compact filters for text, status, category, color, project, follow-up, and not-touched age.
- Color filters should operate on the user's chosen task color, whatever that color means to them.
- Color filters should show both a swatch and the color code.
- Suggested view colors should come from colors that exist on active or archived tasks.
- Reset filters should be obvious when filters are active.
- Saved views still define reusable ways to see tasks.
- Saved views should start with only `Overview` and `Archive`; users can drag sidebar views into their preferred order.
- Do not build a full advanced query builder yet.

## Localization

- The UI should support English and Danish first.
- Language selection belongs in sidebar settings.
- Localization should cover the main navigation and task-wall controls before deeper admin text.

## Templates and Fields

- Basic task creation should not force template choice.
- Quick-create should use the configured default template.
- Custom fields should be available in task detail without dominating the screen.
- Template help should explain that fields add structure and can later be used by views and filters.
- Useful examples include `People contains Jan`, `Status = Active and People contains Jan/Lars`, and `Not touched in 14 days`.

## Intentionally Not Included Yet

- Desktop/Tauri.
- SQLite/offline sync.
- Login/auth.
- Sharing.
- Calendar or email scanning.
- AI.
- MCP.
- Pasted-note import.
- WiX, MSI packaging, or installer signing.
- Complex kanban.
- Full advanced query builder.
