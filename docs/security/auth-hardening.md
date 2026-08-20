# Authentication Hardening

DumpTether uses a first-party opaque session token. Hosted browser sessions rely
on the Secure, HttpOnly session cookie created by the auth endpoints and do not
retain the bearer token in browser storage. The desktop client keeps a bearer
token only for its local sidecar boundary, where cookie-only browser auth is not
the right transport.

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
- Hosted browser tokens are not persisted in `localStorage` or
  `sessionStorage`; browser API and SignalR calls use the HttpOnly cookie.
- Desktop bearer tokens remain separate from hosted browser state and are used
  only for the local sidecar/API-client boundary.
- Expired, revoked, and inactive-user sessions are rejected.
- Production forces `Auth:RequireAuthentication=true`.
- Server signup can be `Open`, `Whitelist`, `InviteOnly`, or `Closed`; production examples default to invite-only.
- Outside Development, every available sign-up mode requires email confirmation
  so an unverified password account cannot claim invitations or task shares sent
  to somebody else's address.
- Development login is disabled outside development.
- Temporary browser `Guest` sessions are blocked from unsafe non-auth API writes so they cannot persist task data on a hosted server.
- Protected controllers use the session policy.
- SignalR live updates use the same session policy, authenticated user identity, and hub-time session revalidation.
- SignalR transport requests are exempt from the JSON CSRF middleware because the hub validates sessions separately.
- Cookie-authenticated unsafe requests require a matching `DumpTether.Csrf` cookie and `X-DumpTether-CSRF` header.
- Rate limiting exists for auth and task write endpoints.
- Password recovery has a dedicated IP-only limiter so arbitrary bearer values
  cannot create new anonymous rate-limit partitions.
- Recovery tokens are random, stored only as hashes, expire after at most one
  hour, are consumed atomically, and invalidate other outstanding reset links.
- Successful password resets revoke every active session and never sign the
  user in automatically.
- Account deletion uses a durable pending/deleting state, atomic worker claims,
  a 48-hour cancellation period, and a 24-hour reminder.
- The API adds conservative security headers: `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, and `X-Permitted-Cross-Domain-Policies`.
- Unknown presented tokens are recorded at Debug level by token source and path
  only, while revoked, expired, inactive-user, and missing-user session states
  retain warning-level audit evidence. Raw tokens are never logged.
- ASP.NET Core request start/finish diagnostics are suppressed because SignalR
  transports may carry bearer tokens in the query string when a non-browser
  client cannot use the hosted cookie path.
- OAuth completion accepts only framework-recognized local URLs or an absolute
  URL whose full origin matches the current forwarded request origin.
- Cloud synchronization sends bearer credentials only to HTTPS endpoints, with
  HTTP reserved for loopback development endpoints.

## Roles

The intentional role set is small:

- `Owner`: controls the board/workspace and can delete it.
- `Member`: can participate in shared boards and shared tasks, but cannot delete owner resources.
- `ReadOnly`/`Guest`: an invited, authenticated user with read-only board or task access. This is not the same thing as anonymous guest login.

The HTTP policy proves that a request belongs to a valid session. Workspace-write endpoint policies also guard project/category, template, saved-filter, and archive-reason writes. Task writes remain checked in the application layer because the allowed action can depend on a task-level viewer/editor share as well as the workspace role.

## Remaining Hardening Work

- Replace synchronous recovery/deletion mail with a transactional outbox when
  retry telemetry and dead-letter operations are introduced.
- Disconnect active SignalR connections immediately after password reset rather
  than relying on the next hub call/reconnect to observe revoked sessions.
- Implement portable account export and test deletion against backup restore
  procedures before claiming deployment-level GDPR compliance.
- Add image-storage rules before image uploads are implemented.
- Revisit file scanning only if DumpTether later accepts arbitrary documents.
- Add deeper audit events for suspicious auth behavior once production observability exists.
