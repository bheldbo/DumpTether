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
- `src/DumpTether.Api/appsettings.Desktop.json` documents direct-run desktop
  defaults for developers.
- `scripts/dev.ps1 -Target LocalBoth` starts the local SQLite API plus Vite.
- This folder contains a Tauri scaffold and a sidecar publish script.
- `scripts/desktop.ps1` opens an interactive target menu when run without `-Target`.
- `scripts/desktop.ps1 -Target BuildExe` builds the desktop executable without an installer.
- `scripts/desktop.ps1 -Target Build` builds the desktop executable and NSIS installer.
- Linux desktop bundles can be built on a Linux host or Linux CI runner.
- Installer signing is future release work.
- A first-pass cloud sync exists for board/task headers, templates, header field
  values and newly-synced note/entry field values. Linked boards retry in the
  background and after local changes. Later note edits/deletes, archive/delete
  sync and richer conflict recovery remain future work.

## Desktop Configuration

Desktop uses normal ASP.NET Core configuration. The Tauri shell starts the API
sidecar with an allow-listed Desktop profile:

```text
--environment=Desktop
```

The source profile remains available for direct API development:

```text
src/DumpTether.Api/appsettings.Desktop.json
```

`appsettings.Desktop.json` contains safe desktop defaults for direct developer
runs. Packaged builds do not copy this editable JSON file beside the executable.
Instead, each Tauri launch allocates a random loopback port, creates a fresh
256-bit bootstrap token, injects both into the webview before React starts, and
passes the critical local profile to the sidecar as allow-listed command
arguments. The API validates the profile fail-closed and rejects local requests
without the launch token. SMTP, email confirmation, MFA, hosted OAuth,
registration, and PostgreSQL belong to the hosted cloud API. If
`DataProtection:KeysPath` is empty in Desktop, DumpTether uses the app-data
folder automatically.

Tauri development mode loads the React UI from Vite while still injecting the
random sidecar endpoint and launch token. Packaged CORS trusts only the Tauri
webview origin; development trusts only the local Vite origin. Ordinary web
development keeps proxying to the hosted-development API port.

Tauri's `src-tauri/capabilities/default.json` is not user runtime config. It is
the shell security allow-list containing the sidecar argument sequence plus
strict patterns for the random loopback URL and bootstrap token. The packaged
process cannot use that permission to start the sidecar with arbitrary settings.

The Tauri app identifier is `net.heldbo.dumptether`. This is a reverse-DNS-style
stable application ID for the operating system, installer, app data identity and
future signing/update flows. The bundle publisher is `bheldbo`.

Client release metadata has one non-secret source target:

```text
deploy/targets/standalone.json
```

Edit that file for:

- product name
- desktop version
- app identifier
- publisher
- window title
- default hosted/cloud API URL

Then generate the client config:

```powershell
node scripts/configure-client.mjs --target standalone
```

or:

```powershell
.\scripts\desktop.ps1 -Target ConfigureClient
```

That updates `package.json`, `package-lock.json`, `src-tauri/tauri.conf.json`
and `src-tauri/Cargo.toml`, plus the public React deployment target. CI checks
that generated files match the selected target.

Local desktop commands resolve `DUMPTETHER_DEPLOYMENT_TARGET` from the process
environment first, then root `.env`/`.env.local`, and finally fall back to
`standalone`. Build commands restore the previously generated target afterward
so a customer/development build does not leave tracked metadata dirty.

There is no second desktop business-config file. The desktop sidecar uses normal
ASP.NET configuration, but the packaged app does not ship an editable runtime
overlay. Development scripts can still supply configuration for direct local
diagnostics; production cloud behavior is configured and enforced by the hosted
server.

Endpoint shape:

- Normal desktop use talks to a per-launch random `127.0.0.1` sidecar port.
- Cloud login/sync uses the hosted DumpTether API URL configured before the app
  starts. The user does not edit the server URL inside the running desktop UI.
- Packaged builds read `cloudApiBaseUrl` from the selected deployment target so
  the Account panel knows which hosted API to log in to.
- Self-hosted or alternate-server builds should select a deployment target
  config before packaging or deployment, not through an in-app setting.
- A future online-only desktop mode could make the API base URL configurable, but
  that is not the offline-first default.

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

## Build Executable Only

```powershell
cd apps\desktop
npm run build:desktop:exe
```

Or:

```powershell
.\scripts\desktop.ps1 -Target BuildExe
```

This builds the desktop executable without creating an NSIS/MSI installer:

```text
apps/desktop/src-tauri/target/release/dumptether-desktop.exe
```

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

The PowerShell build helper also copies release-ready files to:

```text
releases/desktop/v0.1.0/windows-x64/
```

The ignored `releases` folder is the predictable local handoff location.
It includes `SHA256SUMS.txt` for verifying copied artifacts. GitHub Releases
and workflow artifacts remain the durable distribution path.

Use the NSIS setup executable for a normal installation. The uninstalled
`dumptether-desktop.exe` is useful for local smoke tests, but it must stay next
to `dumptether-api.exe`. The sidecar is the local ASP.NET Core API and SQLite
runtime bundled into the installer. It is published as a self-contained single
file and does not need PostgreSQL or a system-wide .NET installation.

The durable offline identity is stored in SQLite. Its `DesktopLocal` session is
not a cloud account and cannot be logged out or revoked through ordinary session
controls. Cloud login is optional and creates a separate protected
`DesktopCloud` session on the hosted server. Linked boards retry sync in the
background and after local edits; manual sync remains available for explicit
retry and conflict review.

The `target` tree is generated by Rust/Tauri. It can contain hashed or GUID-like
intermediate names, native build objects and installer snapshots. It is safe to
delete when you want a clean rebuild:

```powershell
.\scripts\clean.ps1
.\scripts\clean.ps1 -Target DesktopDebug
.\scripts\clean.ps1 -Target DesktopRelease
.\scripts\clean.ps1 -Target Generated
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

## GitHub Release Builds

The workflow `.github/workflows/desktop-release.yml` builds desktop installers on
GitHub runners and uploads them to a GitHub Release.

- Tag push: pushing `v*` builds Windows and Linux installers and creates/updates
  the release for that tag.
- Manual run: use `workflow_dispatch` with a tag such as
  `v0.1.0-desktop-preview`; draft and prerelease are enabled by default.
- Windows output: NSIS `.exe` installer.
- Linux output: AppImage, deb and rpm bundles.
- macOS is intentionally not included yet because it needs macOS runners,
  signing and notarization decisions.

This is release packaging, not automatic deployment. Server deployment still uses
the Docker image and Docker Compose.

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
- existing mapped cloud task changes update local task headers and header fields
- new cloud/local tasks can carry their first note entries and entry field values
- append-only notes/timeline entries merge by stable IDs
- independent field changes merge field-by-field
- conflicting edits on the same scalar field become visible conflicts
- shared boards/tasks only appear while logged in and connected
