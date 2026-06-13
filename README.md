# ModularVerticalSlice.NET

A production-ready reference implementation of **Vertical Slice Architecture** on
.NET 10, built around pragmatic module isolation, event-driven coordination via
[Wolverine](https://wolverinefx.net/), and a persistence strategy designed to grow
with the system.

The goal is not to demonstrate a theoretical ideal — it is to find a stable,
maintainable equilibrium for modular .NET applications that can evolve from a
well-structured monolith toward independently deployable services without a
disruptive rewrite.

---

## Architecture overview

The solution is organized around **vertical slices**: each feature owns its command,
handler, and persistence access end-to-end. Modules (Bookings, Catalog, Payments,
Notifications) group related slices and define their own boundaries.

```
src/
  ModularVerticalSlice.Application/     # modules, handlers, sagas, domain logic
    Modules/
      Bookings/
        Features/                       # one folder per vertical slice
        Persistence/                    # DbContextSlice interfaces (module-owned)
        Messages/                       # public events and commands
      Catalog/
      Payments/
      Notifications/
    Shared/                             # cross-cutting contracts (no module deps)
  ModularVerticalSlice.Persistence/     # AppDbContext, EF Core config, migrations
  ModularVerticalSlice.SharedKernel/    # Result<T>, base types
  ModularVerticalSlice.WebApi/          # HTTP endpoints, middleware, composition root

tests/
  ModularVerticalSlice.UnitTests/
  ModularVerticalSlice.IntegrationTests/
  ModularVerticalSlice.ArchitectureTests/   # structural rules via NetArchTest
```

Modules communicate through **Wolverine messages** — never by direct method call across
module boundaries. The WebApi is the composition root: it wires modules together but
contains no business logic.

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

When a module eventually needs to become a service, only the implementation changes.
The contract — the slice interface — was already there.

→ See [ADR-0026](docs/adr/ADR-0026-dbcontextslice-pattern.md) for the full design
rationale and comparison with Bounded DbContext.

---

## Architecture rules

Structural boundaries are enforced by automated tests in `ModularVerticalSlice.ArchitectureTests`
using [NetArchTest](https://github.com/BenMorris/NetArchTest). Rules include:

- modules do not reference each other's `Features` or `Domain` namespaces
- handlers and sagas reside in `Application.Modules` and have no dependency on `WebApi`
- `WebApi` does not reference module persistence entity types
- `Application` has no dependency on the `Persistence` assembly — handlers use only their declared DbContextSlice

Violations fail the build. There is no advisory-only rule.

---

## Local setup

Requirements: [.NET 10 SDK](https://dotnet.microsoft.com/download) and Docker.

```powershell
docker compose up -d
dotnet run --project .\src\ModularVerticalSlice.WebApi
```

The development baseline uses the disposable PostgreSQL credentials from
`docker-compose.yml`, pre-configured in `appsettings.Development.json`.

For CI, production, and EF Core design-time commands, override via environment or
a secret manager:

```powershell
$env:ConnectionStrings__Database = "Host=localhost;Port=5432;Database=modularverticalslice;Username=<user>;Password=<password>"
```

---

## Architecture decisions

Design decisions with their rationale are recorded in [`docs/adr/`](docs/adr/).
