# Ubuntu VPS Production Runbook

This guide deploys one self-hosted DumpTether instance on an Ubuntu VPS. It is
written so another operator can repeat the setup with a different domain.

The resulting runtime is:

```text
Internet
  -> Cloudflare proxy
  -> VPS ports 80/443
  -> Caddy reverse proxy
     -> React web container
     -> ASP.NET Core API/SignalR container
        -> private PostgreSQL container + persistent volume
```

PostgreSQL, API port `8080`, and any development mail UI remain private. The
desktop app is a separate installer: it works against local SQLite and uses the
configured public HTTPS origin only for optional login and synchronization.

## 1. Collect The Required Values

Prepare these before editing the server:

- a Linux VPS with a public IPv4 address
- a domain or subdomain, such as `dumptether.example.com`
- a GitHub repository/package owner
- a long random PostgreSQL password
- a transactional email provider API key or SMTP account
- an Entra application client ID and client-secret value if Microsoft login is enabled
- the exact Microsoft redirect URI:
  `https://dumptether.example.com/api/auth/oauth/microsoft/callback`
- a real service-operator name and monitored privacy contact address

Never put provider secrets in GitHub, a Dockerfile, a client target JSON, or an
`appsettings` file. Rotate any secret that has been exposed in chat, logs, shell
history, or source control.

## 2. Establish Key-Only SSH

Run local Windows commands from PowerShell, not from the remote Linux prompt:

```powershell
ssh-keygen -t ed25519 -f "$env:USERPROFILE\.ssh\dumptether-vps" -C "dumptether-vps"
scp "$env:USERPROFILE\.ssh\dumptether-vps.pub" administrator@<VPS_IP>:/tmp/dumptether-vps.pub
ssh administrator@<VPS_IP>
```

On the VPS:

```bash
umask 077
mkdir -p ~/.ssh
cat /tmp/dumptether-vps.pub >> ~/.ssh/authorized_keys
chmod 700 ~/.ssh
chmod 600 ~/.ssh/authorized_keys
rm /tmp/dumptether-vps.pub
exit
```

Confirm the key works from a new local terminal before disabling passwords:

```powershell
ssh -i "$env:USERPROFILE\.ssh\dumptether-vps" administrator@<VPS_IP>
```

Then add `/etc/ssh/sshd_config.d/99-dumptether-hardening.conf` on the VPS:

```text
PubkeyAuthentication yes
PasswordAuthentication no
KbdInteractiveAuthentication no
PermitRootLogin no
```

Validate and reload without closing the known-good session:

```bash
sudo sshd -t
sudo systemctl reload ssh
```

Open a second terminal and verify key login again before ending the first one.

## 3. Configure The Provider Firewall

Allow inbound:

| Port | Source | Purpose |
| --- | --- | --- |
| `22/tcp` | the operator's current public IP `/32` | SSH |
| `80/tcp` | Internet | HTTP and certificate validation |
| `443/tcp` | Internet | HTTPS |

Apply equivalent IPv6 rules if IPv6 is enabled. Do not expose `5432`, `8080`,
`8025`, or `1025`. Docker-published ports can bypass uncomplicated host-firewall
expectations, so the Compose file intentionally publishes only Caddy's `80/443`.

## 4. Update Ubuntu And Install Docker

Use Docker's official apt repository rather than the convenience script for a
production host. Follow the current official instructions at
<https://docs.docker.com/engine/install/ubuntu/>. The package set should include:

```bash
sudo apt install docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
sudo systemctl enable --now docker
sudo docker run --rm hello-world
sudo docker compose version
```

The `docker` group is effectively root-equivalent. Keeping deployment commands
behind `sudo` is a reasonable small-server default.

## 5. Configure DNS And Cloudflare

Create a proxied `A` record:

```text
Type: A
Name: dumptether
Content: <VPS_IP>
Proxy status: Proxied
TTL: Auto
```

Cloudflare explains proxied versus DNS-only records at
<https://developers.cloudflare.com/dns/proxy-status/>. Set SSL/TLS encryption to
`Full (strict)` after Caddy has a valid origin certificate. Cloudflare documents
that mode at
<https://developers.cloudflare.com/ssl/origin-configuration/ssl-modes/full-strict/>.

If initial certificate issuance is troublesome, temporarily use DNS-only,
confirm Caddy can issue the certificate, then enable the proxy and Full (strict).
Do not leave the origin in Cloudflare `Flexible` mode.

## 6. Create The Server Deployment Directory

On the VPS:

```bash
sudo install -d -m 0750 -o root -g root /opt/dumptether
cd /opt/dumptether
```

Copy these repository examples to the server using `scp`, a release artifact,
or a tightly scoped deployment process:

```text
deploy/docker/docker-compose.prod.example.yml -> /opt/dumptether/docker-compose.prod.yml
deploy/docker/Caddyfile.example                -> /opt/dumptether/Caddyfile.example
deploy/docker/.env.prod.example                -> /opt/dumptether/.env.prod
```

Protect the real environment file:

```bash
sudo chown root:root /opt/dumptether/.env.prod
sudo chmod 600 /opt/dumptether/.env.prod
```

Real production files remain on the server and are not committed.

## 7. Configure The Production Environment

Use `/opt/dumptether/.env.prod` as the production runtime source of truth. At a
minimum, review:

```text
POSTGRES_DB=dumptether
POSTGRES_USER=dumptether
POSTGRES_PASSWORD=<long-random-secret>

DUMPTETHER_API_IMAGE=ghcr.io/<owner>/dumptether-api:<immutable-tag>
DUMPTETHER_WEB_IMAGE=ghcr.io/<owner>/dumptether-web:<same-release-tag>
DUMPTETHER_DATABASE_PROVIDER=Postgres
DUMPTETHER_APPLY_MIGRATIONS_ON_STARTUP=false
DUMPTETHER_REQUIRE_AUTHENTICATION=true
DUMPTETHER_ALLOW_GUEST_SESSIONS=false
DUMPTETHER_SIGNUP_MODE=Open
DUMPTETHER_ENABLE_DEVELOPMENT_LOGIN=false
DUMPTETHER_ENABLE_LOCAL_DESKTOP_LOGIN=false

DUMPTETHER_CORS_ALLOWED_ORIGIN_0=https://dumptether.example.com
DUMPTETHER_DOMAIN=dumptether.example.com

DUMPTETHER_EMAIL_CONFIRMATION_ENABLED=true
DUMPTETHER_EMAIL_CONFIRMATION_PUBLIC_BASE_URL=https://dumptether.example.com
DUMPTETHER_EMAIL_PROVIDER=BrevoApi
DUMPTETHER_EMAIL_FROM=noreply@notify.example.com
DUMPTETHER_EMAIL_BREVO_API_KEY=<server-secret>
DUMPTETHER_EMAIL_MFA_ENABLED=false

DUMPTETHER_OAUTH_MICROSOFT_ENABLED=true
DUMPTETHER_OAUTH_MICROSOFT_CLIENT_ID=<application-client-id>
DUMPTETHER_OAUTH_MICROSOFT_CLIENT_SECRET=<client-secret-value>
DUMPTETHER_OAUTH_MICROSOFT_TENANT_ID=common

DUMPTETHER_LEGAL_REQUIRE_ACCEPTANCE=true
DUMPTETHER_LEGAL_TERMS_VERSION=<published-version>
DUMPTETHER_LEGAL_PRIVACY_NOTICE_VERSION=<published-version>
DUMPTETHER_LEGAL_OPERATOR_NAME=<operator>
DUMPTETHER_LEGAL_PRIVACY_CONTACT_EMAIL=<monitored-address>
```

`Email MFA` is reserved but not implemented and must remain `false`; the API
rejects `true` at startup rather than claiming protection it does not provide.
Use `InviteOnly` or `Whitelist` until open signup, email confirmation, OAuth,
rate limits, legal display, and abuse controls have been tested through the
public origin.

ASP.NET does not parse `.env.prod` directly. Docker Compose reads it, then maps
each value to hierarchical ASP.NET environment keys such as
`Auth__SignupMode`. ASP.NET environment variables override committed
`appsettings*.json`, so secrets are configured once on the server.

## 8. Understand The Published Artifacts

The hosted release workflow publishes two OCI images:

- `dumptether-api`: ASP.NET Core API, authorization, SignalR, use cases, and EF Core
- `dumptether-web`: compiled React assets served by a small Caddy runtime

The VPS pulls both from GHCR. The web package may mention Go if it inherited
Caddy's image description; DumpTether is React/TypeScript and C#. Current
Dockerfiles override that inherited OCI metadata.

GitHub documents GHCR pulls at
<https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-container-registry>.
Public packages need no registry login. For private packages, use a narrowly
scoped read token and never store it in the repository.

Desktop installers are not container images. The separate Desktop Release
workflow builds Windows and Linux installers, embeds a non-secret default cloud
origin from `deploy/targets/<target>.json`, and attaches artifacts to a GitHub
Release. Desktop business data still lives locally in SQLite until the user
chooses to sign in and synchronize.

## 9. Validate, Pull, Migrate, And Start

From `/opt/dumptether`:

```bash
sudo docker compose --env-file .env.prod -f docker-compose.prod.yml config --quiet
sudo docker compose --env-file .env.prod -f docker-compose.prod.yml pull
```

For an empty database, deliberately set
`DUMPTETHER_APPLY_MIGRATIONS_ON_STARTUP=true`, start the stack once, and inspect
API logs. Then restore the value to `false` and recreate the API. For every later
schema release: back up first, review the migration, deliberately enable/apply
it, verify, and disable automatic startup migration again.

```bash
sudo docker compose --env-file .env.prod -f docker-compose.prod.yml up -d
sudo docker compose --env-file .env.prod -f docker-compose.prod.yml ps
sudo docker compose --env-file .env.prod -f docker-compose.prod.yml logs --tail=200 api
```

Verify from outside the VPS:

```text
https://dumptether.example.com/health/live
https://dumptether.example.com/health/ready
https://dumptether.example.com/api/auth/options
```

Also test registration, email delivery, confirmation-link reuse/expiry, password
login, logout, Microsoft login/callback, legal-document scrolling on a phone,
and one write/read cycle before calling the rollout healthy.

## 10. Routine Rollout And Rollback

Prefer an immutable version or `sha-...` image tag over `latest`.

Before rollout:

1. create and verify a PostgreSQL backup
2. copy the current `.env.prod` to a protected rollback directory
3. put matching API/web image tags in `.env.prod`
4. validate Compose
5. pull and recreate
6. check health and logs

```bash
sudo docker compose --env-file .env.prod -f docker-compose.prod.yml pull api web
sudo docker compose --env-file .env.prod -f docker-compose.prod.yml up -d api web
```

To roll back application code, restore the previous image tags and repeat the
pull/up commands. Database rollback is separate and riskier: restore only when
the reviewed schema/application compatibility requires it.

## 11. Automated Backups

Create `/usr/local/sbin/dumptether-backup` as root:

```bash
#!/usr/bin/env bash
set -euo pipefail

cd /opt/dumptether
umask 077
backup_dir=/var/backups/dumptether
stamp=$(date -u +%Y%m%dT%H%M%SZ)
tmp="$backup_dir/.dumptether-$stamp.dump"
final="$backup_dir/dumptether-$stamp.dump"

install -d -m 0700 "$backup_dir"
docker compose --env-file .env.prod -f docker-compose.prod.yml exec -T postgres \
  sh -c 'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" --format=custom' > "$tmp"
test -s "$tmp"
mv "$tmp" "$final"
find "$backup_dir" -type f -name 'dumptether-*.dump' -mtime +14 -delete
```

Then:

```bash
sudo chown root:root /usr/local/sbin/dumptether-backup
sudo chmod 700 /usr/local/sbin/dumptether-backup
```

Create `/etc/systemd/system/dumptether-backup.service`:

```ini
[Unit]
Description=DumpTether PostgreSQL backup
Requires=docker.service
After=docker.service

[Service]
Type=oneshot
ExecStart=/usr/local/sbin/dumptether-backup
```

Create `/etc/systemd/system/dumptether-backup.timer`:

```ini
[Unit]
Description=Run DumpTether backup nightly

[Timer]
OnCalendar=*-*-* 02:15:00
Persistent=true
RandomizedDelaySec=15m

[Install]
WantedBy=timers.target
```

Enable and test:

```bash
sudo systemctl daemon-reload
sudo systemctl enable --now dumptether-backup.timer
sudo systemctl start dumptether-backup.service
sudo systemctl status dumptether-backup.service
sudo systemctl list-timers dumptether-backup.timer
sudo ls -lh /var/backups/dumptether
```

The PostgreSQL dump is only one recovery asset. Also back up the
`dumptether-data-protection-keys` Docker volume because losing those keys can
invalidate protected session material. Record the actual Compose-prefixed
volume name from `docker volume ls` rather than assuming it.

## 12. Off-Host Backup And Restore Drills

Copy each verified backup to a different failure domain:

- a second server reached with a restricted backup-only SSH key
- encrypted object storage through a tool such as `rclone`
- a provider backup product with documented retention and restore behavior

Use encryption in transit and at rest. Protect dumps as personal data. Monitor
the copy job and alert when the newest off-host backup is too old. A VPS snapshot
is useful before upgrades, but it does not replace database-level backups and
may share the provider's failure domain.

At least monthly, restore the newest backup into an isolated test PostgreSQL
database and verify task/user counts plus an application smoke test. Do not make
the production server your first restore attempt.

A production restore normally means stopping API writes, preserving the failed
database for investigation, restoring with `pg_restore`, applying only compatible
migrations, then verifying readiness before reopening traffic. Write down the
exact tested restore command for the PostgreSQL image/version in use.

## 13. Automatic Restart And Monitoring

The production Compose services use `restart: unless-stopped`. With Docker
enabled through systemd, containers return after a host reboot unless an
operator explicitly stopped them.

Useful checks:

```bash
sudo systemctl status docker
sudo docker compose --env-file /opt/dumptether/.env.prod \
  -f /opt/dumptether/docker-compose.prod.yml ps
sudo journalctl -u docker --since today
```

Use an external monitor for both `/health/live` and `/health/ready`. The monitor
must live outside the VPS if it should alert when the VPS or network is gone.
Route alerts to an independent address/provider; an unavailable DumpTether API
cannot reliably report its own outage.

## 14. Security Checklist

- SSH key login works and password/root login is disabled.
- SSH is restricted to an operator IP or VPN.
- Only `80/443` are public application ports.
- PostgreSQL and API ports are not published.
- Cloudflare proxy is enabled and TLS is Full (strict).
- Production uses exact `AllowedHosts`/CORS origins, never credentialed `*`.
- Guest, development, and local-desktop login are disabled on the hosted server.
- Real `.env.prod` is mode `600` and absent from Git/source/images.
- Email confirmation and OAuth are tested through the public HTTPS origin.
- Rate limits and signup mode match the intended audience.
- Backups are recent, off-host, encrypted, monitored, and restore-tested.
- Images use reviewed immutable tags and are updated regularly.
- Ubuntu, Docker, Caddy, PostgreSQL, and base images receive security updates.
