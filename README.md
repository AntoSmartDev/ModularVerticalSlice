# ModularVerticalSlice.NET

A modular monolith reference architecture for .NET teams that want explicit boundaries today and a credible path toward service extraction later, without starting with microservice overhead.

## What this repository is

ModularVerticalSlice.NET is both a reference architecture and a starter kit. It does not just show a folder structure. It encodes concrete architectural choices, their trade-offs, and the guardrails that keep those choices stable as the codebase grows.

It is aimed at teams that need:

- business boundaries enforced by the compiler and architecture tests
- message-driven coordination without introducing a broker on day one
- a modular monolith that can evolve without a disruptive rewrite

## Architecture model

This project starts from classic Vertical Slice Architecture: each use case owns its request path end to end instead of being split across horizontal technical layers.

It then adds two constraints that classic VSA often leaves implicit:

- explicit business-module boundaries under `Modules/`
- explicit delivery boundaries under `Delivery/` for consequence-handling capabilities that are application concerns but not business modules

It also keeps the useful parts of Clean Architecture: explicit boundaries, dependency discipline, testability, resilience, and a composition root. What it avoids is the extra ceremony that appears when repository layers, service layers, and abstractions are introduced before the codebase has earned them.

Entity behavior stays on the persisted entity by default. A separate `Domain/` folder exists only when a rule, policy, or value object does not belong cleanly to one persisted entity, or when a cross-entity concept deserves its own home.

In practice this is a modular monolith with explicit boundaries, local transactions, and message-based coordination across modules.

## Why modular-first

Three common starting points:

- **Classic monolith**: fast to start, but boundaries are mostly implicit and extraction later means untangling accidental coupling.
- **Microservice-first**: explicit boundaries from day one, but also distributed transactions, eventual consistency, and operational complexity before the domain is stable enough to justify them.
- **Modular-first (this project)**: explicit module boundaries, shared process, local transactions, and durable messaging. Cross-module commands, events, and asynchronous workflows go through messages. Same-store read composition is allowed only as a narrow, visible exception. A same-process write that must share the caller's local transaction may use an explicit owning-module contract. Extract when earned, not by default.

## Architecture at a glance

- `Modules/` contains business boundaries such as Catalog, Bookings, and Payments.
- `Delivery/` contains concrete downstream capabilities such as booking confirmation email delivery.
- `WebApi` is the composition root. It wires boundaries together but does not own business logic.
- Cross-module coordination uses Wolverine messages.
- Persistence access is isolated through `DbContextSlice` interfaces instead of a global `AppDbContext` dependency from handlers.
- Architecture tests enforce the structural rules.

```text
src/
  ModularVerticalSlice.Application/
    Modules/
      Bookings/
        Features/
        Persistence/
        Messages/
      Catalog/
        Contracts/
      Payments/
    Delivery/
      BookingConfirmation/
    Shared/
  ModularVerticalSlice.Persistence/
  ModularVerticalSlice.SharedKernel/
  ModularVerticalSlice.WebApi/

tests/
  ModularVerticalSlice.UnitTests/
  ModularVerticalSlice.IntegrationTests/
  ModularVerticalSlice.ArchitectureTests/
```

## Getting started

### Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Docker

### Local database

The repository provides a disposable PostgreSQL instance through [`docker-compose.yml`](docker-compose.yml):

```powershell
docker compose up -d postgres
```

Default local credentials:

- Host: `localhost`
- Port: `5432`
- Database: `modularverticalslice`
- Username: `postgres`
- Password: `postgres`

### Apply migrations

The design-time DbContext factory requires an explicit connection string when running EF Core commands:

```powershell
dotnet tool restore
$env:ConnectionStrings__Database = "Host=localhost;Port=5432;Database=modularverticalslice;Username=postgres;Password=postgres"
dotnet ef database update --project .\src\ModularVerticalSlice.Persistence
```

### Run the API

```powershell
dotnet run --project .\src\ModularVerticalSlice.WebApi
```

In Development the API listens on:

- `http://localhost:5211`
- `https://localhost:7037`

The Development profile also enables a fake authenticated user from `appsettings.Development.json`. That means booking endpoints can be exercised locally without wiring a real identity provider.

### OpenAPI

This repository exposes OpenAPI JSON in Development, but it does not include a Swagger UI page.

- OpenAPI document: `http://localhost:5211/openapi/v1.json`

## First API path

If you want to prove the host is running and then exercise one realistic flow, use this sequence.

### 1. Check the host

```powershell
curl.exe http://localhost:5211/health/live
curl.exe http://localhost:5211/health/ready
```

`/health/live` is process-only. `/health/ready` depends on PostgreSQL connectivity.

### 2. Create a catalog event

```powershell
curl.exe -X POST "http://localhost:5211/api/v1/events/" `
  -H "Content-Type: application/json" `
  -d "{\"title\":\"Architecture Demo\",\"date\":\"2030-06-01T20:00:00Z\",\"ticketPrice\":25.0,\"availableTickets\":100}"
```

This returns an `EventReadModel` payload that includes the generated event id.

### 3. Create a booking for that event

In Development, fake authentication is already enabled and provides the `bookings.write` scope required by the booking endpoint.

```powershell
curl.exe -X POST "http://localhost:5211/api/v1/bookings/" `
  -H "Content-Type: application/json" `
  -d "{\"eventId\":\"<event-id>\",\"quantity\":2,\"clientRequestId\":\"<new-guid>\"}"
```

This returns the new booking id.

### 4. Query the results

The booking query endpoints use the same Development fake user and scopes:

```powershell
curl.exe http://localhost:5211/api/v1/events/
curl.exe http://localhost:5211/api/v1/bookings/
curl.exe http://localhost:5211/api/v1/bookings/<booking-id>
```

## Public verification

The public PowerShell script is the main verification contract:

```powershell
./scripts/verify.ps1
```

It restores dependencies, builds the solution, and runs unit, architecture, and integration tests.

If PostgreSQL is not already running, the script can start the repository's disposable database container and wait for it:

```powershell
./scripts/verify.ps1 -StartDatabase
```

Notes:

- the script does not stop or remove containers
- integration tests apply their required migrations
- `-SkipIntegrationTests` gives faster but incomplete feedback

Hosted CI is optional automation over the same public contract. Any CI workflow should invoke this script so failures stay locally reproducible.

## Architecture rules

Structural boundaries are enforced by `ModularVerticalSlice.ArchitectureTests` using [NetArchTest](https://github.com/BenMorris/NetArchTest).

Rules include:

- modules do not reference each other's `Features` or `Domain` namespaces
- delivery boundaries do not act as hidden business modules
- handlers and sagas live in `Application` namespaces and do not depend on `WebApi`
- `WebApi` does not reference module persistence entity types
- `Application` does not depend on the `Persistence` assembly directly; handlers use only their declared `DbContextSlice`

These rules are meant to fail builds when violated. They are not advisory.

## Key design choices

### Wolverine over MediatR

Wolverine is both mediator and message bus. Outbox, inbox, retry, circuit breaker, sagas, scheduled messages, and dead-letter queues come from one library instead of being assembled from separate mediator and transport layers.

### No Repository pattern

Queries live in handlers or slice-specific extension methods. Each module declares a `DbContextSlice` interface exposing only the `DbSet<T>` or `IQueryable<T>` it needs. Generic repository abstractions such as `GetById` and `GetAll` are intentionally excluded.

### `Result<T>` over exceptions for business errors

Business failures such as `NotFound`, `Conflict`, and `Validation` are modeled as return values, not control-flow exceptions. HTTP mapping is explicit.

### PostgreSQL durable transport by default

This project does not require a separate broker to get outbox, inbox, retry, and dead-letter behavior. Wolverine plus PostgreSQL is the default durable transport. RabbitMQ, Kafka, or Azure Service Bus become relevant only when throughput or topology needs justify them.

### Shared kernel stays small

`ModularVerticalSlice.SharedKernel` contains only `Result<T>`, `Error`, and `ErrorType`. A shared kernel that grows without discipline becomes the next monolith.

## DbContextSlice

`DbContextSlice` adapts the bounded-DbContext idea to a modular monolith without paying the infrastructure cost of separate DbContexts for every module.

The important distinction is that many slices share one `AppDbContext` instance. That means:

- no extra connection
- no extra roundtrip cost
- no distributed transaction overhead
- access isolation enforced at the type-system level

A handler can only see the tables declared in its slice. Cross-module reads must therefore be explicit. In this solution, `GetBookingDetails` and `GetCustomerBookings` use `IBookingCatalogReadDbContextSlice` to compose Booking and Catalog data in one query without falling back to a global DbContext dependency.

For the full rationale, see [ADR-0026](docs/adr/ADR-0026-dbcontextslice-pattern.md).

## Operational readiness

The host exposes a small operational surface that matches the actual runtime.

### Health endpoints

- `GET /health/live`: process-only liveness
- `GET /health/ready`: readiness based on PostgreSQL connectivity through the application `AppDbContext`

Because Wolverine durable messaging shares the same PostgreSQL dependency, the readiness signal is meaningful for the running application.

### Correlation and logs

- an inbound `X-Correlation-Id` header is honored and echoed back
- a non-empty correlation id is generated when the header is absent
- the correlation id flows through HTTP and message handling scopes
- meaningful workflow events are emitted as structured logs

## Architecture decisions

Design decisions with rationale are recorded in [docs/adr/](docs/adr/).
