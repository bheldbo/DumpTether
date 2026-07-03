# Docker Compose Production Deployment

This document describes the intended production direction for the current DumpTether Docker setup. It is not automatic deployment yet.

## Build the API Image Locally

From the repository root:

```powershell
docker build -f src/DumpTether.Api/Dockerfile -t dumptether-api:local .
```

The Dockerfile is multi-stage:

- SDK image restores and publishes `DumpTether.Api` in `Release`.
- ASP.NET Core runtime image runs the published output.
- Runtime configuration is supplied through environment variables.
- `.env` files and local secrets are excluded by `.dockerignore`.

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
DUMPTETHER_DOMAIN=dumptether.example.com
```

Production secrets are never committed. Real production files stay on the server or in a deployment secret store.

Production compose is intended to use PostgreSQL. SQLite mode is for the local/offline runtime and future desktop app, not the hosted server.

For early private hosting, keep registration invite-only or whitelist-based. The API supports `DUMPTETHER_SIGNUP_MODE=Open`, `Whitelist`, `InviteOnly`, or `Closed`. `Whitelist` uses `DUMPTETHER_SIGNUP_WHITELIST_EMAIL_0` and/or `DUMPTETHER_SIGNUP_WHITELIST_DOMAIN_0`. `InviteOnly` uses `DUMPTETHER_SIGNUP_INVITE_CODE_0`. Do not commit real invite codes.

## CORS and Origins

If the React frontend and API are served from the same public origin through the reverse proxy, normal browser requests do not need cross-origin access. Keep CORS boring.

If the frontend is served from a different origin, set exact allowed origins with environment variables such as:

```text
DUMPTETHER_CORS_ALLOWED_ORIGIN_0=https://dumptether.example.com
```

The API maps that to `Cors:AllowedOrigins:0` and rejects wildcard origins. Use an origin only, not a path.

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

## GitHub Actions Direction

Current PR checks can build the Docker image without pushing it.

Later, GitHub Actions can:

1. Build the API image.
2. Tag it with the commit SHA and/or release version.
3. Push it to GHCR.
4. The server pulls the new image.
5. The server runs `docker compose --env-file .env.prod -f docker-compose.prod.yml up -d`.

Do not add automatic deployment or GHCR pushes until the registry, secrets, and server update process are explicit.

## Configuration Categories

Deployment/runtime config:

- connection strings
- auth signing keys or session settings
- email confirmation settings
- SMTP/API email provider credentials
- OAuth provider client IDs and secrets
- email MFA settings
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

The API validates feature config at startup. If email confirmation, SMTP email, Brevo API email, OAuth, or email MFA is enabled without its required settings, startup fails with `DumpTether configuration is incomplete` and lists the missing keys.

Brevo API values belong in the real server `.env.prod` only:

- `DUMPTETHER_EMAIL_CONFIRMATION_ENABLED=true`
- `DUMPTETHER_EMAIL_BREVO_API_ENABLED=true`
- `DUMPTETHER_EMAIL_BREVO_API_KEY=...`
- `DUMPTETHER_EMAIL_FROM=noreply@your-domain.example`
- `DUMPTETHER_EMAIL_CONFIRMATION_PUBLIC_BASE_URL=https://your-domain.example`

SMTP settings are kept as placeholders for future fallback/support, but email confirmation currently uses the Brevo transactional email API. Never commit the real SMTP username, SMTP password, Brevo API key, or OAuth client secrets.
