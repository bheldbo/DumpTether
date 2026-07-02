# DumpTether

DumpTether is a plain personal task wall for messy work notes.

Everything is a task. A task can be a TODO, a note, a follow-up, a tiny case file, or a sticky reminder. Templates add structure when you need it; colors, categories, follow-up dates, statuses and filters help you find things again without turning the app into Jira.

The current MVP is a web app backed by ASP.NET Core and PostgreSQL. The same API can also run locally against SQLite as the first step toward the desktop/offline app. The planned desktop shell should reuse the same React UI and C# domain/application logic.

## What It Does Today

- Create boards. A board is the main space for a set of tasks.
- Use walls inside a board: `All tasks` and `Archive`.
- Create task templates with header fields and per-entry fields.
- Build simple note/TODO-like entry layouts from template rows and cells.
- Add tasks as sticky-note style cards.
- Color-code tasks and boards.
- Add one or more categories to a task.
- Set custom statuses and follow-up dates.
- Filter by text, status, category, color, follow-up, stale/not touched and sharing.
- Add structured notes to tasks.
- Archive and reopen tasks with archive reasons.
- Share boards or tasks when online.
- Use Owner, Member and Read-only/Guest-style access.
- Use English or Danish UI text.
- Run locally with Docker PostgreSQL, ASP.NET Core and Vite.
- Run the API in local SQLite mode as an offline foundation.
- Run the API in Docker for server deployment.
- Gate public signup with open, whitelist, invite-only or closed registration modes.

## Product Direction

The product should feel like:

- a wall of personal sticky notes
- structured notes inside each task
- powerful filtering when needed
- fast dumping, updating and moving on

It should not become:

- a Jira clone
- a kanban-first system
- a timeline-heavy audit tool
- a generic notes app
- an import/parser project

Future goals:

- desktop/local app using the same React UI
- SQLite offline state
- login and optional sync to a hosted server
- OAuth login
- email confirmation/MFA
- better live collaboration
- image attachments
- sharing hardening
- backups/export as `.dumptether`

AI, MCP, email scanning and calendar integrations are future extensions, not MVP behavior.

## Architecture

DumpTether is a modular monolith.

```text
apps/web/                 React + TypeScript + Vite frontend
apps/desktop/             Tauri desktop shell scaffold for the shared UI
src/DumpTether.Api/       ASP.NET Core HTTP API
src/DumpTether.App/       Application services and use cases
src/DumpTether.Domain/    Domain model and business rules
src/DumpTether.Data/      EF Core persistence with PostgreSQL/SQLite provider selection
src/DumpTether.Database/  Runnable database maintenance shell for migrations/local data chores
docs/                     ADRs, security notes, deployment notes
deploy/docker/            Production Docker Compose examples
```

Design principles:

- Keep business rules in C#.
- Keep React focused on interaction and presentation.
- Keep backend authorization authoritative.
- Keep core concepts relational.
- Use JSON only where flexibility makes sense, such as field values/layout config.
- Prefer one API shape for web, hosted server and future desktop sidecar.
- Avoid duplicated business logic between web, server and future desktop.

Current shortcomings:

- Desktop/offline mode has the first SQLite/API foundation and Tauri scaffold, but no sync yet.
- Email confirmation/OAuth plumbing exists, but provider setup is still rough.
- Sharing works as an MVP flow, but permissions and notifications need more polish.
- Live updates are early and should be hardened before real multi-user use.
- Attachments/images are not implemented yet.
- The frontend is being actively refactored out of the older giant `App.tsx`.

## Run It Locally

Prerequisites:

- .NET SDK 8+
- Node.js 24 LTS+ with npm
- Docker Desktop or Docker Engine with Compose
- Git

Fast web/server path:

```powershell
dotnet tool restore
.\scripts\dev.ps1 -Target Both -OpenBrowser
```

That starts PostgreSQL, applies EF migrations, runs the API and starts Vite.

Local SQLite path:

```powershell
.\scripts\dev.ps1 -Target LocalBoth -OpenBrowser
```

That starts the same API against a local SQLite database and starts Vite. It does not start Docker or PostgreSQL. By default the database is created at `%APPDATA%\DumpTether\dumptether.db` on Windows or the local application data folder on Linux. This is the offline foundation, not full cloud sync yet.

Quick chooser:

```text
Web dev with PostgreSQL:    .\scripts\dev.ps1 -Target Both -OpenBrowser
Offline-style web dev:      .\scripts\dev.ps1 -Target LocalBoth -OpenBrowser
Desktop dev shell:          .\scripts\desktop.ps1 -Target Dev
Windows desktop installer:  .\scripts\desktop.ps1 -Target Build
Linux server deployment:    Docker Compose, see docs/deployment/docker-compose-production.md
Linux desktop bundles:      build on a Linux host or Linux CI runner, see apps/desktop/README.md
```

Manual path:

```powershell
docker compose up -d
dotnet run --project src/DumpTether.Database -- migrate
dotnet run --project src/DumpTether.Api --launch-profile DumpTether.Api
```

Then in another terminal:

```powershell
cd apps/web
npm.cmd ci
npm.cmd run dev
```

Open:

```text
http://localhost:5173
```

API health:

```text
http://localhost:55868/health
```

## Visual Studio

Open `DumpTether.sln` in Visual Studio 2022.

Useful launch profiles:

```text
DumpTether.Api        API only
DumpTether.Api.Local  API only with local SQLite
DumpTether.Backend    PostgreSQL + migrations + API
DumpTether.Web        Vite frontend only
DumpTether.FullStack  PostgreSQL + migrations + API + Vite
DumpTether.LocalFullStack  local SQLite API + Vite
DumpTether.DesktopDev      Tauri desktop dev shell + local SQLite API sidecar
DumpTether.Database   interactive database tools menu
DumpTether.DatabaseMigrate   apply EF migrations only
DumpTether.DatabaseSeedTestData   apply migrations and seed reusable local test data
DumpTether.DatabaseStatus    show configured database status
```

For backend debugging, use `DumpTether.Api` and start the web app separately.

For a quick full stack run, use `DumpTether.FullStack`.

For a quick offline-style run without Docker/PostgreSQL, use `DumpTether.LocalFullStack`.

For the desktop shell, install Rust/Cargo first and use `DumpTether.DesktopDev`. This runs the Tauri wrapper, Vite, and a local SQLite API sidecar.

For database maintenance, use the `DumpTether.Database` project. It opens a menu for status/migrate, seeding development test data, clearing task data, resetting the configured local database, and inspecting/removing the local SQLite database. It deliberately does not own business rules; it delegates to `DumpTether.Data` and EF Core.

Docker orchestration still belongs to scripts:

```powershell
.\scripts\db.ps1 -Action Start
.\scripts\db.ps1 -Action Stop
.\scripts\db.ps1 -Action Migrate
```

The frontend lives in `apps/web`. Visual Studio is fine for the solution/backend; Vite is still the normal frontend dev server.

## Configuration

Two files matter most:

- `appsettings*.json`: committed, non-secret defaults and app behavior.
- `.env`: uncommitted runtime/secrets, especially Docker and production-like runs.

Do not commit real secrets.

ASP.NET Core configuration is the runtime source of truth. The layers are:

```text
src/DumpTether.Api/appsettings.json
  -> appsettings.Development.json / appsettings.Production.json / appsettings.Desktop.json
  -> environment variables
  -> .env values loaded by DumpTether scripts or Docker Compose
```

Typed config is bound in C# through `DumpTetherApiSetup` and the option classes in `DumpTether.App` plus `DumpTetherDatabaseOptions` in `DumpTether.Data`. `DumpTetherRuntimeSetupReader` reads cross-cutting runtime setup such as CORS and startup migrations. There is not a separate hidden config system.

Tauri config is different: `apps/desktop/src-tauri/tauri.conf.json` and `capabilities/default.json` configure the desktop shell, window, bundler and sidecar permission. They do not replace `appsettings*.json`.

Common local PostgreSQL defaults:

```text
Host=localhost;Port=5432;Database=dumptether;Username=dumptether;Password=dumptether_dev_password
```

Useful runtime settings:

```text
ConnectionStrings__DumpTether
Database__Provider
Database__Sqlite__Path
Auth__RequireAuthentication
Auth__AllowGuestSessions
Auth__SignupMode
Auth__SignupWhitelistEmails__0
Auth__SignupWhitelistDomains__0
Auth__SignupInviteCodes__0
Auth__EnableDevelopmentLogin
Auth__SessionDays
Auth__SessionCleanupDays
Archive__RetentionDays
Cors__AllowedOrigins__0
Cors__AllowedOrigins__1
Cors__AllowedOrigins__2
EmailConfirmation__Enabled
EmailConfirmation__PublicBaseUrl
Email__FromEmail
Email__BrevoApi__Enabled
Email__BrevoApi__ApiKey
OAuth__Google__Enabled
OAuth__Microsoft__Enabled
OAuth__Facebook__Enabled
Usage__MaxActiveTasksPerWorkspace
Usage__MaxTotalTasksPerWorkspace
```

The `scripts/dev.ps1` helper reads root `.env` values and maps `DUMPTETHER_*` variables to ASP.NET configuration keys. Visual Studio launch profiles do not automatically import `.env`, so use launch settings, user secrets or local environment variables for F5-only runs.

Signup modes are server-side:

- `Open`: anyone can register.
- `Whitelist`: only configured emails/domains can register.
- `InviteOnly`: a configured invite code is required.
- `Closed`: registration is disabled.

For a small hosted instance, start with `Auth__SignupMode=InviteOnly` or `Auth__SignupMode=Whitelist` and `Auth__AllowGuestSessions=false`. Temporary guest sessions are not allowed to write task, template, category, sharing or board data to the hosted API; a true browser-only demo mode is future UI work.

CORS is configured only in the API. The server does not need CORS to "reach" clients. CORS only matters when browser or webview JavaScript calls an API on another origin.

If the website and API are served from the same origin, CORS can stay empty and same-origin browser policy is enough. Vite dev and the Tauri sidecar shape are cross-origin (`localhost:5173` or `tauri.localhost` calling `127.0.0.1:55868`), so local development uses exact allowed origins such as `Cors__AllowedOrigins__0=http://localhost:5173`, `Cors__AllowedOrigins__1=http://127.0.0.1:5173`, `Cors__AllowedOrigins__2=http://tauri.localhost` or `DUMPTETHER_CORS_ALLOWED_ORIGIN_0=https://dumptether.example.com`. Never use `*` with credentials.

Set `Database__Provider=Postgres` for hosted/server PostgreSQL. Set `Database__Provider=Sqlite` for local/offline SQLite. `Database__Sqlite__Path` is optional; if omitted, DumpTether uses the OS app-data path.

## Docker And Production

Local PostgreSQL only:

```powershell
docker compose up -d
```

Local API + PostgreSQL in Docker:

```powershell
Copy-Item .env.example .env
docker compose -f docker-compose.local.yml up --build
```

Production example files:

```text
deploy/docker/docker-compose.prod.example.yml
deploy/docker/.env.prod.example
deploy/docker/Caddyfile.example
docs/deployment/docker-compose-production.md
```

Production rules:

- API runs as a Docker image.
- PostgreSQL runs as a separate Docker service.
- PostgreSQL uses a persistent Docker volume.
- PostgreSQL should not publish port `5432` publicly.
- Secrets live on the server, not in GitHub.
- Caddy/nginx should terminate public HTTP/HTTPS.
- Run migrations intentionally, not accidentally.

Validate production compose shape:

```powershell
docker compose --env-file deploy/docker/.env.prod.example -f deploy/docker/docker-compose.prod.example.yml config
```

## Database

PostgreSQL is the server database. SQLite is the local/offline database.

Schema ownership is split like this:

- `DumpTether.Domain` owns entities and invariants.
- `DumpTether.App` owns use-case interfaces.
- `DumpTether.Data` owns EF Core mappings, repositories and migrations.
- `DumpTether.Database` is the runnable maintenance shell for migrations/status/local resets.

The schema is mostly normalized relational data:

- users
- sessions
- workspaces
- workspace memberships
- projects/categories
- task items
- task shares
- task templates
- field definitions
- field values
- timeline/note entries
- archive resolutions

Flexible pieces such as template layout and field values use JSON where it keeps the product adaptable.

Inspect local PostgreSQL with pgAdmin:

```text
Host: localhost
Port: 5432
Database: dumptether
Username: dumptether
Password: dumptether_dev_password
```

Clear only task/note/share data from the local dev database:

```powershell
.\scripts\db.ps1 -Action ClearTasks
```

This keeps users, sessions, boards, categories, templates and settings.

Run the database project directly when you do not need Docker orchestration:

```powershell
dotnet run --project src/DumpTether.Database -- status
dotnet run --project src/DumpTether.Database -- migrate
dotnet run --project src/DumpTether.Database -- seed-test-data
dotnet run --project src/DumpTether.Database -- clear-tasks
dotnet run --project src/DumpTether.Database -- reset
dotnet run --project src/DumpTether.Database -- local-info
```

Other local database actions:

```powershell
.\scripts\db.ps1 -Action Menu
.\scripts\db.ps1 -Action Start
.\scripts\db.ps1 -Action Status
.\scripts\db.ps1 -Action Migrate
.\scripts\db.ps1 -Action SeedTestData
.\scripts\db.ps1 -Action ResetPostgres
.\scripts\db.ps1 -Action LocalInfo
.\scripts\db.ps1 -Action RemoveLocalSqlite
```

`seed-test-data` creates or reuses a local development user, a sample board, default archive reasons, Basic/ToDo templates and a couple of sample tasks. Override the demo login with `DUMPTETHER_SEED_EMAIL` and `DUMPTETHER_SEED_PASSWORD` in your local environment or `.env`.

Destructive actions ask for typed confirmation unless you pass `-Yes`.

Local SQLite database defaults:

```text
Windows: %APPDATA%\DumpTether\dumptether.db
Linux:   ~/.local/share/DumpTether/dumptether.db
```

The current SQLite path is for local/offline development. Full login sync, conflict resolution and desktop packaging are tracked in `docs/adr/0006-local-offline-runtime-and-sync.md`.

## Desktop App

Desktop source lives in `apps/desktop`.

The current desktop scaffold uses Tauri and the same React UI from `apps/web`. The intended app runtime is:

```text
Tauri window
  -> local DumpTether.Api sidecar
  -> SQLite local database
  -> same React task wall UI
```

Install desktop prerequisites first:

- Rust toolchain with Cargo
- Node.js and npm
- .NET SDK
- Windows WebView2 runtime on Windows

Then:

```powershell
.\scripts\desktop.ps1 -Target Install
.\scripts\desktop.ps1 -Target Dev
.\scripts\desktop.ps1 -Target Build
```

Or from `apps/desktop`:

```powershell
npm install
npm run dev
npm run build:desktop
```

`build:sidecar` publishes `DumpTether.Api` as a generated sidecar binary for Tauri. `build:desktop` runs `tauri build`, which is the path toward `.exe`/`.msi` bundles. Signing certificates, update feeds and sync are still future work.

The first sidecar build may download .NET runtime packs from NuGet for the selected runtime, for example `win-x64`.

Linux desktop publishing is architecture-supported, but should be done on Linux:

```bash
cd apps/desktop
npm install
npm run build:desktop:linux
```

That builds the Linux sidecar and asks Tauri for AppImage/deb/rpm bundles. Expected output is under `apps/desktop/src-tauri/target/release/bundle/`. Linux packaging needs the normal Tauri Linux system dependencies, Rust/Cargo, Node.js and the .NET SDK on the Linux build machine. Cross-building Linux desktop installers from Windows is not the current plan.

Server deployment to Linux is already the intended production path: use the Docker image and `deploy/docker/docker-compose.prod.example.yml`. That is separate from Linux desktop publishing.

## Security Notes

Current security posture:

- Passwords are hashed.
- Session tokens are random and stored hashed.
- Workspace/task access is scoped server-side.
- Backend authorization is authoritative.
- Auth/task write endpoints have rate limiting.
- CORS uses an explicit allow-list when cross-origin browser calls are needed.
- Production PostgreSQL should stay private to the Docker network.
- Real secrets are ignored and must not be committed.

Before a real public MVP:

- Confirm production cookies/session settings.
- Confirm HTTPS at the reverse proxy.
- Review auth error logging so it helps debugging without leaking tokens.
- Harden sharing and live update authorization paths.

## Testing

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

Docker compose:

```powershell
docker compose config
```

## Git And CI Flow

Preferred flow:

```text
branch -> pull request -> checks -> squash merge to main
```

Branch names should be feature/issue shaped. Codex-created branches normally use `codex/`.

Pull requests should include:

- summary
- what changed
- tests run
- risks
- follow-up work

CI currently covers backend restore/build/test, frontend lint/typecheck/build, CodeQL and Docker build validation where configured.

Release direction:

1. Merge to `main`.
2. Tag a release when the MVP state is worth preserving.
3. Build/publish API image.
4. Server pulls the new image.
5. Run migrations intentionally.
6. Restart API with Docker Compose.

Automatic deployment is intentionally not wired yet.

Desktop release workflow:

- `.github/workflows/desktop-release.yml` builds desktop installers and attaches them to a GitHub Release.
- Tag pushes such as `v0.1.0` create/update a release from that tag.
- Manual `workflow_dispatch` lets you create a draft/prerelease such as `v0.1.0-desktop-preview`.
- Windows builds produce the NSIS `.exe` installer.
- Linux builds produce AppImage/deb/rpm bundles on an Ubuntu runner.
- macOS builds are intentionally not included yet because they need macOS runner/signing/notarization decisions.
- Installer signing and auto-update feeds are still future release hardening.

## AI Disclosure

This repository is being developed with AI coding assistance. DumpTether itself does not currently include AI features in the MVP product.

If AI summaries, daily digests or MCP integrations are added later, they should be explicit opt-in features with clear privacy boundaries.

## FAQ

### Why does Vite say `ECONNREFUSED` for `/api/...`?

Vite is running, but the ASP.NET API is not running or not reachable at the configured proxy target. Start the API with Visual Studio, `dotnet run`, or `.\scripts\dev.ps1 -Target Both`.

### Do I use `appsettings` or `.env`?

Use `appsettings` for committed non-secret defaults. Use `.env`, environment variables, user secrets or server secrets for local/prod secrets and runtime overrides.

### Can I inspect the database with pgAdmin?

Yes. Use `localhost:5432`, database `dumptether`, user `dumptether`, password `dumptether_dev_password` for the local Docker Compose database.

### Why not JSON files as the main offline save format?

The planned desktop/local app should use SQLite for live local state. JSON is better for small config and export/import bundles.

### Does the desktop app duplicate the web app?

No. The intended design is one React UI and one C# domain/application/API shape. Website uses hosted ASP.NET Core + PostgreSQL. Desktop uses local ASP.NET Core sidecar + SQLite.

### Can I run without an account?

Guest sessions can exist for trying the app, but hosted/server task writes require a real account. For a public server, prefer `DUMPTETHER_ALLOW_GUEST_SESSIONS=false` until a browser-only demo mode exists.

### Is sharing available offline?

No. Shared boards/tasks are online concepts. Future desktop sync should show local tasks offline and sync/share when logged in and connected.

### Where should longer docs live?

Versioned technical docs should stay in `docs/` so they travel with the code. A GitHub Wiki can later mirror user-facing guides, but the repo docs should remain the source of truth.
