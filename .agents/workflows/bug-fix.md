# Bug Fix Workflow

## 1. Reproduce

Record the smallest reliable reproduction, expected behavior, actual behavior,
runtime, account/role, board/task context, and relevant evidence.

## 2. Bound The Fault

Trace the request and state flow before editing. Determine whether the defect
belongs to domain rules, authorization, persistence, API, cache/state,
rendering, packaging, or environment configuration.

## 3. Create A Focused Work Packet

Acceptance criteria include:

- the reported reproduction is fixed
- a regression test exists where practical
- adjacent permission and failure states still behave correctly

## 4. Implement And Review

Use one Feature Engineer. Invoke Architect, Security, or UX only when the
defect exposes a wider boundary problem.

## 5. Verify

Run the regression test plus the smallest relevant full suite. Re-run the
original user workflow in the affected runtime and viewport.

