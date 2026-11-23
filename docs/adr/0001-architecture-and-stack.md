# ADR 0001: Architecture and technology stack

- **Status:** Accepted
- **Date:** 2026-11-23

## Context
The design document (chapter 6.1) proposes a three-tier web application:
React client → ASP.NET Core Web API → relational database, with clearly
separated presentation, API, service, domain and data-access layers.
The most important business rule (BR01, no overlapping reservations) must
hold even when two requests arrive at the same time.

## Decision
- **Backend:** ASP.NET Core Web API on .NET 10, split into four projects
  (simplified Clean Architecture): Domain → Application → Infrastructure → Api.
  Domain has no dependencies; Infrastructure (EF Core) depends on Application.
- **Database:** PostgreSQL 18 with EF Core + Npgsql. PostgreSQL was chosen
  because its EXCLUDE constraint (with `btree_gist` and `tstzrange`) can
  enforce BR01 in the database itself, not only in application code.
- **Frontend:** React + TypeScript + Vite, with a typed API client generated
  from the API's OpenAPI document.
- **Local environment:** PostgreSQL runs in Docker Compose.

## Consequences
- Business rules live in one place (the service layer) and are unit-testable.
- BR01 is guaranteed even under concurrency.
- Integration tests need a real PostgreSQL (Testcontainers), because the
  EXCLUDE constraint can't be tested with an in-memory database.
- More projects than a single-project API, but this matches the layers
  described in the design document.
