# DumpTether

DumpTether is a lightweight personal task-and-note system that turns messy working notes into a plain task wall with structured notes, templates, views, and archive reasons.

This repository is the initial modular monolith scaffold with the first task item API surface.

## MVP Boundaries

- Everything will be modeled as a task.
- Tasks will have structured fields and compact note history.
- Templates, views, projects, and archive reasons are core concepts.
- The product should feel like a personal task wall, not a Jira clone, timeline-heavy audit system, or complex import/parser tool.
- AI, MCP, email, calendar, sharing, and desktop support are outside the MVP.

## Repository Layout

```text
apps/web/                 React + TypeScript + Vite frontend
src/DumpTether.Api/       ASP.NET Core API host
src/DumpTether.App/       Application services and use cases
src/DumpTether.Data/      EF Core persistence
src/DumpTether.Domain/    Domain model and business rules
docs/adr/                 Architecture decision records
docs/security/            Security principles
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

The first task endpoints use a temporary development workspace and project until authentication and workspace selection are introduced. This is development-only plumbing, not a security boundary.

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

That profile opens separate PowerShell windows for the API and Vite frontend and opens `http://localhost:5173`. It is a run helper, not an API debugger attach. For backend breakpoints, use `DumpTether.Api` and start the frontend separately with `DumpTether.Web` or `.\scripts\dev.ps1 -Target Web -OpenBrowser`.

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
