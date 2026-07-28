# PostgreSQL Resilience

This is a production planning guide. The local Docker Compose database remains
a single PostgreSQL container because pretending that two local containers are
high availability would give false confidence.

## Start With Recoverable Backups

For the first hosted DumpTether deployment:

1. keep PostgreSQL on a persistent volume
2. create an encrypted daily logical backup with `pg_dump`
3. copy backups to storage outside the database host
4. retain several daily, weekly and monthly restore points
5. automate a restore test into a disposable database
6. monitor backup age and failed backup jobs

A backup on the same server is not enough. A replica is also not a backup:
accidental deletes and corrupt writes can replicate to the standby.

Managed PostgreSQL with automated backups and point-in-time recovery is the
lowest-operations option when the public service becomes important. A
self-hosted installation should prove restores before adding failover.

## Add Availability Separately

When downtime becomes important, add a second PostgreSQL host as a streaming
replication standby:

- keep primary and standby on separate failure domains
- use a dedicated `LOGIN REPLICATION` role, not a superuser
- protect replication traffic with private networking and TLS
- use a replication slot carefully and monitor retained WAL disk usage
- keep an independent WAL archive and base backups
- define and rehearse promotion/failover
- decide whether asynchronous replication's small data-loss window is
  acceptable

PostgreSQL streaming replication is asynchronous by default. Synchronous
replication can reduce data loss but increases write latency and can reduce
availability when the synchronous standby is unavailable.

DumpTether should not hand-roll automatic leader election in Docker Compose.
Use a proven PostgreSQL HA system or a managed service when automatic failover
is required.

## Recovery Objectives

Before choosing tools, write down:

- RPO: how much recent data may be lost
- RTO: how long recovery may take
- backup retention
- who receives alerts
- who is allowed to promote a standby
- how DNS, reverse proxy and application connection strings move to the new
  primary

For an early personal deployment, a reasonable starting target is daily
off-host backups, a tested restore procedure and manual recovery. Add
point-in-time recovery and a standby when actual usage justifies the added
operational burden.

## Application Expectations

The API continues to use one writable PostgreSQL endpoint. Failover tooling
owns which server that endpoint resolves to. EF Core migrations run once as a
controlled deployment step, not concurrently on every replica.

Backups and replication planning must include:

- the PostgreSQL database
- ASP.NET data-protection keys
- deployment secrets or a documented way to rotate/recreate them
- future attachment storage, if attachments are introduced

Never place database backups, WAL archives, credentials or data-protection keys
in Git.
