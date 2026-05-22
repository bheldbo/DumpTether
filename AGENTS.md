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

## Database rules

- Use PostgreSQL.
- Use EF Core migrations.
- Do not change persisted entities without a migration.
- Use JSON for flexible configuration and field values only where appropriate.
- Keep core concepts relational.

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
