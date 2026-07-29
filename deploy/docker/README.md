# DumpTether Docker Deployment

This folder contains example production Docker Compose files. They are templates only.

Real production files live on the server:

- `.env.prod`
- `docker-compose.prod.yml`
- optionally a real `Caddyfile`

Do not commit those real files.

## Files

- `docker-compose.prod.example.yml`: web, API, PostgreSQL, and Caddy reverse proxy.
- `.env.prod.example`: placeholder production environment values.
- `Caddyfile.example`: placeholder reverse proxy config.

## Production Shape

- Caddy publishes ports `80` and `443`.
- Caddy serves the React web image and routes `/api/*` plus health checks to the API.
- API listens internally on Docker port `8080`.
- The API runs as a non-root user.
- PostgreSQL listens only on the Docker network and does not publish `5432`.
- PostgreSQL data is stored in the `dumptether-postgres-data` named volume.
- Data Protection keys persist in the `dumptether-data-protection-keys` volume.
- Same-origin web/API traffic does not require CORS. Exact origins remain available
  for separately hosted browser or webview clients.
- `DUMPTETHER_DATABASE_PROVIDER` should stay `Postgres` for production compose.

## First Server Setup

```bash
cp .env.prod.example .env.prod
cp docker-compose.prod.example.yml docker-compose.prod.yml
```

Edit `.env.prod` on the server and replace all placeholders.

The production example defaults to invite-only registration and disables guest sessions:

```text
DUMPTETHER_ALLOW_GUEST_SESSIONS=false
DUMPTETHER_SIGNUP_MODE=InviteOnly
DUMPTETHER_SIGNUP_INVITE_CODE_0=<long private invite code>
```

Use `Whitelist` with `DUMPTETHER_SIGNUP_WHITELIST_EMAIL_0` or `DUMPTETHER_SIGNUP_WHITELIST_DOMAIN_0` if you would rather allow specific people/domains without sending invite codes. Do not use `Open` until email confirmation/OAuth and traffic protection are ready.

Choose one email provider with `DUMPTETHER_EMAIL_PROVIDER=None`, `Smtp`, or
`BrevoApi`. If email confirmation, Microsoft login, or email MFA is enabled,
the API validates the matching `.env.prod` values at startup and fails with a
clear missing-key error. Keep SMTP passwords, Brevo API keys, and the Microsoft
client secret only in the server `.env.prod` or a secret store. Mailpit is for
local capture and is not part of production Compose.

Set `DUMPTETHER_CORS_ALLOWED_ORIGIN_0` to the exact browser origin that is allowed to call the API, for example `https://dumptether.example.com`. Do not use `*`.

Run:

```bash
docker compose --env-file .env.prod -f docker-compose.prod.yml up -d
```

View logs:

```bash
docker compose --env-file .env.prod -f docker-compose.prod.yml logs -f api
docker compose --env-file .env.prod -f docker-compose.prod.yml logs -f reverse-proxy
```

External monitoring should call:

```text
https://your-domain.example/health/live
https://your-domain.example/health/ready
```

`live` checks that the API process can respond. `ready` also checks that the
database is reachable. The monitor must run outside the API process, preferably
outside the VPS, if it is expected to alert when the server is down.

Restart only the API:

```bash
docker compose --env-file .env.prod -f docker-compose.prod.yml restart api
```

Pull a new API image and recreate only the API:

```bash
docker compose --env-file .env.prod -f docker-compose.prod.yml pull api
docker compose --env-file .env.prod -f docker-compose.prod.yml up -d api
```

PostgreSQL is intentionally not exposed publicly. Do not add a `ports:` entry to the production PostgreSQL service unless you fully understand the network exposure and firewall rules.

Mailpit is intentionally absent. It is a development inbox, not a production
mail server. Use a transactional SMTP/API provider for confirmation and security
email.
