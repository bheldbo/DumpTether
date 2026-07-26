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

## Enforce Effective Archive Policy

Archiving must enforce the policy of the task's owning board. A board override
takes precedence over the owner's user default. Without either, the system
fallback does not require a resolution and treats the archive note as optional.

A selected resolution may require an explanation. Historical evidence should
retain the meaning of the selected resolution even if its configuration later
changes.

## Keep the MVP Small

AI, MCP, calendar, email, sharing, and desktop support are extensions. They should not be added to the MVP security model before the core task system exists.

## Auth Hardening

See [auth-hardening.md](auth-hardening.md) for the current session authentication scheme, endpoint policy, and remaining hardening work.
