# ADR 0008: Transactional Email Boundary

## Status

Accepted for the current registration flow.

## Context

DumpTether needs email confirmation and later account recovery. Provider
delivery can fail temporarily or reach a quota. Creating a separate email
service now would add another deployment, protocol, secret boundary, release
pipeline, and failure mode before the product needs that scale.

## Decision

Transactional email remains a module inside the DumpTether modular monolith:

```text
Auth use case
  -> IEmailProvider
  -> Brevo API | SMTP | development capture
```

While email confirmation is required, registration creates the user, default
board, membership, legal acceptance, and confirmation token inside one database
transaction. The confirmation email is sent before that transaction commits.
If delivery fails, the transaction rolls back so the address is immediately
available for a clean retry and no half-created account remains.

This synchronous boundary is deliberate for the current product because there
is no resend-confirmation workflow or operational outbox yet. A transactional
outbox may replace it later, but only together with retry observability and a
user-facing resend flow.

Configuration should select one provider:

```text
Email:Provider = None | BrevoApi | Smtp
```

Provider credentials remain server secrets. Local development should use
Mailpit through the SMTP provider so confirmation can be tested
without a public domain or paid service.

An agent should request provider configuration only when the provider path is
ready to test. The request must list the exact provider-console values and
redirect/domain requirements. Secrets must be placed in user-secrets or an
uncommitted environment file, never pasted into chat or committed.

## Consequences

- Email remains replaceable without becoming a microservice.
- Failed delivery does not reserve the email address or leave partial account
  data behind.
- Provider latency is currently part of registration latency.
- A future worker may consume a transactional outbox without moving the email
  module to another repository, but that is not current behavior.
- A separate email service is reconsidered only when multiple products,
  independent scaling, compliance, or organizational ownership creates a real
  boundary.
