# Security Principles

## No Secrets in Source Control

Deployment secrets, connection strings, signing keys, external API keys, SMTP credentials, and production storage paths must not be committed. Use local development files, environment variables, or a secret store.

## Separate Configuration Categories

Configuration is separated into:

- Deployment/runtime configuration
- User/workspace configuration
- Integration configuration

Provider settings can live in the database when integrations are introduced, but credentials and secrets belong in secret storage.

## Preserve Evidence

Task timeline entries are evidence. Meaningful task changes should append timeline entries, and timeline history must not be deleted as part of normal product behavior.

## Require Archive Resolution

Archiving a task must require a resolution reason. This protects task history from becoming ambiguous or silently discarded.

## Keep the MVP Small

AI, MCP, calendar, email, sharing, and desktop support are extensions. They should not be added to the MVP security model before the core task system exists.

## Auth Hardening

See [auth-hardening.md](auth-hardening.md) for the current session authentication scheme, endpoint policy, and remaining hardening work.
