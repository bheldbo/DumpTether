# DumpTether Desktop

This is the first desktop shell scaffold.

The intended runtime is:

```text
Tauri shell
  -> bundled local DumpTether.Api sidecar
  -> SQLite database in the user app-data folder
  -> shared React UI from apps/web
```

The desktop app must not contain task/business rules. The domain model, validation,
archive behavior, sharing boundaries and future sync rules stay in the C# projects.

## Current State

- `DumpTether.Api` can run with `Database:Provider=Sqlite`.
- `scripts/dev.ps1 -Target LocalBoth` starts the local SQLite API plus Vite.
- This folder contains a Tauri scaffold and a sidecar publish script.
- Installer signing and sync are not implemented yet.

## Prerequisites

- Node.js and npm
- Rust toolchain with Cargo
- .NET SDK
- Windows WebView2 runtime on Windows

Install desktop dependencies from this folder:

```powershell
cd apps\desktop
npm install
```

## Local Desktop Dev

For now, the most reliable local loop is still:

```powershell
.\scripts\dev.ps1 -Target LocalBoth -OpenBrowser
```

Once Rust/Tauri dependencies are installed, run:

```powershell
cd apps\desktop
npm run dev
```

## Build Sidecar

```powershell
cd apps\desktop
npm run build:sidecar
```

This creates a generated sidecar binary under `src-tauri/binaries/`.
Those binaries are ignored by Git.

The first sidecar build may download .NET runtime packs from NuGet for the selected runtime, for example `win-x64`.

## Build Installer

```powershell
cd apps\desktop
npm run build:desktop
```

Tauri can produce Windows installer bundles from `tauri build`. Code signing,
release signing certificates and update feeds are future release work.

## Future Sync Shape

Offline data is local SQLite. When the user later logs in and syncs:

- local-only tasks upload to the server
- remote changes download into SQLite
- append-only notes/timeline entries merge by stable IDs
- independent field changes merge field-by-field
- conflicting edits on the same scalar field become visible conflicts
- shared boards/tasks only appear while logged in and connected
