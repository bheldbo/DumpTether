# Authentication Hardening

DumpTether uses a first-party session token for the current MVP. The frontend sends it as a bearer token, and the API can also read the session cookie created by the auth endpoints.

## Session Scheme

`DumpTether.Session` is the ASP.NET Core authentication scheme for DumpTether sessions.

The scheme:

- reads the current token through `IAuthTokenAccessor`
- hashes the token before lookup
- rejects missing, expired, revoked, or inactive-user sessions
- creates a `ClaimsPrincipal` with user id, session id, email, and display name

This makes ASP.NET Core aware of DumpTether users. Controllers and hubs can then use normal authorization policies instead of each endpoint manually re-checking tokens.

## Session Policy

`DumpTether.SessionRequired` is the endpoint authorization policy.

The policy:

- allows unauthenticated access only when `Auth:RequireAuthentication=false`
- requires an authenticated DumpTether session when `Auth:RequireAuthentication=true`
- is applied to task, workspace, project/category, template, view, archive, sharing, and live-update endpoints

Application services still enforce workspace membership, owner/member boundaries, and task-share access. The policy is defense in depth at the HTTP boundary; it is not the only authorization layer.

## Current Protections

- Session tokens are stored hashed in the database.
- Expired, revoked, and inactive-user sessions are rejected.
- Production forces `Auth:RequireAuthentication=true`.
- Development login is disabled outside development.
- Protected controllers use the session policy.
- SignalR live updates use the same session policy and authenticated user identity.
- Cookie-authenticated unsafe requests require a matching `DumpTether.Csrf` cookie and `X-DumpTether-CSRF` header.
- Rate limiting exists for auth and task write endpoints.
- The API adds conservative security headers: `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, and `X-Permitted-Cross-Domain-Policies`.

## Roles

The intentional role set is small:

- `Owner`: controls the board/workspace and can delete it.
- `Member`: can participate in shared boards and shared tasks, but cannot delete owner resources.
- `ReadOnly`/`Guest`: future permission level for limited access without write authority.

The HTTP policy only proves that a request belongs to a valid session. Workspace and task permissions are still checked in the application layer because the allowed action depends on the target workspace, task, share, and ownership boundary.

## Remaining Hardening Work

- Add role-specific endpoint policies once `ReadOnly`/`Guest` behavior is implemented.
- Add audit-friendly logs for auth failures without logging tokens or secrets.
- Disconnect or reject existing live-update connections promptly when sessions are revoked.
- Add storage and malware-scan rules before attachments are implemented.
