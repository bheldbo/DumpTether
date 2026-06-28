# DumpTether Desktop

This folder is reserved for the future desktop shell.

The intended desktop shape is:

```text
Tauri shell
  -> bundled local DumpTether.Api sidecar
  -> SQLite database
  -> shared React UI from apps/web
```

The desktop shell should not contain business rules. Task rules, template validation, sharing boundaries and archive behavior stay in C# under `src/`.

Current first step:

- `DumpTether.Api` can run with `Database:Provider=Sqlite`.
- `scripts/dev.ps1 -Target LocalBoth` starts the local SQLite API plus Vite.

Future steps:

- add Tauri project files
- bundle/publish the API sidecar
- load the built React UI
- add tray/window behavior
- add installer packaging
- add optional login/sync
