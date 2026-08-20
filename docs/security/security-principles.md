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

## Preserve Archive Evidence

Archiving is a direct task action and does not require a resolution or note.
Authorization remains backend-authoritative, and the archive operation must add
timeline evidence without deleting or rewriting prior history. Reopening adds
its own timeline evidence and retains the task's earlier notes.

## Keep the MVP Small

AI, MCP, calendar, email, sharing, and desktop support are extensions. They should not be added to the MVP security model before the core task system exists.

## Auth Hardening

See [auth-hardening.md](auth-hardening.md) for the current session authentication scheme, endpoint policy, and remaining hardening work.
