# Docker Compose Production Deployment

This document describes the intended production direction for the current DumpTether Docker setup. It is not automatic deployment yet.

## Build the Production Images Locally

From the repository root:

```powershell
docker build -f src/DumpTether.Api/Dockerfile -t dumptether-api:local .
docker build -f apps/web/Dockerfile -t dumptether-web:local .
```

The Dockerfile is multi-stage:

- SDK image restores and publishes `DumpTether.Api` in `Release`.
- ASP.NET Core runtime image runs the published output.
- Runtime configuration is supplied through environment variables.
- `.env` files and local secrets are excluded by `.dockerignore`.
- The runtime image uses the built-in non-root `app` user.

The web image builds `apps/web` and serves its static output. In the production
Compose shape, Caddy sends `/api/*` and `/health*` to the API and everything else
to the web image. The web app therefore uses same-origin requests and the image
does not need a customer-specific API URL.

## Run Local Compose

PostgreSQL only, for Visual Studio or `dotnet run`:

```powershell
docker compose up -d
dotnet tool run dotnet-ef database update --project src/DumpTether.Data --startup-project src/DumpTether.Data
dotnet run --project src/DumpTether.Api --launch-profile DumpTether.Api
```

PostgreSQL plus Dockerized API:

```powershell
Copy-Item .env.local.example .env
docker compose -f docker-compose.local.yml up --build
```

Then run the frontend locally:

```powershell
cd apps/web
npm.cmd run dev
```

## Create Production Environment Files

On the server, copy the production examples:

```bash
cd deploy/docker
cp .env.prod.example .env.prod
cp docker-compose.prod.example.yml docker-compose.prod.yml
```

Edit `.env.prod` on the server:

```text
POSTGRES_DB=dumptether
POSTGRES_USER=dumptether
POSTGRES_PASSWORD=<long random secret>
DUMPTETHER_API_IMAGE=<registry>/<image>:<tag>
DUMPTETHER_WEB_IMAGE=<registry>/<web-image>:<tag>
DUMPTETHER_DATABASE_PROVIDER=Postgres
DUMPTETHER_APPLY_MIGRATIONS_ON_STARTUP=false
DUMPTETHER_REQUIRE_AUTHENTICATION=true
DUMPTETHER_ALLOW_GUEST_SESSIONS=false
DUMPTETHER_SIGNUP_MODE=InviteOnly
DUMPTETHER_SIGNUP_INVITE_CODE_0=<long private invite code>
DUMPTETHER_ENABLE_DEVELOPMENT_LOGIN=false
DUMPTETHER_MAX_ACTIVE_TASKS_PER_WORKSPACE=1000
DUMPTETHER_MAX_TOTAL_TASKS_PER_WORKSPACE=5000
DUMPTETHER_CORS_ALLOWED_ORIGIN_0=https://dumptether.example.com
DUMPTETHER_LEGAL_REQUIRE_ACCEPTANCE=true
DUMPTETHER_LEGAL_TERMS_VERSION=2026-08-06
DUMPTETHER_LEGAL_PRIVACY_NOTICE_VERSION=2026-08-06
DUMPTETHER_LEGAL_OPERATOR_NAME=<real legal operator name>
DUMPTETHER_LEGAL_PRIVACY_CONTACT_EMAIL=<monitored privacy address>
DUMPTETHER_DOMAIN=dumptether.example.com
```

Production secrets are never committed. Real production files stay on the server or in a deployment secret store.

Production compose is intended to use PostgreSQL. SQLite mode is for the local/offline runtime and future desktop app, not the hosted server.

Do not switch `DUMPTETHER_SIGNUP_MODE` to `Open` merely because DNS and email
credentials exist. First verify a real registration, Brevo delivery, confirmation
link, login, logout, Microsoft callback, rate limiting, and legal document display
through the public HTTPS origin. Keep guest sessions disabled on the hosted API.

For early private hosting, keep registration invite-only or whitelist-based. The API supports `DUMPTETHER_SIGNUP_MODE=Open`, `Whitelist`, `InviteOnly`, or `Closed`. `Whitelist` uses `DUMPTETHER_SIGNUP_WHITELIST_EMAIL_0` and/or `DUMPTETHER_SIGNUP_WHITELIST_DOMAIN_0`. `InviteOnly` uses `DUMPTETHER_SIGNUP_INVITE_CODE_0`. Do not commit real invite codes.

## CORS and Origins

If the React frontend and API are served from the same public origin through the reverse proxy, normal browser requests do not need cross-origin access. Keep CORS boring.

If the frontend is served from a different origin, set exact allowed origins with environment variables such as:

```text
DUMPTETHER_CORS_ALLOWED_ORIGIN_0=https://dumptether.example.com
```

The API maps that to `Cors:AllowedOrigins:0` and rejects wildcard origins. Use an origin only, not a path.

Production Compose enables trusted forwarded headers because Caddy is the only
peer on the dedicated `proxy-api` network and its private Docker address is
dynamic. Caddy gets public/ACME access through `public`; the API gets provider
access through `api-egress`; proxy traffic, static web and PostgreSQL each use
separate internal networks. Do not publish API port `8080` or attach unrelated
services to `proxy-api` while that trust mode is enabled.

Data Protection keys are mounted from a persistent named volume. Losing those
keys can invalidate protected cloud-session material even when PostgreSQL is
intact, so include that volume in the server recovery plan.

## Start Production Compose

```bash
docker compose --env-file .env.prod -f docker-compose.prod.yml up -d
```

View logs:

```bash
docker compose --env-file .env.prod -f docker-compose.prod.yml logs -f
docker compose --env-file .env.prod -f docker-compose.prod.yml logs -f api
docker compose --env-file .env.prod -f docker-compose.prod.yml logs -f postgres
docker compose --env-file .env.prod -f docker-compose.prod.yml logs -f reverse-proxy
```

Restart only the API:

```bash
docker compose --env-file .env.prod -f docker-compose.prod.yml restart api
```

Pull a new API image and recreate the API:

```bash
docker compose --env-file .env.prod -f docker-compose.prod.yml pull api
docker compose --env-file .env.prod -f docker-compose.prod.yml up -d api
```

## PostgreSQL Backup

Create a compressed logical backup from the PostgreSQL container:

```bash
docker compose --env-file .env.prod -f docker-compose.prod.yml exec -T postgres \
  sh -c 'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" --format=custom' \
  > dumptether-$(date +%Y%m%d-%H%M%S).dump
```

For Windows PowerShell against a local Docker host:

```powershell
docker compose --env-file .env.prod -f docker-compose.prod.yml exec -T postgres `
  sh -c 'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" --format=custom' `
  > dumptether-backup.dump
```

Keep backups outside the repository and protect them like production data.

## Avoid Exposing PostgreSQL

The production compose example intentionally omits `ports:` from `postgres`. That means PostgreSQL is reachable by the API over the Docker network as `postgres:5432`, but it is not published to the public network.

Do not add:

```yaml
ports:
  - "5432:5432"
```

to production PostgreSQL unless you also add strict firewall and network controls.

The same rule applies to API port `8080`, Mailpit ports `1025/8025`, and any
future operations UI. Public inbound traffic should normally be limited to
`80/443`; restrict SSH to keys and preferably an allowlist or VPN. Apply the
same intent to IPv6 rules.

Docker-published ports have their own packet-filtering behavior. Verify the
effective Docker firewall rules rather than assuming a host firewall alone will
hide a published container port.

## Health And Monitoring

Use:

```text
/health/live
/health/ready
```

`live` is process liveness. `ready` also calls the configured database. Readiness
results are cached briefly and both endpoints are rate-limited, but monitoring
should still use a sensible interval such as 30-60 seconds. Configure an
external uptime monitor against both endpoints. A stopped API cannot email about
its own failure.

Administrative actions are a separate server-operator boundary described in
`docs/adr/0009-server-operations-and-administration.md`. The future first
surface is a CLI reached over SSH/VPN, not an Admin role in the public client.

## GitHub Actions Direction

Pull requests build both production images without pushing them.

`.github/workflows/hosted-release.yml` publishes matching API and web images to
GHCR for `v*` tags, or for an explicitly named manual preview. Each build gets
the requested version tag and an immutable `sha-...` tag. It uses the repository
`GITHUB_TOKEN`; no registry password is stored in DumpTether configuration.

The workflow deliberately does not deploy. On the server:

1. Put the selected immutable/versioned image tags in `.env.prod`.
2. Pull both images.
3. Back up PostgreSQL.
4. Apply reviewed migrations intentionally.
5. Run `docker compose --env-file .env.prod -f docker-compose.prod.yml up -d`.
6. Check `/health/live` and `/health/ready`.

If the GHCR packages are private, sign the server in with a narrowly scoped
token that can read packages:

```bash
echo "$GHCR_READ_TOKEN" | docker login ghcr.io -u <github-user> --password-stdin
```

Keep that token in the server secret store or root-owned environment, never in
Compose source or the repository. Public packages do not require registry login.

## Configuration Categories

Deployment/runtime config:

- connection strings
- auth signing keys or session settings
- email confirmation settings
- SMTP/API email provider credentials
- OAuth provider client IDs and secrets
- future email MFA settings (currently unsupported and disabled)
- cookie settings
- allowed origins/hosts
- reverse proxy settings
- storage paths

User/workspace config:

- templates
- views
- categories
- statuses
- colors
- archive reasons

Integration config:

- future email/calendar/AI/MCP provider settings
- non-secret provider metadata in the database
- credentials in environment variables or a secret store

## Config Validation

The API validates feature config at startup. If email confirmation, the selected
email provider, or Microsoft login is enabled without its required settings,
startup fails with `DumpTether configuration is incomplete` and lists the
missing keys. Email MFA does not have a completed challenge flow yet;
`DUMPTETHER_EMAIL_MFA_ENABLED` must remain `false` and startup rejects `true`.

Brevo API values belong in the real server `.env.prod` only:

- `DUMPTETHER_EMAIL_CONFIRMATION_ENABLED=true`
- `DUMPTETHER_EMAIL_PROVIDER=BrevoApi`
- `DUMPTETHER_EMAIL_BREVO_API_KEY=...`
- `DUMPTETHER_EMAIL_FROM=noreply@your-domain.example`
- `DUMPTETHER_EMAIL_CONFIRMATION_PUBLIC_BASE_URL=https://your-domain.example`

SMTP is also implemented. Select `DUMPTETHER_EMAIL_PROVIDER=Smtp`, configure
the SMTP host/TLS/authentication values, and keep credentials only in the real
server environment. Mailpit is a local development inbox, not a production
delivery service. Never commit SMTP passwords, Brevo API keys, or the Microsoft
client secret.
