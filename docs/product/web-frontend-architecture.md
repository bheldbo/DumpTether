# DumpTether Web Frontend Architecture

DumpTether's web app should stay plain and fast, but the code should not become one large
screen script. The frontend is a React/Vite app that reflects backend state and delegates
business rules to the ASP.NET Core API.

## Visual Studio

Open `DumpTether.sln` when working on the backend, migrations, API tests, and shared C#
domain/application code.

Treat `apps/web` as a separate Vite application. It is included in the solution for discovery,
but the normal frontend loop is still:

```powershell
cd apps/web
npm run dev
npm run lint
npm run typecheck
npm run build
```

## Module Boundaries

`App.tsx` should orchestrate application state, top-level routing mode, data refreshes, and
cross-feature coordination. It should not own every modal, editor, list item, and formatting
helper.

Current frontend boundaries:

- `components/`: small reusable UI primitives such as icons, modal shell, toasts, and color picker.
- `components/TaskMetadata.tsx`: shared task badge/chip display used by wall cards and detail views.
- `features/sharing/`: board/task sharing modal, pending invite chips, member chips, and task share strip.
- `features/task-wall/`: wall-level loading, creation, and batch action controls.
- `features/timeline/`: note and entry-field behavior for a task.
- `features/task-detail/`: task-specific dialogs and detail interactions.
- `taskUtils.ts`: task wall filtering, card state, and color helpers.
- `templateFieldUtils.ts`: template field shape, entry field defaults, and validation helpers used by UI.
- `templateLayout.ts`: grid/layout calculations for template header and entry fields.
- `workspaceCache.ts`: first-party board snapshot cache for fast workspace/view switching.
- `appUtils.ts`: browser/runtime helpers, formatting, URL state, role display, and generic error text.
- `appTypes.ts`: shared UI-only types and local storage keys.

## Direction

The next refactors should move these remaining surfaces out of `App.tsx`:

- sidebar and board navigation
- workspace header and member/share controls
- task wall/card interactions
- task detail header and field editing
- template list/editor
- account/settings panels

Each feature module should expose one or two top-level components and keep helper functions
private unless another feature genuinely needs them.

## Rules

- Backend authorization and validation remain authoritative.
- React may cache and optimistically render, but it must not invent permissions.
- Shared UI primitives belong in `components`; product workflows belong in `features`.
- Keep API DTO types in `types.ts`; do not duplicate backend rules in TypeScript.
- Prefer focused modules over broad barrel files so imports show the real dependency direction.
