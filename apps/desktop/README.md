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
- `src/DumpTether.Api/appsettings.Desktop.json` holds desktop sidecar defaults.
- `scripts/dev.ps1 -Target LocalBoth` starts the local SQLite API plus Vite.
- This folder contains a Tauri scaffold and a sidecar publish script.
- `scripts/desktop.ps1 -Target Build` builds the desktop executable and NSIS installer.
- Linux desktop bundles can be built on a Linux host or Linux CI runner.
- Installer signing and sync are not implemented yet.

## Desktop Configuration

Desktop uses normal ASP.NET Core configuration. The Tauri shell starts the API
sidecar with:

```text
--environment=Desktop
```

That makes the sidecar read:

```text
src/DumpTether.Api/appsettings.json
src/DumpTether.Api/appsettings.Desktop.json
```

`appsettings.Desktop.json` contains safe desktop defaults: SQLite, local loopback
URL, app-owned DataProtection keys, disabled email/OAuth/MFA, and the exact local
origins needed by the webview and Vite dev server. If `DataProtection:KeysPath`
is left empty in Desktop, DumpTether uses the app-data folder automatically.

Tauri's `src-tauri/capabilities/default.json` is not DumpTether runtime config. It
is Tauri's security allow-list saying the shell may start the bundled sidecar with
that one `--environment=Desktop` argument. In other words: edit ASP.NET config for
DumpTether behavior; edit Tauri config only for shell/window/bundle permissions.

There is no second desktop business-config file. The desktop sidecar uses normal
ASP.NET configuration, so environment variables can still override
`appsettings.Desktop.json` when needed.

## Prerequisites

- Node.js and npm
- Rust toolchain with Cargo
- .NET SDK
- Visual Studio Desktop development with C++ workload on Windows
  - Include MSVC x64/x86 build tools and a Windows SDK.
- Windows WebView2 runtime on Windows

Install desktop dependencies from this folder:

```powershell
cd apps\desktop
npm install
```

Or from the repository root:

```powershell
.\scripts\desktop.ps1 -Target Install
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

Or from the repository root:

```powershell
.\scripts\desktop.ps1 -Target Dev
```

## Build Sidecar

```powershell
cd apps\desktop
npm run build:sidecar
```

Or:

```powershell
.\scripts\desktop.ps1 -Target Sidecar
```

This creates a generated sidecar binary under `src-tauri/binaries/`.
Those binaries are ignored by Git.

The first sidecar build may download .NET runtime packs from NuGet for the selected runtime, for example `win-x64`.

## Build Installer

```powershell
cd apps\desktop
npm run build:desktop
```

Or:

```powershell
.\scripts\desktop.ps1 -Target Build
```

This builds:

```text
apps/desktop/src-tauri/target/release/dumptether-desktop.exe
apps/desktop/src-tauri/target/release/bundle/nsis/DumpTether_0.1.0_x64-setup.exe
```

MSI/WiX is available as an explicit target:

```powershell
cd apps\desktop
npm run build:desktop:msi
```

Or:

```powershell
.\scripts\desktop.ps1 -Target BuildMsi
```

The MSI target depends on WiX and the Windows Installer service being available
on the build machine. If WiX fails with ICE validation errors about the Windows
Installer service, use the NSIS installer for local testing and fix the Windows
Installer/WiX environment before cutting a signed MSI release.

Code signing, release signing certificates and update feeds are future release
work.

## Linux Desktop Bundles

Build Linux desktop bundles on Linux, not from Windows:

```bash
cd apps/desktop
npm install
npm run build:desktop:linux
```

That command:

1. publishes the local ASP.NET Core API sidecar for the current Linux CPU
2. builds the shared React UI
3. asks Tauri for AppImage, deb and rpm bundles

Expected output:

```text
apps/desktop/src-tauri/target/release/bundle/appimage/
apps/desktop/src-tauri/target/release/bundle/deb/
apps/desktop/src-tauri/target/release/bundle/rpm/
```

Linux build machines need the normal Tauri Linux prerequisites, Rust/Cargo,
Node.js, npm and the .NET SDK. The helper scripts also support explicit sidecar
runtimes:

```bash
node scripts/build-sidecar.mjs --runtime linux-x64
node scripts/build-sidecar.mjs --runtime linux-arm64
```

The Windows `scripts/desktop.ps1 -Target BuildLinux` target exists for PowerShell
Core on a Linux host. It intentionally refuses to produce Linux desktop bundles
from Windows.

## Future Sync Shape

Offline data is local SQLite. When the user later logs in and syncs:

- local-only tasks upload to the server
- remote changes download into SQLite
- append-only notes/timeline entries merge by stable IDs
- independent field changes merge field-by-field
- conflicting edits on the same scalar field become visible conflicts
- shared boards/tasks only appear while logged in and connected
