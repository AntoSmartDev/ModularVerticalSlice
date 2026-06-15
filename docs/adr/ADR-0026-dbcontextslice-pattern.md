# ADR-0026 — DbContextSlice Pattern

**Status:** Accepted  
**Date:** 2026-06-13

## Context

The application uses a single shared `AppDbContext` (EF Core) backed by one PostgreSQL
database. Business modules — Bookings, Catalog, Payments — must be isolated from each
other's persistence surface: a handler in the Bookings module must not accidentally query
or modify Catalog entities. The solution also contains `Delivery/BookingConfirmation`,
but delivery capabilities are not treated as persistence-owning business modules.

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

This is a deliberate pragmatic escape hatch, not a rejection of module boundaries.
Commands, events, asynchronous workflows, and independently committed write-side
coordination still cross modules through messages. A same-process write that must share
the caller's local transaction may instead use an explicit owning-module in-process
contract. That contract operates through the owning module's slice without saving
independently; it is a deliberate local-transaction choice and must be redesigned if the
process or database boundary changes. Same-store read composition may use a dedicated
composite slice when one projected relational query is clearer and more efficient than
introducing premature replication or remote calls. Delivery boundaries stay downstream
consumers of those outcomes; they do not justify broadening module-owned persistence
surfaces.

The explicit query surface also helps control a common EF Core N+1 failure mode. The
current composed Bookings queries join and project the required data in one SQL query
instead of repeatedly navigating relationships. The slice itself is not an automatic
N+1 guarantee: query shape must still be reviewed and tested.

## The extraction path

DbContextSlice is designed for eventual module promotion. When a module needs to become
a separate service:

1. Module-owned slice interfaces already isolate handlers from the global
   `AppDbContext`, reducing the code that must change.
2. Their implementations and DI registrations can move to a module-specific DbContext
   or remote adapter.
3. Composite and cross-module read slices identify the coupling that must be replaced by
   an API, replicated read model, or another remote contract.
4. Shared transactions, migrations, and messaging topology must be revisited when the
   process or database boundary changes.

The pattern does not make service extraction free. It makes the required work visible
earlier and gives it a narrower, testable starting point. Compared with an unplanned
extraction from a global DbContext, fewer handlers depend directly on infrastructure and
the cross-module compromises are easier to locate.

## Architecture enforcement

The `AppDbContextGuardrailTests` assert that no type in the Application
assembly depends on `AppDbContext` directly. Handlers must use their declared
DbContextSlice interface. This guarantee is checked whenever the architecture test suite
runs and should be enforced as a required CI gate.

## Consequences

- **Positive:** access isolation enforced by the compiler — no reliance on team discipline
- **Positive:** shared transaction preserved — atomic operations across module tables
  remain possible within the monolith without distributed transaction infrastructure
- **Positive:** extraction starts from narrow contracts and visible cross-module coupling
- **Positive:** cross-module data access is structurally visible and auditable
- **Positive:** `Slice` naming is consistent with the project's Vertical Slice Architecture vocabulary
- **Neutral:** the rename from `I*DbContext` to `I*DbContextSlice` is a pure refactor — no behavioral change, no migration needed
- **Negative:** slightly longer interface names; accepted as the cost of semantic precision
