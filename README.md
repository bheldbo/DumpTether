# DumpTether

DumpTether is a lightweight personal task-and-note system that turns messy working notes into structured tasks with history, templates, views, and archive reasons.

This repository is the initial modular monolith scaffold. It intentionally does not implement business features yet.

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

Configure the API through an environment variable:

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
dotnet run --project src/DumpTether.Api
```

Run the frontend:

```powershell
cd apps/web
npm ci
npm run dev
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
npm run lint
npm run typecheck
npm run build
```

Docker Compose validation:

```powershell
docker compose config
```

## Configuration

Configuration is split into deployment/runtime configuration, user/workspace configuration, and integration configuration. Real secrets must not be committed to source control.
