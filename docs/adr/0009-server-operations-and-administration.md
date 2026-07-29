# ADR 0009: Server operations and administration

## Status

Accepted as the direction for future operator tooling.

## Context

Workspace Owner, Member and Guest roles govern product data. They are not
server-operator roles. A server operator can affect every account, session and
deployment on one hosted DumpTether installation, so exposing those actions in
the ordinary web, desktop or mobile client would enlarge the public attack
surface and blur two different authorization boundaries.

DumpTether also needs deployment health and security notifications. The API
cannot reliably report that it has stopped, because a stopped process cannot
send its own alert.

## Decision

The first administration surface will be a separate server-side operations CLI:

```text
SSH key or private VPN
  -> restricted operating-system account
  -> DumpTether operations CLI or one-shot container
  -> application-level operator use cases
  -> PostgreSQL
```

The CLI will not publish a network port and will not be bundled into ordinary
clients. It will reuse application services instead of editing database tables
directly. `DumpTether.Database` remains the migrations and maintenance tool;
account and session operations belong in a separate operations host.

A future graphical admin host may bind to loopback only and be reached through
an SSH tunnel or private VPN. Network location is an extra boundary, not a
replacement for operator authentication, short sessions, CSRF protection and
authorization.

Operators see account, session, usage and deployment metadata by default, not
private task or note content. Locking an account revokes its sessions and live
connections. Every privileged read or mutation is recorded in an append-only
operator audit log with actor, action, target, time and outcome. Passwords,
session tokens, secrets and unrestricted raw network history are never logged.

Service availability is monitored externally against `/health/live` and
`/health/ready`. The external monitor may send email or another alert when the
API or VPS is unavailable. Administrative security events may use the normal
transactional email provider while the API is running.

High-risk operator mutations may notify a configured security address after the
audit record is committed. Notification failure must not erase or roll back the
audit evidence, and alert delivery credentials remain server-side secrets.

## Consequences

- There is no `Admin` workspace role.
- Server administration is unavailable from public product clients.
- SSH/VPN and operating-system hardening remain part of the deployment model.
- A compromised product account does not automatically gain an administration
  surface.
- Operations tooling can be added without duplicating domain rules or granting
  direct database access to a browser.

## Non-goals

- No administrator GUI is implemented by this decision.
- No task-content inspection is granted to operators.
- No assumption is made that one VPS can monitor its own complete failure.
