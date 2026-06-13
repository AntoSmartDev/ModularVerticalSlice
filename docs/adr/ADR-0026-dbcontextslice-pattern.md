# ADR-0026 — DbContextSlice Pattern

**Status:** Accepted  
**Date:** 2026-06-13

## Context

The application uses a single shared `AppDbContext` (EF Core) backed by one PostgreSQL
database. Modules — Bookings, Catalog, Payments, Notifications — must be isolated from
each other's persistence surface: a handler in the Bookings module must not accidentally
query or modify Catalog entities.

An early naming convention named the per-module persistence interfaces
`IBookingWriteDbContext`, `IBookingReadDbContext`, etc. This name is misleading:
the `DbContext` suffix implies a **standalone** DbContext with its own connection and
lifecycle, which is not the case. All module interfaces are implemented by the same
shared `AppDbContext` instance — they are projections of it, not independent contexts.

## Decision

Rename all per-module persistence interfaces to the `DbContextSlice` convention:

| Old name | New name |
|---|---|
| `IBookingWriteDbContext` | `IBookingWriteDbContextSlice` |
| `IBookingReadDbContext` | `IBookingReadDbContextSlice` |
| `IBookingCatalogReadDbContext` | `IBookingCatalogReadDbContextSlice` |
| `ICatalogReadDbContext` | `ICatalogReadDbContextSlice` |
| `ICatalogWriteDbContext` | `ICatalogWriteDbContextSlice` |
| `IPaymentReadDbContext` | `IPaymentReadDbContextSlice` |
| `IPaymentWriteDbContext` | `IPaymentWriteDbContextSlice` |

The naming convention for new module persistence interfaces is:
`I{Module}[Read|Write]DbContextSlice`

## What is a DbContextSlice?

A **DbContextSlice** is a narrow interface that exposes only the EF Core `DbSet`
or `IQueryable` surfaces that a single module needs. It is:

- **a slice, not a standalone context** — implemented by the shared `AppDbContext`,
  not a separate `DbContext` subclass with its own connection or lifetime
- **read or write** — read slices expose `IQueryable<T>` (no-tracking); write slices
  expose `DbSet<T>` (tracked, change-tracked by EF)
- **module-owned** — defined in `Application.Modules.{Module}.Persistence`, not in
  the Persistence project; the module declares what it needs, the Persistence project
  fulfils the contract

```
Application.Modules.Bookings.Persistence
  IBookingWriteDbContextSlice   ← declared here (module owns the contract)
  IBookingReadDbContextSlice

Persistence
  AppDbContext : IBookingWriteDbContextSlice,   ← fulfilled here
                 IBookingReadDbContextSlice,
                 ICatalogReadDbContextSlice,
                 ...
```

## Rationale

- `IBookingWriteDbContext` implied a dedicated context → misleading about identity
- `IBookingWriteDbContextSlice` signals it is a **projection** of a shared context
- The `Slice` suffix aligns with the project's **Vertical Slice Architecture** vocabulary
- Handlers that accept `IBookingWriteDbContextSlice` cannot reach Catalog or Payment
  entities — the slice enforces the boundary at the type system level
- The architectural guardrail in `AppDbContextGuardrailTests` (M11/F02) ensures
  Application.Modules never directly references `AppDbContext`, enforcing that all
  access goes through the declared slice

## Consequences

- **Positive:** naming accurately conveys that modules share one underlying context
- **Positive:** `Slice` vocabulary is consistent with Vertical Slice Architecture
- **Positive:** new module onboarding follows the clear pattern: define your
  `I{Module}[Read|Write]DbContextSlice`, implement in `AppDbContext`, register adapter
- **Neutral:** the rename is a pure refactor — no behavioral change, no migration needed
- **Negative:** slightly longer interface names; accepted as the cost of precision
