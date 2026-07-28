# DumpTether Agent Operating Model

DumpTether uses one coordinating Codex thread and a small set of temporary
specialist roles. The human owner remains the product authority.

Agents are not a pretend company hierarchy. Invoke a role only when it owns a
distinct decision, implementation slice, or independent check.

## Coordinator

The main Codex thread is the Coordinator. It:

- keeps the latest user request authoritative
- selects only the roles needed for the work
- creates and maintains the work packet
- prevents agents from editing overlapping files
- integrates agent output
- runs or delegates verification
- reports decisions, risks, and unfinished work to the human owner

The Coordinator is not a separate sub-agent.

## Core Roles

| Role | Default reasoning | Use it for |
| --- | --- | --- |
| [Product Owner](roles/product-owner.md) | High | Product intent, user outcomes, acceptance criteria, non-goals, product readback |
| [Principal Architect](roles/architect.md) | High | Boundaries, data ownership, security/platform impact, ADRs, issue slicing |
| [Feature Engineer](roles/feature-engineer.md) | Medium | One bounded vertical implementation slice |
| [Independent Reviewer](roles/reviewer.md) | High | Findings-first review against the work packet and architecture |
| [Verifier](roles/verifier.md) | Medium | Tests, responsive smoke tests, migrations, Docker, desktop, and release checks |

## On-Demand Specialists

| Role | Default reasoning | Trigger |
| --- | --- | --- |
| [Security Reviewer](roles/security-reviewer.md) | High | Auth, sessions, sharing, CORS, secrets, uploads, SignalR, sync trust boundaries |
| [UX Reviewer](roles/ux-reviewer.md) | High | New workflows, responsive UI, interaction regressions, accessibility, localization |

## Reasoning Levels

Choose reasoning effort by risk, not by role title:

- `Low`: formatting, spelling, deterministic metadata, and simple localization.
- `Medium`: isolated UI behavior, a focused endpoint, ordinary tests, and documentation.
- `High`: cross-layer features, authorization, caching, migrations, sharing, templates,
  desktop packaging, and platform behavior.
- `XHigh`: expensive-to-reverse architecture, identity boundaries, destructive data
  transitions, cryptographic/session design, and sync conflict policy.

For DumpTether, auth, sharing, SignalR authorization, PostgreSQL/SQLite parity,
migrations, offline sync, and template schema changes start at `High`.

## Routing

Use the smallest useful set of roles:

- Product wording or behavior is ambiguous: Product Owner first.
- Persistence, authorization, public API, sync, configuration, deployment, or
  multiple clients are affected: Architect before implementation.
- A bounded change is ready: Feature Engineer.
- A significant behavior or security boundary changed: Independent Reviewer.
- A user-facing workflow changed: UX Reviewer and responsive verification.
- Auth, sharing, sessions, secrets, live updates, or sync changed: Security Reviewer.
- Every implementation ends with Verifier checks appropriate to its risk.

Skip a role if it cannot produce a distinct artifact or decision.

## Durable Artifacts

Use these instead of relying on a long chat:

- [Product readback](templates/product-readback.md)
- [Work packet](templates/work-packet.md)
- [Decision note](templates/decision-note.md)
- [Review report](templates/review-report.md)
- [Verification report](templates/verification-report.md)

Feature delivery follows [feature-delivery.md](workflows/feature-delivery.md).
Bug fixes follow [bug-fix.md](workflows/bug-fix.md).

## Working With Codex

Natural-language requests are enough:

```text
Use the Product Owner to recite this feature back to me before implementation.
```

```text
Have the Architect split this into vertical GitHub issues, then implement only
the first ready slice.
```

```text
Implement this work packet, then run an independent review and verification.
```

The Coordinator should keep working while sub-agents handle independent
sidecar tasks. Parallel implementation is allowed only when write scopes do
not overlap.

