# DumpTether Roadmap

This roadmap is directional. The MVP should stay plain, fast, and focused on a personal task wall, structured notes, templates, views, and archive reasons.

## v0.1 Web MVP

- ASP.NET Core API.
- React web UI.
- PostgreSQL persistence.
- Tasks with compact note history.
- Archive reasons.
- Basic templates, custom fields, and saved views.

## v0.2 Templates, Views, and Search Hardening

- Simpler task wall interaction.
- Task cards that can be scanned like a sticker wall.
- User-created statuses and categories.
- User-selected task colors.
- Faster note entry on tasks.
- Cleaner note history with less timeline ceremony.
- Saved view filters over task text, status, project, archive state, dates, and custom fields.
- Search examples and inline help for templates and views, including custom field queries.

## v0.3 Desktop Online Wrapper

- Add `apps/desktop` Tauri wrapper.
- Reuse the same React frontend.
- Connect the desktop shell to the hosted API.
- Do not add offline storage yet.

## v0.4 Desktop Offline SQLite

- Add local .NET sidecar API.
- Add SQLite persistence provider.
- Store primary local state in SQLite.
- Store small local settings in JSON.
- Prepare attachment storage in the app data folder.

## v0.5 Login and Sync

- Add authentication.
- Add sync metadata such as stable IDs, `CreatedAt`, `UpdatedAt`, `DeletedAt`, `Version`, and `DeviceId`.
- Add sync conflict handling.
- Keep timeline entries append-only where possible.

## v0.6 Sharing

- Share selected tasks, projects, or views.
- Add explicit permissions.
- Keep personal/local-first workflows intact.

## v0.7 Calendar

- Add calendar integration for follow-ups and date-aware views.
- Keep calendar as an extension, not a core dependency.

## v0.8 Email Suggestions

- Add optional email-assisted capture and suggestions.
- Do not make email required for task creation.

## v0.9 AI Summaries and Daily Digest

- Add optional summaries, daily digest, and review assistance.
- Keep AI advisory and reversible.

## v1.0 MCP Read-Only

- Add read-only MCP access for external tools.
- Do not allow MCP write actions until the read model, permissions, and audit story are mature.

## Future: Teams and Enterprise Self-Hosting

- Optional organization/tenant layer.
- Manager/team-lead task assignment to users.
- In-app notifications for newly assigned tasks.
- Microsoft Entra ID/Active Directory and Google Workspace login options.
- Self-hosted API/PostgreSQL deployment for organizations.
- See `docs/product/enterprise-teams.md`.
