<!--
Sync Impact Report
==================
Version change: (unfilled template) → 1.3.0
  - First concrete constitution. Version 1.3.0 is taken as supplied by the developer
    (no earlier filled versions exist in this repository).

Principles defined (template placeholder → new title):
  - [PRINCIPLE_1_NAME] → I. Clean Architecture (NON-NEGOTIABLE)
  - [PRINCIPLE_2_NAME] → II. Data Access (NON-NEGOTIABLE)
  - [PRINCIPLE_3_NAME] → III. Security (NON-NEGOTIABLE)
  - [PRINCIPLE_4_NAME] → IV. Inventory & Concurrency (NON-NEGOTIABLE)
  - [PRINCIPLE_5_NAME] → V. Data Integrity

Added sections:
  - Project Context
  - Business Rules
  - API Conventions
  - Seed Data
  - Frontend
  - AI-Assisted Development
  - Tech Stack
  - Repository Layout
  - Quality Gates
  - Project Workflow
  - Governance: added Versioning Policy and Compliance Review sub-points (required by
    the constitution workflow; derived from the supplied Governance and Quality Gates text)

Removed sections:
  - None ([SECTION_2_NAME] / [SECTION_3_NAME] replaced by the sections above)

Templates:
  - Not modified by this command; dependent templates read the constitution at runtime.

Follow-up TODOs:
  - None. No placeholders deferred.

Note: this report is review scratch material; remove it before committing the file.
-->

# Online Clothing & Shoes Store Constitution

## Project Context

A single online store (not a multi-vendor platform) selling clothes and shoes.
Built as a proof of concept for .NET. English only, LTR, EGP only.
Two roles: **Admin** (store owner) and **Customer**. Guests can browse and use the cart but
must log in to check out.

## Core Principles

### I. Clean Architecture (NON-NEGOTIABLE)

- The backend MUST consist of four projects: `Store.Domain`, `Store.Application`,
  `Store.Infrastructure`, `Store.API`.
- `Store.Domain` MUST have no dependencies on other projects or frameworks.
- Business rules MUST live in the Domain and Application layers, never in controllers.
- Database constraints (check constraints, unique indexes) are a safety net that duplicates
  critical rules; they MUST NOT be the only place a rule is enforced.
- The `Order` entity owns its status transitions and its `OrderItems`, and MUST reject
  invalid transitions.
- Keep it simple: no MediatR, no CQRS, no domain events, no message broker. Services call
  each other directly within a transaction.
- Application is organized by feature (Auth, Products, Categories, Cart, Orders, Dashboard),
  each with its DTOs, service, and validators.

**Rationale**: a small, layered structure keeps rules testable in isolation and keeps the
proof of concept easy to review.

### II. Data Access (NON-NEGOTIABLE)

- EF Core, Code First, SQL Server. No Dapper. No generic repository.
- `AppDbContext` MUST be accessed through the `IAppDbContext` interface.
- Entity configuration MUST use Fluent API in separate configuration classes. No data
  annotations on entities.
- Read-only queries MUST use `AsNoTracking` and project directly to DTOs with `Select`.
- Every schema change MUST go through a named migration. The database is never edited by hand.
- Exactly one stored procedure, `sp_GetDashboardStats`, created inside a migration via
  `migrationBuilder.Sql` and returning a single JSON result using `FOR JSON PATH`.

**Rationale**: one data-access approach keeps the schema reproducible and queries efficient.

### III. Security (NON-NEGOTIABLE)

- All queries MUST use LINQ. Raw SQL is allowed only through `FromSql` / `SqlQuery` with
  interpolated parameters.
- No `FromSqlRaw`, `ExecuteSqlRaw`, or any string-concatenated SQL anywhere.
- The stored procedure MUST use plain parameters and no dynamic SQL.
- Sorting and filtering options MUST come from predefined enums, never from raw user strings.
- Passwords MUST be hashed with ASP.NET Core `PasswordHasher` (no full ASP.NET Identity).
- Authentication uses JWT stored in an httpOnly cookie. Role-based authorization MUST be
  applied on every admin endpoint.
- Secrets (admin and demo passwords, JWT key, connection string) come from User Secrets in
  development and environment variables in production. They MUST NEVER be committed.

**Rationale**: these rules remove SQL injection paths and keep credentials out of source control.

### IV. Inventory & Concurrency (NON-NEGOTIABLE)

- Stock MUST be tracked per variant (product + color + size), never per product.
- `ProductVariants` has a `rowversion` column used for optimistic concurrency.
- Database check constraint: `Stock >= 0`.
- Checkout (create order, deduct stock, clear cart) MUST run in a single transaction.
- On concurrency conflict, checkout MUST fail cleanly with an `outOfStock` error; it never
  oversells.

**Rationale**: overselling is the most costly failure for a store; it is prevented in code
and guarded by the database.

### V. Data Integrity

- Money is `decimal(18,2)`.
- All dates are stored in UTC. The frontend displays them in Cairo time.
- `OrderItems` MUST store a snapshot (product name, color, size, unit price) at order time.
- Products with orders MUST be deactivated (`IsActive = false`), never deleted.

**Rationale**: historical orders must stay accurate after products or prices change.

## Business Rules

- Payment: cash on delivery only.
- Shipping fee: fixed 50 EGP from configuration, stored on each order.
- Maximum 5 units per variant per order, enforced in the backend and by a database check
  constraint.
- Order statuses: `Pending → Confirmed → Shipped → Delivered`; `Cancelled` is allowed from
  `Pending` or `Confirmed`.
- Customers may cancel only `Pending` orders. Admins confirm or cancel; they never edit order
  contents.
- Cancelling an order returns its quantities to stock.
- Low-stock alert threshold: 3 units or fewer (configuration value).
- Dashboard revenue counts `Delivered` orders only. Best sellers count all non-cancelled orders.
- Admin can mark products as featured (`IsFeatured`) for the home page.
- No discounts, no wishlist, no password reset, no online payment, no translations.

## API Conventions

- REST, under `/api`. Admin endpoints under `/api/admin`.
- Errors use one shape: `{ "code": "outOfStock", "message": "Not enough stock" }`.
- Error codes are defined once as constants in the backend.
- Input validation with FluentValidation. Global exception middleware. Swagger enabled in
  development.

## Seed Data

- The store must never open empty: default data is seeded automatically.
- Static lookup data (colors, sizes) uses `HasData`. Everything else uses runtime seeding
  (`UseSeeding`).
- Seeding is idempotent: it runs only when the target table is empty.
- Seeded data must be internally consistent (dates relative to the seed date, stock reflecting
  seeded orders).
- The exact seed content is defined in the Database spec.

## Frontend

- Next.js App Router with shadcn/ui.
- Product pages are Server Components; interactive parts are Client Components.
- Guest cart lives in localStorage and is merged on login.
- Middleware protects `/admin`, `/account`, and `/checkout`.

## AI-Assisted Development

All code is generated by an AI agent from the specs, plan, and tasks. The developer reviews
every change before it is committed.
For the critical areas below, the agent MUST also explain the implementation and add the tests
listed in Quality Gates before the task is considered done:

- Product variants and stock handling
- Checkout and concurrency handling
- Order status transitions (`Order` entity)

## Tech Stack

.NET 10 (LTS) · ASP.NET Core Web API · EF Core · SQL Server · FluentValidation · Next.js ·
shadcn/ui

## Repository Layout

```
/backend    .NET solution
/frontend   Next.js app
/specs      Spec Kit specifications
```

## Quality Gates

- Required tests: checkout concurrency (no overselling), order status transitions, and
  dashboard stored procedure results against seed data.
- Every pull request is checked against the Security and Data Access principles above.
- Coverage thresholds and exclusions are defined in Project Workflow.

## Project Workflow

These rules adapt the developer's global agent rules to this project. Where they conflict,
this section applies.

- **Work items**: every task is tracked as a GitHub Issue. Its number is the work item number
  used in branch names, commit messages, and PR titles (e.g. `feat #12 product variants
  schema`). If no issue exists for a task, stop and ask.
- **Coverage**: changed code must reach 80% line and branch coverage. Excluded from coverage:
  EF Core migrations, entity configurations, seed data, `Program.cs` and DI registration, and
  DTOs without logic.
- **Local database**: the local SQL Server instance used for development is not a real
  database. The agent may create and apply migrations and run seeding against it without
  asking. Any other database requires approval.
- **Approved dependencies** (may be used without asking):
  - Backend: `Microsoft.EntityFrameworkCore.SqlServer`, `Microsoft.EntityFrameworkCore.Design`,
    `Microsoft.EntityFrameworkCore.Tools`, `FluentValidation.DependencyInjectionExtensions`
    (validators are called explicitly; the deprecated `FluentValidation.AspNetCore`
    auto-validation is not used), `Microsoft.AspNetCore.Authentication.JwtBearer`,
    `Swashbuckle.AspNetCore`
  - Backend tests: `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`, `Shouldly`,
    `Microsoft.AspNetCore.Mvc.Testing`, `Testcontainers.MsSql`, `coverlet.collector`
  - Frontend: `next`, `react`, `react-dom`, `tailwindcss`, shadcn/ui components,
    `lucide-react`, `zod`, `react-hook-form`
  - Anything else requires approval.
- **Frontend/backend separation**: database and backend work belong to the backend side.
  Frontend work is always done in separate issues, branches, and PRs.

## Governance

- **Priority**: this constitution has priority for project decisions (architecture, data
  access, security, business rules, tech stack). The developer's global agent rules have
  priority for working practices (Git, commits, PRs, testing, scope, code style), except where
  the Project Workflow section adapts them.
- **Amendments**: any change to this constitution must be written here first, with the reason,
  before it is applied in code.
- **Versioning policy**: semantic versioning. MAJOR for removing or redefining a principle or
  rule in a backward-incompatible way; MINOR for a new principle or section, or materially
  expanded guidance; PATCH for clarifications and wording fixes.
- **Compliance review**: every pull request is reviewed against the Core Principles, with the
  Security and Data Access principles checked explicitly (see Quality Gates). Plans produced by
  `/speckit-plan` must pass a Constitution Check against this document.

**Version**: 1.3.0 | **Ratified**: 2026-11-24 | **Last Amended**: 2026-11-24
