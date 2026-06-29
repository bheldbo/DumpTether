# ADR 0002: Configuration Boundaries

## Status

Accepted

## Context

DumpTether needs configuration for local development, deployment, user/workspace preferences, and future integrations. These concerns have different lifecycles and security requirements, so they should not be stored or managed as one undifferentiated settings blob.

## Decision

DumpTether separates configuration into three categories.

## Deployment and Runtime Configuration

Deployment/runtime configuration includes connection strings, auth signing keys, cookie settings, allowed origins, reverse proxy settings, external API keys, SMTP credentials, storage paths, host names, and environment-specific runtime switches.

These values are supplied through environment variables, deployment platform settings, local user secrets, or uncommitted local settings files. `appsettings.example.json` documents the expected shape only and must not contain real secrets.

At application startup, the API reads deployment/runtime configuration into a small runtime setup object and module-specific `IOptions<T>` bindings. `Program.cs` should compose startup; modules should own their option classes and validation where practical. This keeps configuration from becoming a shared global bag while still allowing `.env`, Docker, Visual Studio launch profiles, and command-line arguments to feed the same ASP.NET Core configuration pipeline.

## User and Workspace Configuration

User/workspace configuration includes templates, field definitions, saved views, archive reasons, categories, statuses, colors, default project, and display preferences.

These values belong to the application data model because they are part of how a workspace behaves. They should be persisted through the normal application database schema and migrations.

## Integration Configuration

Integration configuration includes provider settings and credentials for future extensions.

Provider metadata may be stored in the database when integrations are introduced. Future email, calendar, AI, and MCP credentials and secrets must be stored in a secret store or deployment environment, not in source control or ordinary database records.

## GitHub Secret Handling

Real secrets must not be committed to GitHub. Repository secrets, organization secrets, environment secrets, or deployment platform secret stores should be used for CI/CD and production deployments.

The MVP does not include AI, MCP, email scanning, calendar sync, or full offline cloud sync integrations, so no integration secrets are required for those yet. Sharing and the desktop shell foundation exist, but must still use the same runtime/secret boundary.

## Consequences

- The repository remains safe to publish publicly or privately.
- Local development remains reproducible through documented examples.
- Future integrations can be added without changing the core configuration boundary.
- User/workspace configuration can evolve through EF Core migrations with the rest of the relational model.
- Database schema definitions and migrations stay with the EF Core Data project.
- `DumpTether.Database` is the runnable maintenance shell for migration/status/reset operations, while scripts may wrap it when Docker orchestration is also needed.
- The API should not become the primary database maintenance console.
