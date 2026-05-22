# Task Wall UI Principles

DumpTether should feel like a lightweight personal sticker wall: quick to dump into, quick to scan, and powerful to filter when the wall gets noisy.

## Task Wall

- The task wall is the default working surface.
- Task cards should stay compact and readable.
- A card should show title, status, category, color, last touched date, follow-up date, and whether notes exist.
- Creating a task should be a single-line action at the top of the wall.
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
- Reset filters should be obvious when filters are active.
- Saved views still define reusable ways to see tasks.
- Do not build a full advanced query builder yet.

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
