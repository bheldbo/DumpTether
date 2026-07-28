# ADR 0008: Transactional Email Boundary

## Status

Proposed.

## Context

DumpTether needs email confirmation and later account recovery. Provider
delivery can fail temporarily or reach a quota. Creating a separate email
service now would add another deployment, protocol, secret boundary, release
pipeline, and failure mode before the product needs that scale.

## Decision

Transactional email remains a module inside the DumpTether modular monolith:

```text
Auth use case
  -> transactional email outbox
  -> background dispatcher
  -> IEmailProvider
  -> Brevo API | SMTP | development capture
```

Registration should persist the user, confirmation token, and outbox message in
one database transaction. Delivery happens asynchronously. A temporary provider
failure must not lose the confirmation request or roll back a valid
registration.

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
- Failed delivery can be retried and observed.
- A separate worker process may consume the same outbox later without moving
  the module to another repository.
- A separate email service is reconsidered only when multiple products,
  independent scaling, compliance, or organizational ownership creates a real
  boundary.
