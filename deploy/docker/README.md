# DumpTether Docker Deployment

This folder contains example production Docker Compose files. They are templates only.

Real production files live on the server:

- `.env.prod`
- `docker-compose.prod.yml`
- optionally a real `Caddyfile`

Do not commit those real files.

## Files

- `docker-compose.prod.example.yml`: API, PostgreSQL, and Caddy reverse proxy.
- `.env.prod.example`: placeholder production environment values.
- `Caddyfile.example`: placeholder reverse proxy config.

## Production Shape

- Caddy publishes ports `80` and `443`.
- API listens internally on Docker port `8080`.
- PostgreSQL listens only on the Docker network and does not publish `5432`.
- PostgreSQL data is stored in the `dumptether-postgres-data` named volume.
- CORS uses exact allowed origins from `.env.prod` when the frontend is hosted separately.

## First Server Setup

```bash
cp .env.prod.example .env.prod
cp docker-compose.prod.example.yml docker-compose.prod.yml
```

Edit `.env.prod` on the server and replace all placeholders.

If you enable email confirmation, SMTP, Brevo API email, OAuth, or email MFA, the API validates the matching `.env.prod` values at startup and fails with a clear missing-key error. Email confirmation currently uses the Brevo transactional API key path. Keep real SMTP passwords, Brevo API keys, and OAuth client secrets only in the server `.env.prod` or a secret store.

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
