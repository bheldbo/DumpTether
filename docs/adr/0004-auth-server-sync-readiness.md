# ADR 0004: Auth, Server Hosting, and Sync Readiness

## Status

Accepted

## Context

DumpTether is expected to run in three shapes:

- Hosted web app with a shared PostgreSQL server.
- Desktop app with the same React frontend and a local .NET sidecar API.
- Desktop app optionally signed in and syncing with the hosted server.

The product should not duplicate business rules between web, desktop, and server. Authentication and sync therefore need to sit around the existing Domain/App/API layers instead of replacing them with frontend-only logic.

## Decision

The hosted service should become multi-tenant around explicit user and workspace ownership:

- `AppUser`
- `Workspace`
- `WorkspaceMembership`
- `Device`
- `UserSession`
- `RefreshToken`

Workspace-scoped data such as tasks, projects, templates, fields, notes, archive reasons, and saved filters remains tied to `WorkspaceId`.

The first implementation uses a simple first-party auth foundation:

- `AppUser` with a framework password hash.
- `UserSession` with a random opaque session token returned to the client and stored only as a hash.
- `WorkspaceMembership` as the authorization boundary for authenticated workspace access.
- Secure, HttpOnly cookies in production when cookie auth is used, plus bearer token support for API testing and future desktop clients.

Future hardened server authentication should use:

- ASP.NET Core authentication middleware.
- Short-lived access tokens.
- Rotating refresh tokens stored hashed in the database.
- Device/session records so a user can revoke a browser or desktop install.
- Secure cookies for the website where practical.
- Bearer tokens only where the desktop app or API clients need them.

The API should resolve the current workspace from authenticated membership, not from the temporary development workspace header. That header is development-only and must be removed before production auth.

The desktop app should keep local state in SQLite. Sync should use the same API shape and application rules, with metadata added before offline sync is implemented:

- Stable IDs.
- `CreatedAt`.
- `UpdatedAt`.
- `DeletedAt`.
- `Version`.
- `DeviceId`.
- Append-only note/timeline entries where possible.

## Database Direction

PostgreSQL remains the hosted database. The schema should prefer relational ownership boundaries over broad JSON blobs:

- Workspace membership is relational.
- Task ownership and project tags are relational.
- Field definitions are relational.
- Field values can keep flexible JSON values because the field schema is workspace-defined.
- Runtime secrets are never stored in source control.
- Refresh tokens are stored as hashes, never plaintext.

Indexes should follow the common wall queries:

- `WorkspaceId + ArchivedAt + LastTouchedAt`.
- `WorkspaceId + ProjectId + ArchivedAt`.
- `WorkspaceId + Color`.
- `WorkspaceId + Status`.
- `WorkspaceId + FollowUpAt`.
- Field-value search indexes only after query patterns are clear.

## Security Direction

Every server query must be scoped through the authenticated user's workspace membership. Controllers should not trust client-supplied workspace IDs as authorization.

Production auth work should include:

- Password hashing through the framework, not custom hashing.
- Refresh-token rotation and reuse detection.
- Login/session rate limiting.
- CSRF protection for cookie-authenticated browser flows.
- Secure cookie flags in production.
- Clear separation between deployment secrets and workspace/user configuration.
- Tests proving that a user cannot access another workspace.

## Consequences

The current development workspace selector remains for anonymous local development only. Authenticated requests are scoped through membership. Full login UI, password reset, MFA, OAuth, and sync remain future milestones.

The same C# Domain/App logic remains reusable by the hosted API and the future desktop sidecar API.
