# ADR-0026 — DbContextSlice Pattern

**Status:** Accepted  
**Date:** 2026-06-13

## Context

The application uses a single shared `AppDbContext` (EF Core) backed by one PostgreSQL
database. Modules — Bookings, Catalog, Payments, Notifications — must be isolated from
each other's persistence surface: a handler in the Bookings module must not accidentally
query or modify Catalog entities.

### The Bounded DbContext precedent

The established approach in DDD + EF Core is the **Bounded DbContext**: one DbContext
class per bounded context, each with its own connection, its own migration scope, and
its own transactional lifetime. The pattern gives genuine infrastructure isolation — each
module literally cannot see the other's tables because the other's DbContext does not
expose them.

The cost is proportional: separate connections, separate migration pipelines, no atomic
transactions across boundaries without distributed transaction infrastructure, and
significantly higher operational complexity. This cost is appropriate when a module is
mature and stable enough to justify the commitment. It is premature when modules are
still evolving and the boundaries are not yet settled.

### What the early naming got wrong

An early version of this codebase named the per-module persistence interfaces
`IBookingWriteDbContext`, `IBookingReadDbContext`, etc. The `DbContext` suffix implies
a standalone context — its own connection, its own lifetime. That implication is false:
all module interfaces are implemented by the same shared `AppDbContext` instance.
The name described something that does not exist.

## Decision

Introduce the **DbContextSlice** pattern and rename all per-module persistence interfaces
to the `I{Module}[Read|Write]DbContextSlice` convention.

A **DbContextSlice** is a narrow interface that exposes only the EF Core `DbSet` or
`IQueryable` surfaces a single module needs. It is implemented by the shared
`AppDbContext` — not a separate class — and registered via an explicit adapter in the
DI container.

| Old name | New name |
|---|---|
| `IBookingWriteDbContext` | `IBookingWriteDbContextSlice` |
| `IBookingReadDbContext` | `IBookingReadDbContextSlice` |
| `IBookingCatalogReadDbContext` | `IBookingCatalogReadDbContextSlice` |
| `ICatalogReadDbContext` | `ICatalogReadDbContextSlice` |
| `ICatalogWriteDbContext` | `ICatalogWriteDbContextSlice` |
| `IPaymentReadDbContext` | `IPaymentReadDbContextSlice` |
| `IPaymentWriteDbContext` | `IPaymentWriteDbContextSlice` |

## How DbContextSlice differs from Bounded DbContext

Bounded DbContext conflates two concerns: **access isolation** and **infrastructure
separation**. DbContextSlice decouples them.

**Access isolation** is the constraint that a module can only query the tables it owns.
This is a design-time concern, expressible in the type system. DbContextSlice enforces
it today, at compile time: a handler that depends on `IBookingWriteDbContextSlice` has
no access to Catalog or Payment tables — not by convention, not by discipline, but
because those tables simply do not exist in the type.

**Infrastructure separation** is the decision to give a module its own connection, its
own migration scope, and its own transactional lifetime. This is an operational concern,
appropriate when a module's boundaries are stable and its load profile justifies the cost.
DbContextSlice defers this decision: *n* slices share one DbContext instance, one
connection, and one transaction. No distributed transaction, no two-phase commit, no
eventual consistency where you do not need it yet.

The module earns infrastructure separation when it is ready. Until then, the shared
transaction is not a compromise — it is the correct choice for a modular monolith.

## The forcing function

Because a DbContextSlice exposes only the tables declared in its interface, navigation
to another module's tables is structurally impossible. A handler in Bookings cannot
accidentally join against Payments entities — the Payments DbSet is not present in
`IBookingWriteDbContextSlice`.

This makes cross-module data access an explicit architectural decision, not an
implementation detail. When Bookings needs to read Catalog data (a legitimate read
composition), a dedicated composite slice is declared — `IBookingCatalogReadDbContextSlice`
— and its existence is documented, visible, and testable. There is no silent coupling.

## The extraction path

DbContextSlice is designed for eventual module promotion. When a module needs to become
a separate service:

1. The slice interface — declared in `Application.Modules.{Module}.Persistence` — does
   not change. It is the contract.
2. The implementation changes: instead of the shared `AppDbContext`, the module gets its
   own `DbContext` subclass backed by its own database.
3. The DI registration changes: the new implementation replaces the adapter.

Nothing in the module's handlers, sagas, or domain logic needs to change. The contract
was already there.

This is the key structural advantage over an unplanned extraction: the persistence
boundary was drawn at design time, enforced by the compiler, and validated by
architecture tests (see ADR-0025, M11/F01). The extraction is a deployment decision,
not a refactoring effort.

## Architecture enforcement

The `AppDbContextGuardrailTests` (M11/F02) assert that no type in the Application
assembly depends on `AppDbContext` directly. Handlers must use their declared
DbContextSlice interface. This guarantee is checked on every build.

## Consequences

- **Positive:** access isolation enforced by the compiler — no reliance on team discipline
- **Positive:** shared transaction preserved — atomic operations across module tables
  remain possible within the monolith without distributed transaction infrastructure
- **Positive:** extraction path is clean and requires no handler-level refactoring
- **Positive:** cross-module data access is structurally visible and auditable
- **Positive:** `Slice` naming is consistent with the project's Vertical Slice Architecture vocabulary
- **Neutral:** the rename from `I*DbContext` to `I*DbContextSlice` is a pure refactor — no behavioral change, no migration needed
- **Negative:** slightly longer interface names; accepted as the cost of semantic precision
