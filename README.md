# DumpTether

DumpTether is a lightweight personal task-and-note system that turns messy working notes into structured tasks with history, templates, views, and archive reasons.

This repository is the initial modular monolith scaffold with the first task item API surface.

## MVP Boundaries

- Everything will be modeled as a task.
- Tasks will have structured fields and a timeline.
- Templates, views, projects, and archive reasons are core concepts.
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

To run the API from Visual Studio:

1. Start Docker Desktop.
2. Run `.\scripts\dev.ps1 -Target Migrate` once to start PostgreSQL and apply migrations.
3. Set `DumpTether.Api` as the startup project.
4. Press F5.

The API opens at `http://localhost:55868/health`.

To run the full local stack from a terminal:

```powershell
.\scripts\dev.ps1 -Target All
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
