# ADR 0001: Modular Monolith Architecture

## Status

Accepted

## Context

DumpTether is a personal task-and-note system where everything is a task, every task has structured fields, and every task has a timeline. The MVP needs clear domain boundaries without the operational cost of distributed services.

## Decision

DumpTether will use a modular monolith:

- `src/DumpTether.Domain` contains domain entities, value objects, and core invariants.
- `src/DumpTether.App` contains application services and use cases.
- `src/DumpTether.Data` contains EF Core persistence against PostgreSQL.
- `src/DumpTether.Api` hosts the ASP.NET Core API and delegates use cases to the application layer.
- `apps/web` contains the React + TypeScript + Vite frontend.

Controllers and endpoints must not contain domain logic. Application services enforce use cases, and domain entities enforce core invariants. Persistence stays relational for core concepts, with JSON reserved for flexible configuration and field values where it is appropriate.

## Consequences

- The MVP remains easy to run locally and reason about.
- Project boundaries make later extraction possible without designing for microservices prematurely.
- Business changes can be tested mostly through domain and application code.
- EF Core migrations must accompany persisted entity changes.

## Non-Goals

The MVP will not include microservices, AI features, MCP, email, calendar integration, sharing, or desktop support.
