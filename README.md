# DumpTether

DumpTether is a lightweight personal task-and-note system that turns messy working notes into a plain task wall with structured notes, templates, searchability and archive reasons.

This repository is the initial Minimal Viable Product (MVP) and my learning experience with agentic warfare.

## MVP Boundaries

- Everything will be modeled as a task.
- Tasks will have structured fields and compact note history.
- Templates, views, projects, and archive reasons are core concepts.
- The product should feel like a personal task wall, not a Jira clone, timeline-heavy audit system, or complex import/parser tool.
- AI, MCP, email, calendar, sharing, and desktop support are outside the MVP.

See `docs/product/product-principles.md` and `docs/product/ui-principles.md` for the product and task-wall UX direction.

## Repository Layout

```text
apps/web/                 React + TypeScript + Vite frontend
src/DumpTether.Api/       ASP.NET Core API host
src/DumpTether.App/       Application services and use cases
src/DumpTether.Data/      EF Core persistence
src/DumpTether.Domain/    Domain model and business rules
docs/adr/                 Architecture decision records
docs/deployment/          Deployment notes and examples
docs/security/            Security principles
deploy/docker/            Production Docker Compose examples
```

## Prerequisites

- .NET SDK 8 or later
- Node.js 24 LTS or later, with npm
- Docker Desktop or Docker Engine with Compose
- Git

## Getting Started

Restore local .NET tools:

```powershell
dotnet tool restore
```

Start PostgreSQL:

```powershell
docker compose up -d
```

The local Docker Compose database uses this development-only pattern:

```text
Host=localhost;Port=5432;Database=dumptether;Username=dumptether;Password=dumptether_dev_password
```

Visual Studio and `dotnet run --launch-profile DumpTether.Api` use the local Compose connection string from `src/DumpTether.Api/Properties/launchSettings.json`.

For terminal sessions that do not use the launch profile, configure the API through an environment variable:

```powershell
$env:ConnectionStrings__DumpTether = "Host=localhost;Port=5432;Database=dumptether;Username=dumptether;Password=dumptether_dev_password"
```

Alternatively, copy the shape from `appsettings.example.json` into an uncommitted `src/DumpTether.Api/appsettings.Development.json`. The example file is documentation only; do not put real secrets in source control.

Apply EF Core migrations:

```powershell
dotnet tool run dotnet-ef database update --project src/DumpTether.Data --startup-project src/DumpTether.Data
```

Run the API:

```powershell
dotnet run --project src/DumpTether.Api --launch-profile DumpTether.Api
```

Local and hosted requests now use the same user/session/workspace boundary. The API requires authentication by default; the remaining anonymous workspace path is only an explicit test/development escape hatch.

### Authentication Foundation

The first auth foundation is intentionally small and first-party. It stores password hashes only, stores hashed session tokens, and scopes workspace access through `workspace_memberships`.

For UI testing, start the API and web app, open `http://localhost:5173`, and use the login/register panel. In the Visual Studio development profile, the API also enables a development-only button that creates or signs in as:

- Email: `dev@dumptether.local`
- Password: `dumptether-dev-password`

That dev account is a normal `AppUser` with a normal `UserSession` and workspace membership. Production config disables the dev login endpoint.

Temporary guest sessions are also available for trying the wall without registering. They use the same backend session/workspace boundary, but the browser keeps the token in tab-scoped storage and the UI warns the user to sign up or log in to keep the work.

Email confirmation now uses the Brevo transactional email API when enabled. OAuth login is wired for Google, Microsoft, and Facebook when each provider has client configuration. Email MFA is still a future flow, but its config is validated before it can be enabled. If one of these auth features is enabled without the required settings, the API fails startup with a clear `DumpTether configuration is incomplete` exception listing the missing keys.

Basic abuse guardrails are in place for the MVP:

- Auth endpoints are rate limited.
- Task write endpoints are rate limited.
- A workspace is capped at 1,000 active tasks and 5,000 total tasks by default.

These are safety defaults, not a billing model. Real paid/free plans can later move these limits into persisted workspace/account configuration.

Register a user and default workspace:

```powershell
curl.exe -X POST http://localhost:55868/api/auth/register `
  -H "Content-Type: application/json" `
  -d "{\"email\":\"you@example.com\",\"password\":\"change-this-password\",\"displayName\":\"You\"}"
```

Login and copy the returned `sessionToken` for API testing:

```powershell
curl.exe -X POST http://localhost:55868/api/auth/login `
  -H "Content-Type: application/json" `
  -d "{\"email\":\"you@example.com\",\"password\":\"change-this-password\",\"deviceName\":\"local dev\"}"
```

Call authenticated endpoints with the opaque session token:

```powershell
curl.exe http://localhost:55868/api/auth/me `
  -H "Authorization: Bearer {session-token}"
```

Logout revokes the current session:

```powershell
curl.exe -X POST http://localhost:55868/api/auth/logout `
  -H "Authorization: Bearer {session-token}"
```

Create a task item:

```powershell
curl.exe -X POST http://localhost:55868/api/tasks `
  -H "Content-Type: application/json" `
  -d "{\"title\":\"Capture launch notes\"}"
```

List task items:

```powershell
curl.exe http://localhost:55868/api/tasks
```

Get one task item:

```powershell
curl.exe http://localhost:55868/api/tasks/{id}
```

Update a task item:

```powershell
curl.exe -X PATCH http://localhost:55868/api/tasks/{id} `
  -H "Content-Type: application/json" `
  -d "{\"title\":\"Capture launch notes v2\",\"status\":\"In Progress\",\"followUpAt\":\"2026-05-22T09:00:00Z\"}"
```

Add a timeline note:

```powershell
curl.exe -X POST http://localhost:55868/api/tasks/{id}/timeline `
  -H "Content-Type: application/json" `
  -d "{\"note\":\"Captured the source note.\"}"
```

Archive a task item:

```powershell
curl.exe -X POST http://localhost:55868/api/tasks/{id}/archive `
  -H "Content-Type: application/json" `
  -d "{\"archiveResolutionId\":\"{archive-resolution-id}\",\"note\":\"Finished and verified.\"}"
```

Reopen an archived task item:

```powershell
curl.exe -X POST http://localhost:55868/api/tasks/{id}/reopen `
  -H "Content-Type: application/json" `
  -d "{\"note\":\"Needs another pass.\"}"
```

List task templates:

```powershell
curl.exe http://localhost:55868/api/templates
```

Create a task template with custom fields:

```powershell
curl.exe -X POST http://localhost:55868/api/templates `
  -H "Content-Type: application/json" `
  -d "{\"name\":\"Research Note\",\"fields\":[{\"name\":\"Source\",\"type\":\"Text\",\"required\":true,\"sortOrder\":0,\"options\":[]},{\"name\":\"Confidence\",\"type\":\"Select\",\"required\":false,\"sortOrder\":1,\"options\":[\"Low\",\"Medium\",\"High\"]}]}"
```

Create a task from a template:

```powershell
curl.exe -X POST http://localhost:55868/api/tasks `
  -H "Content-Type: application/json" `
  -d "{\"title\":\"Check upgrade note\",\"taskTemplateId\":\"{template-id}\",\"fieldValues\":{\"{source-field-id}\":\"Release notes\",\"{confidence-field-id}\":\"High\"}}"
```

Update task field values:

```powershell
curl.exe -X PATCH http://localhost:55868/api/tasks/{id} `
  -H "Content-Type: application/json" `
  -d "{\"fieldValues\":{\"{confidence-field-id}\":\"Medium\"}}"
```

List saved views:

```powershell
curl.exe http://localhost:55868/api/views
```

Create a saved view for waiting tasks:

```powershell
curl.exe -X POST http://localhost:55868/api/views `
  -H "Content-Type: application/json" `
  -d "{\"name\":\"Waiting work\",\"filter\":{\"status\":\"Waiting\",\"archive\":\"Active\"},\"sort\":{\"field\":\"lastTouchedAt\",\"direction\":\"desc\"},\"sortOrder\":20}"
```

Use a saved view to query tasks:

```powershell
curl.exe "http://localhost:55868/api/tasks?viewId={view-id}"
```

Use equivalent task filters directly:

```powershell
curl.exe "http://localhost:55868/api/tasks?archive=Active&status=Waiting"
curl.exe "http://localhost:55868/api/tasks?notViewedSinceDays=7"
curl.exe "http://localhost:55868/api/tasks?notTouchedSinceDays=14"
curl.exe "http://localhost:55868/api/tasks?followUp=Today"
curl.exe "http://localhost:55868/api/tasks?text=upgrade&sort=followUpAt&direction=asc"
```

Open a task detail with `GET /api/tasks/{id}` to update `LastViewedAt`. This does not update `LastTouchedAt`; only meaningful edits, timeline entries, archive/reopen events, and field changes touch the task.

Run the frontend:

```powershell
cd apps/web
npm.cmd ci
npm.cmd run dev
```

Use `npm.cmd` from PowerShell if the `npm.ps1` shim is blocked by local execution policy.

### Docker

The API has a multi-stage Dockerfile at `src/DumpTether.Api/Dockerfile`. The container listens on port `8080` and does not require the .NET SDK on the server.

For local containerized API + PostgreSQL:

```powershell
Copy-Item .env.example .env
docker compose -f docker-compose.local.yml up --build
```

`docker-compose.local.yml` exposes PostgreSQL on `localhost:5432` for developer tools and exposes the API on `http://localhost:55868`. It applies migrations on API startup for local convenience.

For local PostgreSQL only, keep using:

```powershell
docker compose up -d
```

For production, use `deploy/docker/docker-compose.prod.example.yml` as the canonical starting point and provide real values through an uncommitted `.env.prod` or host secret store:

```powershell
docker compose --env-file deploy/docker/.env.prod.example -f deploy/docker/docker-compose.prod.example.yml config
```

The production example includes PostgreSQL, the API, and a Caddy reverse proxy placeholder. It does not publish the PostgreSQL port. The API reaches PostgreSQL through the Docker network using `Host=postgres`. Keep `DUMPTETHER_APPLY_MIGRATIONS_ON_STARTUP=false` in normal production operation unless you intentionally run a controlled migration step.

See `docs/deployment/docker-compose-production.md` for image build, server `.env.prod`, logs, API restart, PostgreSQL backup, and future GHCR deployment notes.

## Visual Studio

Open `DumpTether.sln` in Visual Studio 2022.

The repository includes `.vsconfig`, so Visual Studio can prompt for the required ASP.NET, Node.js, and Docker tooling workloads if they are missing.

The `DumpTether.Api` project includes several launch profiles in the debug target dropdown next to the Start button:

```text
DumpTether.Api        Debug the API only. Uses the local PostgreSQL connection string.
DumpTether.Backend    Start PostgreSQL, apply migrations, then run the API from PowerShell.
DumpTether.Web        Run the Vite frontend only. Use this when the API is already running.
DumpTether.FullStack  Start PostgreSQL, apply migrations, run API + Vite, then open the web UI.
DumpTether.Database   Start PostgreSQL and apply migrations only.
```

For API debugging from Visual Studio:

1. Start Docker Desktop.
2. Run `.\scripts\dev.ps1 -Target Migrate` once to start PostgreSQL and apply migrations.
3. Set `DumpTether.Api` as the startup project and choose the `DumpTether.Api` launch profile.
4. Press F5.

The API opens at `http://localhost:55868/health`.

For the easiest full-stack run from Visual Studio:

1. Set `DumpTether.Api` as the startup project.
2. Choose the `DumpTether.FullStack` launch profile.
3. Press Ctrl+F5 or F5.

That profile opens separate PowerShell windows for the API and Vite frontend and opens `http://127.0.0.1:5173`. The script waits for the API health endpoint before starting Vite so the frontend proxy does not race the backend startup. It is a run helper, not an API debugger attach. For backend breakpoints, use `DumpTether.Api` and start the frontend separately with `DumpTether.Web` or `.\scripts\dev.ps1 -Target Web -OpenBrowser`.

Visual Studio's true "Multiple startup projects" selection is stored in local `.vs`/`.suo` state, so it is not a good repo setting to commit. The committed launch profiles above are the portable version.

To run the full local stack from a terminal:

```powershell
.\scripts\dev.ps1 -Target Both -OpenBrowser
```

This starts PostgreSQL, applies migrations, and opens separate API and web dev server windows.

### Docker Desktop Troubleshooting

If Docker Desktop says virtualization support was not detected:

1. Enable these Windows features from an elevated terminal:

   ```powershell
   dism.exe /online /enable-feature /featurename:Microsoft-Windows-Subsystem-Linux /all /norestart
   dism.exe /online /enable-feature /featurename:VirtualMachinePlatform /all /norestart
   ```

2. Restart Windows. The `/norestart` flag means the feature changes are not fully active yet.
3. If Docker still reports missing virtualization, enable CPU virtualization in BIOS/UEFI, usually called Intel VT-x, Intel Virtualization Technology, AMD-V, or SVM.
4. Start Docker Desktop and verify:

   ```powershell
   wsl --status
   docker info
   ```

## First GitHub Push

This folder is configured for `https://github.com/bheldbo/DumpTether.git` as the `origin` remote.

After reviewing the files:

```powershell
git add .
git commit -m "Initial DumpTether monorepo"
git push -u origin main
```

Open pull requests against `main`. Pull requests use the template in `.github/pull_request_template.md` and run backend, frontend, and CodeQL workflows.

## Verification

Backend:

```powershell
dotnet restore DumpTether.sln
dotnet build DumpTether.sln --configuration Release -warnaserror
dotnet test DumpTether.sln --configuration Release
```

Frontend:

```powershell
cd apps/web
npm.cmd run lint
npm.cmd run typecheck
npm.cmd run build
```

Docker Compose validation:

```powershell
docker compose config
```

## Configuration

Configuration is split into deployment/runtime configuration, user/workspace configuration, and integration configuration. Real secrets must not be committed to source control.

Useful local/runtime environment variables:

- `ConnectionStrings__DumpTether`: PostgreSQL connection string.
- `Auth__RequireAuthentication`: requires a session for the app.
- `Auth__AllowGuestSessions`: allows temporary browser-tab sessions.
- `Auth__EnableDevelopmentLogin`: local-only dev login button.
- `EmailConfirmation__Enabled`: requires newly registered email/password users to confirm their email before login.
- `EmailConfirmation__PublicBaseUrl`: public API base URL used to build confirmation links.
- `Email__FromEmail`: sender address for transactional email.
- `Email__Smtp__Enabled`: enables SMTP config validation.
- `Email__Smtp__Host`: SMTP host, for Brevo usually `smtp-relay.brevo.com`.
- `Email__Smtp__Port`: SMTP port, usually `587`.
- `Email__Smtp__Username`: SMTP login, store only in `.env` or server secrets.
- `Email__Smtp__Password`: SMTP password, store only in `.env` or server secrets.
- `Email__BrevoApi__Enabled` and `Email__BrevoApi__ApiKey`: Brevo transactional email API mode.
- `Mfa__Email__Enabled`: future email MFA for suspicious logins.
- `OAuth__Google__Enabled`, `OAuth__Microsoft__Enabled`, `OAuth__Facebook__Enabled`: OAuth providers.
- `OAuth__Google__ClientId`, `OAuth__Google__ClientSecret`, `OAuth__Microsoft__ClientId`, `OAuth__Microsoft__ClientSecret`, `OAuth__Facebook__ClientId`, `OAuth__Facebook__ClientSecret`: OAuth secrets.

If you paste or commit an SMTP/API password by accident, rotate it in the provider dashboard before using it again.

### Brevo Email Confirmation

For Brevo API mode, you do not need the SMTP login/password. Set the API values in your uncommitted `.env`:

```powershell
DUMPTETHER_EMAIL_CONFIRMATION_ENABLED=true
DUMPTETHER_EMAIL_CONFIRMATION_PUBLIC_BASE_URL=http://localhost:55868
DUMPTETHER_EMAIL_FROM=your-verified-sender@example.com
DUMPTETHER_EMAIL_BREVO_API_ENABLED=true
DUMPTETHER_EMAIL_BREVO_API_KEY=your-rotated-brevo-api-key
```

`DUMPTETHER_EMAIL_CONFIRMATION_ENABLED=true` means DumpTether will require confirmation before email/password login. `DUMPTETHER_EMAIL_BREVO_API_ENABLED=true` means the email sender is Brevo's transactional API. They are separate on purpose so the same sender can later be used for password reset or email MFA without forcing every environment to require email confirmation.

The sender address in `DUMPTETHER_EMAIL_FROM` must be verified/allowed in Brevo. The confirmation link is built from `DUMPTETHER_EMAIL_CONFIRMATION_PUBLIC_BASE_URL`, so local development usually points at the API, for example `http://localhost:55868`.

Restart the API, then test the provider directly in local development:

```powershell
curl.exe -X POST "http://localhost:55868/api/auth/test-email" -H "Content-Type: application/json" --data-raw '{ "email": "you@example.com" }'
```

The test email subject is `DumpTether email test` and the body says the Brevo API configuration can send email. The real registration email subject is `Confirm your DumpTether email`; it contains a confirmation link and a note that the link expires.

Then test the real confirmation flow by registering a new account. The email link opens `/api/auth/confirm-email?token=...`, marks the user confirmed, and invalidates the token.

Root `.env` files are read by Docker Compose through `env_file`. Visual Studio launch profiles do not automatically import the root `.env`; for local F5 testing, use environment variables, user secrets, or an ignored `appsettings.Local.json`. Keep real API keys and SMTP passwords out of committed `appsettings*.json` files.

Forgot password is not implemented yet. It should use the same Brevo sender with a separate password-reset token table and one-time, expiring links.

### OAuth Setup

OAuth is available only for providers explicitly enabled in runtime config. Register these redirect URIs with the providers:

```text
http://localhost:55868/api/auth/oauth/google/callback
http://localhost:55868/api/auth/oauth/microsoft/callback
http://localhost:55868/api/auth/oauth/facebook/callback
```

Production should use the same paths on the production API origin. Example local config:

```powershell
DUMPTETHER_OAUTH_GOOGLE_ENABLED=true
DUMPTETHER_OAUTH_GOOGLE_CLIENT_ID=...
DUMPTETHER_OAUTH_GOOGLE_CLIENT_SECRET=...
DUMPTETHER_OAUTH_MICROSOFT_ENABLED=true
DUMPTETHER_OAUTH_MICROSOFT_CLIENT_ID=...
DUMPTETHER_OAUTH_MICROSOFT_CLIENT_SECRET=...
DUMPTETHER_OAUTH_FACEBOOK_ENABLED=true
DUMPTETHER_OAUTH_FACEBOOK_CLIENT_ID=...
DUMPTETHER_OAUTH_FACEBOOK_CLIENT_SECRET=...
```

OAuth-created users are treated as email-confirmed because the external provider is the login proof for that route.
