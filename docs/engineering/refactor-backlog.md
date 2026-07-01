# Refactor Backlog

This file collects historical seams, legacy naming and cleanup ideas found while working on active features.

Use it when a cleanup is real but not worth derailing the current task.

## Candidates

### Shared clock lives under `DumpTether.App.Tasks`

- Status: noted
- Context: `IClock` and `SystemClock` are shared application infrastructure, but currently live in `src/DumpTether.App/Tasks/`.
- Why it can wait: the type is small and already wired correctly through dependency injection.
- Future cleanup: move clock abstractions to a shared app infrastructure namespace/folder and update imports.
