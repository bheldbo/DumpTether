# Server operator administration

DumpTether keeps server administration outside the public web, desktop and
future mobile clients. Product roles such as Owner, Member and Guest only
control boards and tasks. They never grant server-operator access.

The production API image includes an SSH-only command-line tool at
`/tools/admin`. It uses DumpTether application services and writes audit events
for privileged changes. It does not expose a network port or print password,
session-token or refresh-token hashes.

## Start safely

Connect to the VPS using your SSH key, then enter the deployment directory:

```powershell
ssh -i "$env:USERPROFILE\.ssh\dumptether-vps" administrator@81.88.19.192
```

```bash
cd /opt/dumptether
sudo docker compose --env-file .env.prod -f docker-compose.prod.yml ps
```

Create a PostgreSQL backup before destructive account work:

```bash
sudo mkdir -p /var/backups/dumptether
sudo docker compose --env-file .env.prod -f docker-compose.prod.yml exec -T postgres \
  pg_dump -U dumptether -d dumptether -Fc \
  > /var/backups/dumptether/dumptether-$(date -u +%Y%m%dT%H%M%SZ).dump
```

## Installation statistics

```bash
sudo docker compose --env-file .env.prod -f docker-compose.prod.yml exec -T api \
  dotnet /tools/admin/DumpTether.Admin.dll stats
```

The result includes registered, active and confirmed accounts, active sessions,
sessions seen in the previous 15 minutes, boards and task counts. A recently
seen session is only an activity approximation. It is not proof that a browser
or desktop client is currently connected. A true online-user count belongs in
future SignalR/metrics telemetry.

## Find and inspect an account

```bash
sudo docker compose --env-file .env.prod -f docker-compose.prod.yml exec -T api \
  dotnet /tools/admin/DumpTether.Admin.dll users list --limit 100

sudo docker compose --env-file .env.prod -f docker-compose.prod.yml exec -T api \
  dotnet /tools/admin/DumpTether.Admin.dll users list --search frederikke

sudo docker compose --env-file .env.prod -f docker-compose.prod.yml exec -T api \
  dotnet /tools/admin/DumpTether.Admin.dll users show person@example.com
```

`users show` displays account state, confirmation state, board counts and up to
50 recent sessions. It intentionally does not display private task or note
content.

## Lock, unlock or sign out an account

Every mutation requires a named operator and a reason. Locking an account also
revokes its active sessions.

```bash
sudo docker compose --env-file .env.prod -f docker-compose.prod.yml exec -T api \
  dotnet /tools/admin/DumpTether.Admin.dll users lock person@example.com \
  --actor bjarke --reason "Account owner requested a temporary lock"

sudo docker compose --env-file .env.prod -f docker-compose.prod.yml exec -T api \
  dotnet /tools/admin/DumpTether.Admin.dll users unlock person@example.com \
  --actor bjarke --reason "Identity verified by the account owner"

sudo docker compose --env-file .env.prod -f docker-compose.prod.yml exec -T api \
  dotnet /tools/admin/DumpTether.Admin.dll sessions revoke person@example.com \
  --actor bjarke --reason "Sign out all devices after a security report"
```

## Delete an account

Account deletion is irreversible. Verify the request, back up PostgreSQL, then
type the exact target email twice:

```bash
sudo docker compose --env-file .env.prod -f docker-compose.prod.yml exec -T api \
  dotnet /tools/admin/DumpTether.Admin.dll users delete person@example.com \
  --confirm person@example.com --actor bjarke \
  --reason "Verified account deletion request"
```

The command deletes owned boards, task history, sessions and shares. A template
still referenced by surviving shared data is preserved without an owner so the
remaining task can still render.

## Password and account recovery

Operators cannot read or assign passwords. That is intentional: manually
assigning a password creates a credential-delivery problem and bypasses the
account owner. An operator can instead send the same one-hour, single-use reset
link used by self-service recovery:

```bash
sudo docker compose --env-file .env.prod -f docker-compose.prod.yml exec -T api \
  dotnet /tools/admin/DumpTether.Admin.dll users send-password-reset person@example.com \
  --actor bjarke --reason "Identity verified and recovery requested"
```

The command only works for active, confirmed email/password accounts, uses the
configured transactional email provider, and writes a
`password_reset.requested` operator audit event. Microsoft-only accounts recover
through Microsoft. If a device or token may be compromised, revoke sessions or
lock the account separately.

Self-service deletion is scheduled 48 hours ahead and can be cancelled from the
Account panel until the worker claims it. A reminder is sent about 24 hours
before deletion. The API refuses self-service deletion while an owned board has
members, pending invitations, or task shares; those relationships must be
removed first. The immediate `users delete` command remains an exceptional,
irreversible operator path.

## GDPR access and export requests

The CLI currently supports account discovery and deletion, but not a complete
portable personal-data export. Do not improvise a raw production database dump
as the normal response: it can contain other people's shared data and secrets.

Until the application-level export command is implemented, handle an access
request as a documented operator procedure with identity verification and a
purpose-built, reviewed query. The planned export must include the requesting
user's account metadata, memberships, owned boards, tasks, entries, shares and
legal acceptances while excluding unrelated users' private data. Record the
request and delivery outside DumpTether and transfer the result securely.

## Audit and database access

Mutations are written to `operator_audit_events`. Prefer the CLI over direct SQL.
If pgAdmin is needed for diagnostics, use an SSH tunnel from your own machine;
never publish PostgreSQL port 5432 to the internet and do not install a desktop
environment on the VPS merely to inspect the database.

The future graphical admin surface, if built, should bind to loopback only and
be reached through an SSH tunnel or private VPN. It still needs its own operator
authentication, short sessions, CSRF protection and audit trail.

