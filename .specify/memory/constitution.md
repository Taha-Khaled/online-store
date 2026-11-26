<!--
Sync Impact Report
==================
Version change: 1.5.0 → 1.7.0 (version as supplied by the developer; no 1.6.0 exists in
this repository)
  - Bump: MINOR. The seeding mechanism is redefined (UseSeeding → explicit seeder), a seeding
    switch is added, and the approved dependencies change. No principle is changed.

Modified principles:
  - None

Modified sections:
  - Seed Data: `UseSeeding` replaced by an explicit `DatabaseSeeder` call at startup in every
    environment (after `MigrateAsync`, which runs in Development only); new `Seed:Enabled`
    switch (default true; false = seeder does nothing); per-table emptiness rule replaced by
    a reference to the seeding groups in the Database spec.
  - Project Workflow → Approved dependencies: added `Microsoft.EntityFrameworkCore`
    (Store.Application); removed `Testcontainers.MsSql`; integration tests use the local SQL
    Server.
  - Reasons (from the 001-database plan): Production never runs migrations at startup, so
    EF's UseSeeding hook would never fire there; Docker is not available for Testcontainers;
    `IAppDbContext` exposes `DbSet<T>`.

Added sections:
  - None

Removed sections:
  - None

1.7.0 → 1.7.1 (PATCH):
  - Project Workflow → Frontend/backend separation: "separate issues" → "separate tasks", to
    match the Work items rule (no external issues).

1.7.1 → 1.7.2 (PATCH):
  - Core Principles → III. Security: clarified that the enum rule covers the set of sort and
    filter options; filter values (e.g. color or size names) may be user input when compared as
    query parameters, never concatenated into SQL. Reason: 002-backend-core analysis finding C1
    (product list filters by color and size names).

1.7.2 → 1.7.3 (PATCH):
  - Core Principles → IV. Inventory & Concurrency: a checkout concurrency conflict is retried; if
    stock is no longer enough the checkout fails cleanly with `cartChanged` listing the affected
    items; if every attempt conflicts, `checkoutConflict`. Replaces the single `outOfStock` error.
    Reason: 003-backend-shopping spec (FR-016, FR-018); `cartChanged` with the affected items lets
    the customer fix the cart.

1.7.3 → 1.7.4 (PATCH):
  - Core Principles → I. Clean Architecture: inside each Application feature folder, files go in
    `Requests/`, `Responses/`, `Validators/` and `Services/`, with namespaces following the
    folders; every new file in every future feature follows this layout. Reason: developer
    decision during 003-backend-shopping (task T035 moved the existing Auth, Categories, Products
    and Cart files).

1.7.4 → 1.7.5 (PATCH):
  - Project Workflow → Work items: work items are written `#F<feature>-<task>` (e.g.
    `feat #F004-T005 add variant endpoints`); commits outside the task list use `#F<feature>`
    (e.g. `docs #F004 ...`). Reason: GitHub reads `fix #004` as "fixes #4" and closes that issue
    (commit `fix #003-T034 ...` closed issue #3). Earlier commits stay as they are.
  - Project Workflow → Work items: "there are no external issues" replaced by: each feature is
    tracked by one GitHub issue, and the feature's PR description includes `Closes #<issue>`.

Follow-up TODOs:
  - None.

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
  each with its DTOs, service, and validators. Inside each feature folder, files go in
  `Requests/`, `Responses/`, `Validators/` and `Services/`, and namespaces follow the folders.
  Every new file in every future feature follows this layout.

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
- The set of sort and filter options MUST come from predefined enums, never from raw user
  strings. Filter values (such as color or size names) may be user input when they are compared
  as query parameters; they are never concatenated into SQL.
- Passwords MUST be hashed with ASP.NET Core `PasswordHasher` (no full ASP.NET Identity).
- Authentication uses a JWT. The API MUST read it only from the `Authorization: Bearer` header
  and MUST NEVER set cookies. Role-based authorization MUST be applied on every admin endpoint.
- The Next.js app acts as a backend-for-frontend (BFF): it MUST store the JWT in an httpOnly,
  Secure cookie on its own domain and forward it to the API from the server. The browser MUST
  NEVER call the API directly, except to load public product images.
- Secrets (admin and demo passwords, JWT key, connection string) come from User Secrets in
  development and environment variables in production. They MUST NEVER be committed.

**Rationale**: these rules remove SQL injection paths and keep credentials out of source control.

### IV. Inventory & Concurrency (NON-NEGOTIABLE)

- Stock MUST be tracked per variant (product + color + size), never per product.
- `ProductVariants` has a `rowversion` column used for optimistic concurrency.
- Database check constraint: `Stock >= 0`.
- Checkout (create order, deduct stock, clear cart) MUST run in a single transaction.
- On a concurrency conflict, checkout re-reads the stock and retries. If the stock is no longer
  enough, it MUST fail cleanly with a `cartChanged` error listing the affected items; if every
  attempt conflicts, with `checkoutConflict`. It never oversells.

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
- Static lookup data (colors, sizes) uses `HasData`. Everything else is seeded at runtime by a
  `DatabaseSeeder` called explicitly at application startup in every environment (after
  `MigrateAsync`, which runs in Development only). EF Core `UseSeeding` is not used.
- Seeding is controlled by the configuration switch `Seed:Enabled` (default `true`). When it is
  `false`, the seeder does nothing at all.
- Seeding is idempotent; the seeding groups and their conditions are defined in the Database
  spec.
- Seeded data must be internally consistent (dates relative to the seed date, stock reflecting
  seeded orders).
- The exact seed content is defined in the Database spec.

## Frontend

- Next.js App Router with shadcn/ui.
- Product pages are Server Components; interactive parts are Client Components.
- All API calls go through Next.js server code (Server Components, Server Actions, or Route
  Handlers), per the BFF rule.
- The API's image host is allowed in the `next/image` configuration.
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

## Deployment

- Source code lives in a local Git repository with a private GitHub remote. Pushing to GitHub
  happens only when the developer asks.
- Everything is hosted on MonsterASP.NET: the API, the SQL Server database, and the Next.js app
  (Node.js), each as its own site.
- HTTPS MUST be enabled on both sites before release (required for Secure cookies).
- Production secrets are set in the hosting control panel, never in committed files.
- Applying migrations to the hosted database always requires the developer's approval.
- The design MUST NOT depend on the API and frontend sharing a domain.

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

- **Work items**: each feature is tracked by one GitHub issue, and the feature's PR description
  includes `Closes #<issue>`. The work item number is the Spec Kit task ID
  from the feature's `tasks.md`, written with `F` and its feature number: `#F<feature>-<task>`
  (e.g. `feat #F004-T005 add variant endpoints`). Commits that belong to a feature but to no
  task in its list use `#F<feature>` (e.g. `docs #F004 ...`). The `F` stops GitHub from reading
  `fix #004` as "fixes #4" and closing that issue; earlier commits keep their old format.
  Branches use the feature number (e.g. `feat/001-database`). If a change has no matching task,
  stop and ask.
- **Coverage**: changed code must reach 80% line and branch coverage. Excluded from coverage:
  EF Core migrations, entity configurations, seed data, `Program.cs` and DI registration, and
  DTOs without logic.
- **Local database**: the local SQL Server instance used for development is not a real
  database. The agent may create and apply migrations and run seeding against it without
  asking. Any other database requires approval.
- **Approved dependencies** (may be used without asking):
  - Backend: `Microsoft.EntityFrameworkCore` (also in Store.Application, for `IAppDbContext`),
    `Microsoft.EntityFrameworkCore.SqlServer`, `Microsoft.EntityFrameworkCore.Design`,
    `Microsoft.EntityFrameworkCore.Tools`, `FluentValidation.DependencyInjectionExtensions`
    (validators are called explicitly; the deprecated `FluentValidation.AspNetCore`
    auto-validation is not used), `Microsoft.AspNetCore.Authentication.JwtBearer`,
    `Swashbuckle.AspNetCore`
  - Backend tests: `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`, `Shouldly`,
    `Microsoft.AspNetCore.Mvc.Testing`, `coverlet.collector`. Integration tests run against the
    local SQL Server instance (one uniquely named database per test, dropped afterwards).
  - Frontend: `next`, `react`, `react-dom`, `tailwindcss`, shadcn/ui components,
    `lucide-react`, `zod`, `react-hook-form`
  - Anything else requires approval.
- **Frontend/backend separation**: database and backend work belong to the backend side.
  Frontend work is always done in separate tasks, branches, and PRs.

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

**Version**: 1.7.5 | **Ratified**: 2026-11-24 | **Last Amended**: 2026-11-26
