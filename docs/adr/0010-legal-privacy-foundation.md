# ADR 0010: Legal and privacy foundation

## Status

Accepted for hosted account registration.

## Context

Hosted DumpTether accounts process identity, session, workspace, sharing, and
user-authored content. Public registration therefore needs a visible Terms of
Use and Privacy Notice, a durable record of the versions presented at account
creation, and a clear separation between contractual acceptance and privacy
information.

A source-code license is a separate decision. It controls reuse of the
repository; it does not replace Terms of Use for a hosted service.

## Decision

- The server publishes the current legal document versions and public operator
  contact through `GET /api/auth/options`.
- New password and external-provider accounts must submit the exact current
  versions when `Legal:RequireAcceptance` is enabled.
- The UI asks the user to agree to the Terms of Use and acknowledge the Privacy
  Notice. It does not describe all processing as consent.
- Accepted versions are stored as append-only `LegalAcceptance` records.
- A changed version is rejected so the client must reload and present the
  current document.
- Existing sign-in is not blocked by this first slice. A future material terms
  change may introduce re-acceptance as a separate use case.
- Public signup remains an explicit deployment switch. Mail delivery, legal
  configuration, abuse controls, and operational readiness are independent
  gates.

## Consequences

- PostgreSQL and SQLite both carry legal acceptance migrations.
- The production operator name and privacy contact are required configuration
  when acceptance is enabled.
- Acceptance records establish what was presented; they do not by themselves
  make the service GDPR compliant.
- Self-service export and deletion, processor records, retention operations,
  and incident procedures remain required before broad public availability.

## Source license

No source license is selected by this ADR. The owner must choose deliberately
between a permissive license, a network-copyleft license such as AGPL, or a
proprietary/all-rights-reserved model.
