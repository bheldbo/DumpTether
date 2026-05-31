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

## User and Workspace Configuration

User/workspace configuration includes templates, field definitions, saved views, archive reasons, categories, statuses, colors, default project, and display preferences.

These values belong to the application data model because they are part of how a workspace behaves. They should be persisted through the normal application database schema and migrations.

## Integration Configuration

Integration configuration includes provider settings and credentials for future extensions.

Provider metadata may be stored in the database when integrations are introduced. Future email, calendar, AI, and MCP credentials and secrets must be stored in a secret store or deployment environment, not in source control or ordinary database records.

## GitHub Secret Handling

Real secrets must not be committed to GitHub. Repository secrets, organization secrets, environment secrets, or deployment platform secret stores should be used for CI/CD and production deployments.

The MVP does not include AI, MCP, email, calendar, sharing, desktop, or offline sync integrations, so no integration secrets are required yet.

## Consequences

- The repository remains safe to publish publicly or privately.
- Local development remains reproducible through documented examples.
- Future integrations can be added without changing the core configuration boundary.
- User/workspace configuration can evolve through EF Core migrations with the rest of the relational model.
