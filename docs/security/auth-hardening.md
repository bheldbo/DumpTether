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
- Rate limiting exists for auth and task write endpoints.

## Remaining Hardening Work

- Add CSRF protection before relying on cookie auth for state-changing browser requests.
- Add role-specific endpoint policies once owner/member/share permissions settle.
- Add security headers at the reverse proxy or API layer for production.
- Add audit-friendly logs for auth failures without logging tokens or secrets.
- Disconnect or reject existing live-update connections promptly when sessions are revoked.
- Add storage and malware-scan rules before attachments are implemented.
