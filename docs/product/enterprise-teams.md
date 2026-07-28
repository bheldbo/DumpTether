# Enterprise and Teams Direction

## Status

Future idea. Not MVP.

## Idea

DumpTether may later support a self-hosted teams mode where an organization runs its own hosted API and users connect with the web app or desktop app.

The core use case is:

- a manager, dispatcher or team lead creates tasks on behalf of employees
- assigned users see new tasks appear on their board
- users get an in-app notification and optional live update when a task is assigned
- the organization can self-host the API and database on its own network
- identity can integrate with Microsoft Entra ID/Active Directory, with other OIDC providers evaluated only when a real deployment needs them

This is different from the personal MVP. It should be an extension around the same domain/application/API rules, not a separate product code path.

## Possible Shape

```text
Org-hosted DumpTether API
  -> PostgreSQL
  -> reverse proxy / WAF
  -> OIDC/AD identity provider
  -> web, desktop and mobile clients
```

Teams features should build on:

- users and sessions
- workspace membership
- task-level sharing
- role boundaries: Owner, Member, ReadOnly/Guest
- SignalR live updates
- sync mappings for desktop clients

Likely new concepts:

- organization/tenant
- assignment/audience records
- task source/origin metadata
- notification inbox
- delegated task creation permission
- optional required acknowledgement

## Client Behavior

The web app and desktop app should still point at an API base URL.

- Personal web: browser talks to the hosted public API.
- Personal desktop: desktop talks to local sidecar and optionally syncs with the hosted API.
- Enterprise web: browser talks to the organization API.
- Enterprise desktop: desktop talks to local sidecar and syncs with the organization API after login.

The desktop app should not silently accept externally assigned tasks while offline. It should fetch assignments after the cloud login/session succeeds, then show a clear notification if new tasks arrived.

## Self-Hosting

Self-hosting should use the existing production shape:

- API container
- PostgreSQL container or managed PostgreSQL
- reverse proxy such as Caddy/nginx
- private network or DMZ depending on organization policy
- no public PostgreSQL port
- secrets stored on the server, not in Git

For internal-only deployments, the reverse proxy may terminate TLS on an internal hostname. For internet-facing deployments, put the reverse proxy behind Cloudflare or another WAF/rate-limit layer.

## Security Considerations

- Backend authorization remains authoritative.
- A manager creating a task on behalf of a user must have explicit delegated permission.
- Assignment APIs must be scoped to organization/workspace membership.
- Live updates must only notify users who can access the affected task.
- Desktop sync must re-check server access before caching shared/assigned tasks.
- Revoked access must remove shared/assigned visibility on next sync.
- Audit logs should record who assigned or delegated a task, but never log tokens or secrets.
- OIDC/AD configuration belongs in deployment config; provider secrets belong in environment/secret storage.

## Risks

- Over-expanding into an enterprise ticketing system could pull DumpTether away from its plain task-wall identity.
- Assignment and notification logic can become noisy if every update becomes a ceremony.
- Offline desktop conflict handling becomes more important when tasks are assigned by someone else.
- Self-hosted deployments need clear setup docs or they become support-heavy.

## MVP Boundary

Do not implement this until the personal web/offline/sync flow is stable.

Good first future slice:

1. Add organization/tenant concept.
2. Add assignment notification records.
3. Add API endpoint to assign an existing task to another workspace member.
4. Add SignalR event for assigned task.
5. Show a small badge in the sidebar/account area.
