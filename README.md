# ModularVerticalSlice.NET

A **modular-first** .NET 10 reference architecture for teams that need explicit boundaries
today and a clean path toward independent services tomorrow — without the operational
overhead of microservices from day one.

---

## What this is

ModularVerticalSlice.NET is both a **reference architecture** and a **starter kit**. It
does not just provide a folder structure — it codifies explicit design choices, their
rationale, and the trade-offs behind them, so the codebase can be understood and extended
without re-deriving every decision from scratch.

It is aimed at .NET teams building applications that need:
- module boundaries enforced by the compiler and architecture tests, not team discipline
- event-driven coordination that scales with the domain, not with infrastructure complexity
- a monolith that is ready to evolve — not one that needs to be rewritten before it can

---

## Modular-first, not microservice-first

Three architectural starting points — and why this project chooses the middle one.

**Classic monolith** — a single codebase with no enforced boundaries. Fast to start,
painful to evolve. Extracting a module into a service requires untangling years of
implicit coupling.

**Microservice-first** — separate services from day one. Explicit boundaries by necessity,
but distributed transaction overhead, eventual consistency, and operational complexity
before the domain is stable enough to justify them.

**Modular-first (this project)** — explicit module boundaries, shared process, local
transactions. Commands, events, and asynchronous coordination cross module boundaries
through messages. Same-store read composition can deliberately cross a boundary through
a narrow, explicit `DbContextSlice` when one relational query is the most concrete
solution. A same-process write that must share the caller's local transaction can use an
explicit owning-module `Contracts` interface; it is a deliberate local exception, not a
transport-ready boundary. Each exception is visible in the type system instead of being
hidden behind a global DbContext. Extract when earned, not by default.

> *The goal is to find a stable, maintainable equilibrium: a well-structured modular
> monolith that can evolve toward independently deployable services without a disruptive
> rewrite.*

---

## Key design choices

**Wolverine over MediatR**  
Wolverine is both mediator and message bus. Outbox, inbox, retry, circuit breaker, sagas,
scheduled messages, and dead-letter queues come from a single library — no glue code
between a mediator layer and a separate transport layer.

**No Repository pattern**  
Queries live in handlers or slice-specific extension methods. Each module declares a
`DbContextSlice` interface exposing only the `DbSet<T>` or `IQueryable<T>` it needs.
Generic `GetById` / `GetAll` repositories obscure data access patterns and create a false
sense of abstraction — they are explicitly excluded.

**`Result<T>` over exceptions for business errors**  
Business failures (`NotFound`, `Conflict`, `Validation`) are return values, not thrown
exceptions. HTTP status codes are mapped explicitly: 404, 409, 422.
Exception-as-control-flow is treated as a design smell.

**PostgreSQL durable transport by default**  
No separate message broker is required to get outbox, inbox, retry, and DLQ.
Wolverine + PostgreSQL provides a built-in persistent transport suitable for most
application workloads. RabbitMQ, Kafka, and Azure Service Bus are supported evolutions —
justified when throughput or topology requirements emerge, not as defaults.

**Architecture tests are mandatory, never advisory**  
Structural rules are enforced by [NetArchTest](https://github.com/BenMorris/NetArchTest):
module boundaries, handler isolation, persistence access constraints. Violations fail the
architecture test suite and should block CI. Code review is not a substitute for
executable architecture rules.

**Shared kernel is intentionally minimal**  
`ModularVerticalSlice.SharedKernel` contains only `Result<T>`, `Error`, and `ErrorType`.
No generic helpers, no extension methods, no base classes. A shared kernel that grows
without discipline becomes the new monolith.

---

## DbContextSlice — persistence isolation without infrastructure overhead

**DbContextSlice** adapts the Bounded DbContext concept to Vertical Slice Architecture —
but it is a fundamentally different thing.

The key distinction: *n* DbContextSlices share a single DbContext instance. There is no
infrastructure separation, no extra connection, no roundtrip cost, no distributed
transaction. What you get instead is access isolation enforced at the type system level:
a module can only see — and only query — the tables declared in its slice. Navigation to
other modules' tables is not restricted by convention or discipline; it simply does not
exist in the type.

This forces an explicit question whenever modules need to collaborate: *how do we relate
across boundaries?* That question is exactly where Bounded Contexts earn their value —
and here you answer it at design time, not at production incident time.

Bounded DbContext conflates two concerns: access isolation and infrastructure separation.
DbContextSlice decouples them. Access isolation is enforced today, by the compiler.
Infrastructure separation — its own DbContext, its own database — is deferred until the
module earns it. The shared transaction is preserved in the meantime: no distributed
transaction overhead, no two-phase commit, no eventual consistency where you don't need
it yet.

When a module eventually needs to become a service, the existing slice boundary reduces
the refactoring surface because handlers already depend on narrow contracts instead of
the global `AppDbContext`. Module-owned slices can often keep their shape while their
implementation and DI registration change. Composite or cross-module read slices
deliberately identify the places that must evolve into an API, replicated read model, or
other remote contract during extraction.

### Pragmatic cross-module reads

Messages remain the default for commands, events, asynchronous workflows, and
coordination that must survive future process boundaries. A modular monolith can still
benefit from its shared relational store for selected read flows.

`GetBookingDetails` and `GetCustomerBookings` use the explicit
`IBookingCatalogReadDbContextSlice` to compose Booking and Catalog data in one projected
SQL query. This preserves flexibility without exposing a global DbContext or silently
opening every module table to every handler.

The approach also helps avoid a common source of EF Core N+1 queries: the required data is
joined and projected explicitly instead of being loaded by repeatedly navigating
relationships. A `DbContextSlice` does not automatically prevent N+1 queries; query shape
still matters. Its value is that cross-module access becomes narrow, reviewable, and easy
to replace when the boundary moves out of process.

→ See [ADR-0026](docs/adr/ADR-0026-dbcontextslice-pattern.md) for the full design
rationale and comparison with Bounded DbContext.

---

## Architecture overview

The solution is organised around **vertical slices**: each feature owns its command,
handler, and persistence access end-to-end. Modules group related slices and define their
public boundary. Commands, events, and asynchronous coordination use **Wolverine
messages**. Direct calls into another module's feature or domain internals are not
permitted. Explicit same-store read composition is allowed when its narrower coupling and
single-query efficiency are worth the trade-off. The booking flow also demonstrates a
Catalog-owned in-process reservation contract: Bookings can reserve tickets atomically
without depending on Catalog features or persistence, while later release remains an
asynchronous compensation message. The `WebApi` project is the composition root: it
wires modules together but contains no business logic.

```
src/
  ModularVerticalSlice.Application/     # modules, handlers, sagas, domain logic
    Modules/
      Bookings/
        Features/                       # one folder per vertical slice
        Persistence/                    # DbContextSlice interfaces (module-owned)
        Messages/                       # public events and commands
      Catalog/
        Contracts/                       # public in-process collaboration contracts
      Payments/
      Notifications/
    Shared/                             # cross-cutting contracts (no module deps)
  ModularVerticalSlice.Persistence/     # AppDbContext, EF Core config, migrations
  ModularVerticalSlice.SharedKernel/    # Result<T>, Error, ErrorType
  ModularVerticalSlice.WebApi/          # HTTP endpoints, middleware, composition root

tests/
  ModularVerticalSlice.UnitTests/
  ModularVerticalSlice.IntegrationTests/
  ModularVerticalSlice.ArchitectureTests/   # structural rules via NetArchTest
```

---

## Architecture rules

Structural boundaries are enforced by automated tests in `ModularVerticalSlice.ArchitectureTests`
using [NetArchTest](https://github.com/BenMorris/NetArchTest). Rules include:

- modules do not reference each other's `Features` or `Domain` namespaces
- handlers and sagas reside in `Application.Modules` and have no dependency on `WebApi`
- `WebApi` does not reference module persistence entity types
- `Application` has no dependency on the `Persistence` assembly — handlers use only their declared `DbContextSlice`

Violations fail the architecture test suite. Run it locally and make it a required CI
gate; there is no advisory-only rule.

---

## Local setup

Requirements: [.NET 10 SDK](https://dotnet.microsoft.com/download) and Docker.

```powershell
dotnet tool restore
docker compose up -d
$env:ConnectionStrings__Database = "Host=localhost;Port=5432;Database=modularverticalslice;Username=postgres;Password=postgres"
dotnet ef database update --project .\src\ModularVerticalSlice.Persistence
dotnet run --project .\src\ModularVerticalSlice.WebApi
```

The development baseline uses the disposable PostgreSQL credentials from
`docker-compose.yml`, pre-configured in `appsettings.Development.json`. The explicit
environment variable is required by the design-time DbContext factory when applying EF
Core migrations. The local .NET tool manifest pins `dotnet-ef 10.0.8`.

For CI, production, and EF Core design-time commands, override via environment or
a secret manager:

```powershell
$env:ConnectionStrings__Database = "Host=localhost;Port=5432;Database=modularverticalslice;Username=<user>;Password=<password>"
```

## Public verification

The public PowerShell script is the primary verification contract. It restores
dependencies, builds the solution, and runs unit, architecture, and integration tests:

```powershell
./scripts/verify.ps1
```

PostgreSQL must already be available. To explicitly start the repository's disposable
PostgreSQL service and wait for it before verification:

```powershell
./scripts/verify.ps1 -StartDatabase
```

The script never stops or removes containers. Integration tests apply their required
migrations. For faster but incomplete feedback, use `-SkipIntegrationTests`; the script
reports clearly that full verification was not performed.

Hosted CI is optional automation, not a requirement for using or understanding the
project. Any CI workflow should invoke this same public script so failures remain locally
reproducible.

The included [GitHub Actions workflow](.github/workflows/verify.yml) demonstrates that
adapter: GitHub provides a clean Ubuntu runner and a disposable PostgreSQL service, then
runs the same `./scripts/verify.ps1` command. It runs for pull requests targeting `main`,
pushes to `main`, and manual starts from the GitHub Actions page.

---

## Architecture decisions

Design decisions with their rationale are recorded in [`docs/adr/`](docs/adr/).
