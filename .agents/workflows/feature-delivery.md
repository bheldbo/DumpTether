# Feature Delivery Workflow

## 1. Product Readback

Use the Product Owner when the user outcome, vocabulary, or non-goals are not
already explicit. The human owner corrects the readback.

## 2. Architecture Screen

Use the Architect when the feature changes persistence, authorization, sync,
public API contracts, configuration, deployment, module boundaries, or more
than one client.

Small visual fixes may skip this step.

## 3. Create A Work Packet

Use `../templates/work-packet.md`.

A ready slice has:

- one user-visible outcome
- five or fewer acceptance criteria where practical
- one coherent workflow
- one primary risk
- at most one migration
- tests that prove the outcome

Prefer:

> Owner can revoke a pending task share and the recipient loses access.

Avoid separate layer-only issues such as:

> Add a revoked column.

Each merged slice must leave the repository coherent.

## 4. Implement

One Feature Engineer owns the slice end to end. Parallel work is allowed only
for disjoint deliverables and write scopes.

## 5. Independent Review

Use an Independent Reviewer for significant behavior. Add Security or UX
review only when their trigger applies.

## 6. Verify

Run the checks selected in the work packet and record skipped checks honestly.

## 7. Human Acceptance

The human owner decides whether the result matches the intended product. New
product corrections update the relevant product document before the next
slice.

## Parent Feature Decomposition

For a large feature, create a parent issue and vertical child slices such as:

1. Smallest usable domain/API/UI path.
2. Authorization and failure behavior.
3. Persistence or provider parity.
4. Responsive and localization completion.
5. Operational, migration, and release hardening.

Do not force this exact sequence when a different vertical cut leaves cleaner
working states.

