# AGENTS.md

## Product

This project is called DumpTether.

DumpTether is a lightweight personal task-and-note system that turns messy working notes into structured tasks with history, templates, views, and archive reasons.

The product should feel like a plain personal task wall with structured notes inside each task and powerful filtering when needed.

It is not a Jira clone.
It is not a timeline-heavy audit system.
It is not a generic notes app.
It is not a kanban-first project manager.
It is not a complex import or parser tool.

## Core rules

- Everything is a task.
- Every task has structured fields.
- Every task has structured notes.
- Task history should be useful evidence, not timeline ceremony.
- Templates define structure.
- Views define how the user sees tasks.
- Projects group work.
- Global views cut across projects.
- Archive requires a resolution.
- Sharing, calendar, email and AI are extensions.

## Architecture

Use a modular monolith.

Projects:

- apps/web: React + TypeScript + Vite frontend
- src/DumpTether.Api: ASP.NET Core API
- src/DumpTether.Domain: domain model and business rules
- src/DumpTether.App: application services/use cases
- src/DumpTether.Data: EF Core persistence
- src/DumpTether.Database: runnable database maintenance shell for migrations/local data chores

Do not introduce microservices.
Do not introduce AI features into the MVP.
Do not introduce MCP into the MVP.
Do not introduce calendar/email integrations into the MVP.

## Backend rules

- Use C# and ASP.NET Core.
- Use `TaskItem`, not `Task`.
- Keep domain logic out of controllers.
- Controllers call application services.
- Application services enforce use cases.
- Domain entities enforce core invariants.
- Every meaningful task change should create a TaskTimelineEntry.
- Archiving a task must require an archive resolution reason.
- Do not delete timeline history.
- Do not store deployment secrets in source control.

## Configuration rules

Separate configuration into three categories:

1. Deployment/runtime configuration
   - connection strings
   - signing keys
   - external API keys
   - SMTP credentials
   - storage paths

2. User/workspace configuration
   - templates
   - fields
   - saved views
   - archive reasons
   - default project
   - display preferences

3. Integration configuration
   - provider settings in the database
   - credentials/secrets in secret storage

Never commit real secrets.

## Frontend rules

- Use React + TypeScript.
- Keep task field rendering modular.
- New field types must include renderer and editor support.
- Prefer a plain task wall with simple task units.
- Let users create their own statuses, categories, and task colors.
- Make note entry fast, with minimal overhead.
- Prefer simple list/table/detail views before kanban.
- The task detail page must support structured fields and compact note history.

## Frontend architecture rules

- Treat `apps/web/src/App.tsx` as app composition and cross-feature orchestration. Do not add large feature UI directly to it.
- Put feature-owned UI in `apps/web/src/features/<feature>/`.
- Put reusable primitives in `apps/web/src/components/`.
- Keep API access in `apps/web/src/api.ts`; feature components should receive data and callbacks through props.
- Keep shared formatting, filtering, layout and field logic in narrow utility modules instead of duplicating it inside components.
- Keep localization strings in the localization module; do not hardcode user-facing English or Danish inside feature components.
- Prefer typed DTOs from `apps/web/src/types.ts` over feature-local response shapes.
- Use toasts for recoverable user-facing errors. Use blocking page errors only when the page cannot function.
- Backend authorization and validation are authoritative. Frontend checks are only for a smoother user experience.
- Responsive work must be checked across desktop, tablet and phone widths when the affected UI is visible there.

When adding a feature:

1. Add backend/domain/app/data behavior and tests when the feature changes business behavior.
2. Add or update API DTOs and client API functions before wiring UI.
3. Add the UI as a feature module, with small reusable components extracted when they cross feature boundaries.
4. Add localization keys for visible text.
5. Run the backend and frontend checks listed below, or explain why a check could not run.

## Database rules

- Use PostgreSQL.
- Use EF Core migrations.
- Do not change persisted entities without a migration.
- Use JSON for flexible configuration and field values only where appropriate.
- Keep core concepts relational.
- Keep domain entities in `DumpTether.Domain`.
- Keep application repository/use-case interfaces in `DumpTether.App`.
- Keep EF Core mappings, repositories and migrations in `DumpTether.Data`.
- Use `DumpTether.Database` for runnable database maintenance; do not turn the API into a database console.

## Testing rules

Backend:
- dotnet test

Frontend:
- npm run lint
- npm run typecheck
- npm run build

If tests cannot be run, explain why.

## Pull request rules

Every PR must include:

- Summary
- What changed
- Tests run
- Risks
- Follow-up work
