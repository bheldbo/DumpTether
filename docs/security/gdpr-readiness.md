# GDPR readiness

## Current assessment

DumpTether has useful technical foundations, but the repository must not claim
complete GDPR compliance yet. Compliance also depends on the real operator,
hosting contracts, procedures, notices, and day-to-day operation.

Implemented foundations:

- password and session-token hashing;
- workspace-scoped authorization;
- configurable retention for sessions and archived tasks;
- versioned Terms and Privacy acknowledgement at registration;
- server-side secrets and HTTPS deployment direction;
- no advertising or analytics tracking in the current client.

## Required before broad public signup

- publish the controller's complete identity and privacy contact;
- verify and sign processor agreements for hosting, Cloudflare, Brevo, and
  Microsoft where used;
- document international transfer safeguards;
- implement or operationalize account deletion and data export requests;
- document backup, log, archive, and inactive-account retention;
- maintain a record of processing activities;
- define incident response and personal-data-breach procedures;
- test email confirmation, authentication, sharing authorization, rate limits,
  restoration, and deletion end to end;
- add bot protection or equivalent abuse controls before fully open signup.

## Legal acceptance is not blanket consent

The registration checkbox records agreement to service terms and acknowledgement
of the privacy notice. Necessary account processing should use the appropriate
contractual, legitimate-interest, or legal-obligation basis. Optional future
processing such as marketing must be assessed separately and must not be hidden
inside the service checkbox.

## Operational owner

The deployment operator owns this checklist. A code change cannot certify a
particular deployment as compliant.
