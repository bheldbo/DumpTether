# ADR 0007: Effective Archive Policy

## Status

Proposed.

## Context

Personal users may not need an archive reason for every task. Shared or future
organization boards may require consistent resolutions or explanations for
review. A single unconditional global rule cannot represent both cases.

`All Tasks` is an aggregate across boards, so its tasks may have different
archive requirements.

## Decision

Archive behavior resolves from an effective policy:

1. The task's owning board override.
2. The board owner's user default.
3. A system fallback where resolution is not required and note is optional.

Suggested model:

```text
ArchivePolicy
  Scope: UserDefault | Workspace
  ResolutionRequirement: None | Optional | Required
  NoteRequirement: Optional | Required

ArchiveResolution
  ArchivePolicyId
  Name
  Description
  RequiresExplanation
  IsActive
```

A selected resolution can require an explanation even when notes are otherwise
optional. Deactivating a resolution prevents future selection without erasing
historical evidence.

For shared boards and task shares, the originating board policy applies.
Members cannot replace it with their personal policy. Read-only users cannot
archive. Other archive permission remains governed by task and board
authorization.

`All Tasks` has no policy of its own. Each task resolves policy from its owning
board.

Archive history should retain a snapshot of the selected resolution's display
name so later configuration changes do not rewrite past meaning.

## Consequences

- Personal archiving can remain lightweight.
- Board owners can require consistent evidence for shared work.
- Archive validation moves from an unconditional entity rule to a policy-aware
  application/domain operation.
- Implementing this requires entities, migration, policy resolution, API
  contracts, tests, and user/board settings UI.

