# Feature Specification: Store Database & Seed Data

**Feature Branch**: `feat/001-database`

**Created**: 2026-11-24

**Status**: Draft

**Input**: User description: "Database for the online clothing & shoes store. Database and backend
side only; no frontend work. Create the complete database the store needs, so that the first time
the application runs, the store already contains realistic data (products with variants and
images, a demo customer with order history, an admin account), and the dashboard statistics are
computable from this data." (Full description, seed tables and acceptance criteria supplied by the
developer are reproduced in the requirements below.)

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Store structure is created from nothing (Priority: P1)

The developer points the application at an empty database and applies the schema. Every table,
rule, index and the dashboard statistics procedure is created in one step, with no manual edits.

**Why this priority**: nothing else (seeding, API, frontend) can exist without the schema.

**Independent Test**: apply the schema to an empty database and inspect that all entities,
constraints, indexes and the statistics procedure exist.

**Acceptance Scenarios**:

1. **Given** an empty database, **When** the schema is applied, **Then** it completes without
   errors and all entities listed under Key Entities exist with their rules and indexes.
2. **Given** a database already at the latest schema, **When** the schema is applied again,
   **Then** nothing changes and no error occurs.

---

### User Story 2 - The store never opens empty (Priority: P1)

The first time the application runs, the store already contains the admin account, a demo
customer with order history, the full catalog (categories, 18 products, 177 variants, 37 images)
and stock levels that already reflect the demo orders.

**Why this priority**: the proof of concept must be demonstrable immediately, and the dashboard
and later features are tested against this known data.

**Independent Test**: run the application once against a freshly created database and compare
the stored data with the Seed Data tables below; run it a second time and confirm row counts are
unchanged.

**Acceptance Scenarios**:

1. **Given** a freshly created database, **When** the application starts, **Then** all seed data
   in this spec exists exactly as listed.
2. **Given** a seeded database, **When** the application starts again, **Then** no row is
   duplicated or changed.
3. **Given** the account passwords are not configured, **When** the application tries to seed
   accounts, **Then** it stops with a clear message naming the missing setting and creates no
   account with a default or empty password.
4. **Given** an error occurs partway through seeding the catalog and demo orders, **When**
   seeding stops, **Then** no category, product, variant, image or order from that run is
   saved, and the next start seeds the whole group again.
5. **Given** the admin account already exists but the demo customer does not, **When** the
   application starts, **Then** only the demo customer is created.
6. **Given** two fresh databases seeded the same way, **When** their variant stock values are
   compared, **Then** every variant has the same stock in both.
7. **Given** seeding is switched off in configuration, **When** the application starts against
   a freshly created database, **Then** no accounts, categories, products, variants, images or
   orders are created (colors and sizes still exist, as part of the schema), and missing seed
   passwords cause no error.

---

### User Story 3 - The database refuses invalid data (Priority: P2)

Even if application code has a bug, the database itself rejects data that would break the
store: negative stock, out-of-range quantities, non-positive prices, duplicate variants, and
deleting variants that appear in orders.

**Why this priority**: it is the safety net behind the business rules (constitution I and IV).

**Independent Test**: attempt each invalid write directly against the database and confirm it
is rejected while valid writes succeed.

**Acceptance Scenarios**:

1. **Given** a variant, **When** its stock is set to -1, **Then** the write is rejected.
2. **Given** a cart item or order item, **When** its quantity is 0 or 6, **Then** the write is
   rejected; quantities 1 and 5 are accepted.
3. **Given** a product, **When** a second variant with the same color and size is added,
   **Then** the write is rejected.
4. **Given** a variant that appears in an order item, **When** it (or its product) is deleted,
   **Then** the delete is rejected and nothing is removed.
5. **Given** a product whose variants were never ordered, **When** it is deleted, **Then** its
   variants and images are removed with it.
6. **Given** an order, **When** it is deleted, **Then** its order items are removed with it.

---

### User Story 4 - Stock updates never silently overwrite each other (Priority: P2)

When two operations change the same variant's stock at the same moment, only one succeeds; the
other is told its data was stale, so the store can never oversell.

**Why this priority**: overselling is the most costly failure for the store (constitution IV).

**Independent Test**: load the same variant in two independent sessions, change stock in both,
save both; confirm the first save succeeds and the second is reported as a conflict.

**Acceptance Scenarios**:

1. **Given** two sessions holding the same version of a variant, **When** both save a stock
   change, **Then** one save succeeds and the other is detected as a concurrency conflict, and
   the stored stock equals the successful save's value.

---

### User Story 5 - The admin sees correct dashboard statistics (Priority: P3)

The admin's dashboard figures come from one call that, for a date range and a low-stock
threshold, returns revenue, order counts, best sellers and low-stock variants as one result.

**Why this priority**: valuable, but depends on schema and seed data being in place.

**Independent Test**: call the statistics procedure on the seeded database for the last 30 days
with threshold 3 and compare the result with the expected values below.

**Acceptance Scenarios**:

1. **Given** the seeded database, **When** statistics are requested for the last 30 days (from
   now minus 30 days to now) with threshold 3, **Then** the result is: revenue 5,200; total orders 8; Delivered 3, Shipped 1,
   Confirmed 1, Pending 2, Cancelled 1; best sellers start with Classic Cotton T-Shirt (4 units)
   then Basic Ribbed Top (3 units); low-stock variants are exactly Classic Cotton T-Shirt
   Black/M (1), Floral Midi Dress Red/S (2), Everyday White Sneakers White/43 (3), Leather Derby
   Shoes Brown/44 (1).
2. **Given** a date range containing no orders, **When** statistics are requested, **Then**
   revenue is 0, counts are 0, best sellers is an empty list, and low-stock variants are still
   returned (they do not depend on dates).
3. **Given** no threshold is supplied, **When** statistics are requested, **Then** threshold 3
   is used.

### Edge Cases

- A variant with stock 0 is out of stock, not low stock: it is never in the low-stock list.
- A combination with no variant row does not exist for that product (e.g. Linen Summer Shirt in
  size S) and is never shown as out of stock.
- A cancelled order counts in total orders and in the Cancelled count, but never in revenue or
  best sellers, and its quantities are not deducted from seeded stock.
- Best sellers with equal quantities are ordered by product name (A→Z), so the top 5 is stable
  (8 products tie at 1 unit in the seed data).
- The date range includes its start and excludes its end: an order created exactly at the start
  is counted, one created exactly at the end is not. ORD-0008 is created at the moment of
  seeding, which is earlier than any later "now", so it is inside "the last 30 days".
- A category that has products, or has child categories, cannot be deleted.
- Products already exist (e.g. the admin added one): the catalog and demo-order group is not
  seeded at all, even if other catalog tables are empty.
- No products exist but categories do (e.g. the admin created a category and then deleted its
  products): the catalog group runs, conflicts with an existing category slug, rolls back
  completely and reports the error.
- The demo customer account is missing when the catalog group runs (e.g. its password is not
  configured): the group fails and nothing from it is saved.
- Outside Development, the database schema is behind (migrations pending): the application
  starts, warns with the pending count, and does not seed until the schema is applied.
- An image file listed in the seed is missing from the seed-images folder: seeding still stores
  the image record and reports the missing file, so the catalog is never left half-seeded.

## Requirements *(mandatory)*

### Functional Requirements

**Schema**

- **FR-001**: The database MUST be created and changed only through versioned, named schema
  migrations that apply cleanly to an empty database.
- **FR-002**: The database MUST store every entity listed under Key Entities with the listed
  attributes.
- **FR-003**: Email (users), slug (categories, products), name (colors, sizes), SKU (variants)
  and order number (orders) MUST each be unique.
- **FR-004**: Each product + color + size combination MUST exist at most once; each user +
  variant at most once in cart items; each variant at most once per order.
- **FR-005**: A category MAY reference an optional parent category. The database stores only
  this reference; the one-level nesting limit is an application rule (enforced by a later
  feature) and is not enforced by the database. Seed data respects it.
- **FR-006**: Money values MUST be stored with two decimal places; all dates MUST be stored in
  UTC.

**Safety-net rules (enforced by the database, duplicating application rules)**

- **FR-007**: Variant stock MUST never be negative.
- **FR-008**: Cart item and order item quantity MUST be between 1 and 5 inclusive.
- **FR-009**: Product price MUST be greater than 0.
- **FR-010**: Deleting a variant referenced by an order item MUST be rejected.
- **FR-011**: Deleting a product MUST remove its variants and images, and MUST be rejected when
  any of its variants is referenced by an order item.
- **FR-012**: Deleting an order MUST remove its order items.
- **FR-013**: Deleting a category that has products or child categories MUST be rejected.
- **FR-014**: Each variant MUST carry a concurrency token so that conflicting concurrent updates
  are detected rather than overwritten.
- **FR-015**: Lookups MUST be indexed for: products by category; active + featured products
  (home page); orders by customer; orders by status; orders by creation date.

**Dashboard statistics**

- **FR-016**: Exactly one stored statistics procedure MUST exist. It takes a start date-time and
  an end date-time, both in UTC (start inclusive, end exclusive), and a low-stock threshold
  (default 3), and returns a single JSON result containing:
  - total revenue: sum of order totals of **Delivered** orders created in the range;
  - total orders: number of orders of any status created in the range;
  - orders per status: a count for each of Pending, Confirmed, Shipped, Delivered, Cancelled
    (zero counts included);
  - top 5 best sellers: product id, name and units sold, from all **non-cancelled** orders
    created in the range, ordered by units descending then product name;
  - low-stock variants: product name, color, size, SKU and stock for every variant with stock
    from 1 to the threshold, regardless of the date range.
- **FR-017**: The statistics procedure MUST use only its parameters and no dynamically built
  query text.

**Seeding**

- **FR-018**: On application start, seed data MUST be created automatically, in every
  environment. Colors and sizes are part of the schema itself; all other seed data is created at
  runtime by the seeder. Exception: outside Development, if schema migrations are pending, the
  application still starts, logs a warning with the number of pending migrations, and skips
  seeding.
- **FR-018a**: A configuration switch (`Seed:Enabled`, default on) MUST control runtime seeding.
  When it is off, the seeder does nothing at all: no group runs and no seed setting (such as a
  password) is read or required.
- **FR-019**: Runtime seeding MUST run in two independent groups and be idempotent:
  - **Accounts**: each seed account is created only if no user with that email exists.
  - **Catalog and demo orders** (categories, products, variants, images, demo orders and their
    items): seeded only when there are no products at all, inside a single transaction. If
    any part fails, nothing from the group is saved.
  The accounts group runs first, because demo orders belong to the demo customer.
- **FR-020**: Account passwords MUST come from configuration and be stored only as hashes. If a
  password is not configured, account seeding MUST fail with a clear message.
- **FR-021**: Seeded order dates MUST be relative to the moment of seeding (N days before it).
- **FR-022**: Seeded stock MUST equal the intended final values: the special values listed below,
  and 8–20 for every other variant, after deducting all non-cancelled demo orders. The cancelled
  order MUST NOT be deducted. Stock values MUST be deterministic: seeding the same data always
  produces the same stock for every variant (no randomness). The exact rule is defined in the
  plan.
- **FR-023**: Seeded order items MUST carry snapshots (product name, color name, size name, unit
  price) matching the catalog at seed time, and each order's subtotal, shipping fee and total
  MUST match the Demo Orders table.

### Seed Data

**Accounts**

| Email | Full name | Role |
|---|---|---|
| `admin@store.local` | Store Admin | Admin |
| `customer@store.local` | Demo Customer | Customer |

**Colors (8)** — name, hex, code used in SKUs:
Black #000000 BLK · White #FFFFFF WHT · Navy #1F2A44 NVY · Gray #8E8E8E GRY ·
Beige #D8C8A8 BEG · Red #C0392B RED · Blue #2E6DB4 BLU · Brown #6B4423 BRN

**Sizes (14)** — name (display order): XS (1), S (2), M (3), L (4), XL (5), XXL (6),
38 (10), 39 (11), 40 (12), 41 (13), 42 (14), 43 (15), 44 (16), 45 (17)

**Categories** — parent: children (SKU code)

- Men: T-Shirts (MTS), Shirts (MSH), Pants (MPT)
- Women: Dresses (WDR), Tops (WTP), Pants (WPT)
- Shoes: Sneakers (SSN), Formal (SFM), Sandals (SSD)

Products belong to child categories only.

**Products (18, ★ = featured, all active)**

| # | Category | Product | Price (EGP) | Colors | Sizes | Variants |
|---|---|---|---|---|---|---|
| 001 | Men / T-Shirts | Classic Cotton T-Shirt ★ | 350 | Black, White, Navy | S–XL | 12 |
| 002 | Men / T-Shirts | Graphic Oversized Tee | 450 | Black, Gray | S–XXL | 10 |
| 003 | Men / Shirts | Oxford Button-Down Shirt ★ | 750 | White, Blue | S–XL | 8 |
| 004 | Men / Shirts | Linen Summer Shirt | 850 | Beige, White | M–XL | 6 |
| 005 | Men / Pants | Slim Fit Chinos | 900 | Beige, Navy, Black | S–XL | 12 |
| 006 | Men / Pants | Classic Denim Jeans ★ | 1100 | Blue, Black | S–XXL | 10 |
| 007 | Women / Dresses | Floral Midi Dress ★ | 1250 | Red, Navy | XS–L | 8 |
| 008 | Women / Dresses | Casual Shirt Dress | 1050 | Beige, Black | S–L | 6 |
| 009 | Women / Tops | Basic Ribbed Top | 300 | White, Black, Beige | XS–L | 12 |
| 010 | Women / Tops | Satin Blouse | 650 | White, Red | S–L | 6 |
| 011 | Women / Pants | Wide Leg Trousers ★ | 950 | Black, Beige | XS–L | 8 |
| 012 | Women / Pants | High Waist Jeans | 1050 | Blue | XS–XL | 5 |
| 013 | Shoes / Sneakers | Everyday White Sneakers ★ | 1600 | White | 38–45 | 8 |
| 014 | Shoes / Sneakers | Running Shoes | 1900 | Black, Gray | 39–45 | 14 |
| 015 | Shoes / Formal | Leather Derby Shoes | 2200 | Black, Brown | 40–45 | 12 |
| 016 | Shoes / Formal | Classic Loafers | 1800 | Brown, Black | 40–44 | 10 |
| 017 | Shoes / Sandals | Leather Sandals | 700 | Brown, Black | 38–44 | 14 |
| 018 | Shoes / Sandals | Everyday Slides | 400 | Black, White | 38–45 | 16 |
| | | | | **37 colors** | | **177** |

- Every color × size in the ranges above exists as a variant: **177 variants**.
- SKU: `<category code>-<product #>-<color code>-<size>`, e.g. `MTS-001-BLK-M`, `SSN-013-WHT-42`.
- Images: one per product color, **37 images**, each the main image for its color with display
  order 1, file `<product-slug>-<color>.jpg` from the project's seed-images folder (e.g.
  `classic-cotton-t-shirt-black.jpg`). No general (color-less) images are seeded.

**Special stock values (final, after order deductions)**

| Product | Variant | Stock |
|---|---|---|
| Classic Cotton T-Shirt | Black / M | 1 |
| Classic Cotton T-Shirt | White / L | 0 |
| Floral Midi Dress | Red / S | 2 |
| Everyday White Sneakers | White / 42 | 0 |
| Everyday White Sneakers | White / 43 | 3 |
| Leather Derby Shoes | Brown / 44 | 1 |

All other variants: final stock 8–20, chosen by a deterministic rule (defined in the plan),
never at random.

**Demo orders (all for the demo customer, shipping fee 50 each)**

| Order | Days ago | Status | Items (unit price) | Subtotal | Total |
|---|---|---|---|---|---|
| ORD-0001 | 28 | Delivered | Classic Denim Jeans Blue/M ×1 (1100), Classic Cotton T-Shirt White/M ×2 (350) | 1800 | 1850 |
| ORD-0002 | 24 | Delivered | Everyday White Sneakers White/41 ×1 (1600) | 1600 | 1650 |
| ORD-0003 | 20 | Cancelled | Floral Midi Dress Navy/M ×1 (1250) | 1250 | 1300 |
| ORD-0004 | 15 | Delivered | Oxford Button-Down Shirt White/L ×1 (750), Slim Fit Chinos Beige/L ×1 (900) | 1650 | 1700 |
| ORD-0005 | 9 | Shipped | Leather Derby Shoes Black/42 ×1 (2200) | 2200 | 2250 |
| ORD-0006 | 5 | Confirmed | Basic Ribbed Top Black/S ×3 (300), Wide Leg Trousers Black/S ×1 (950) | 1850 | 1900 |
| ORD-0007 | 2 | Pending | Graphic Oversized Tee Gray/L ×1 (450), Everyday Slides Black/42 ×1 (400) | 850 | 900 |
| ORD-0008 | 0 | Pending | Classic Cotton T-Shirt Black/L ×2 (350) | 700 | 750 |

All demo orders ship to the demo customer's name and phone at one fixed Cairo address.

### Key Entities *(include if feature involves data)*

- **User**: full name, unique email, phone, password hash, role (Admin or Customer), created date.
- **Category**: name, unique slug, optional parent category (the one-level nesting limit is an
  application rule, see FR-005).
- **Product**: category, name, unique slug, description, price (> 0), active flag, featured flag,
  created date. Price belongs to the product, never to a variant.
- **Color**: unique name, hex code.
- **Size**: unique name, display order.
- **Product Variant**: product, color, size (unique together), unique SKU, stock (≥ 0),
  concurrency token. The unit of stock.
- **Product Image**: product, optional color (none = general image), URL, display order,
  main-image flag.
- **Cart Item**: user, variant (unique together), quantity (1–5), date added. There is no
  separate cart entity.
- **Order**: unique order number, customer, status (Pending, Confirmed, Shipped, Delivered,
  Cancelled), subtotal, shipping fee, total, shipping name, phone, city, address line, notes
  (optional), created date, updated date.
- **Order Item**: order, variant (unique per order), snapshot of product name, color name, size
  name and unit price, quantity (1–5).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Creating the database from empty and starting the application once produces a
  usable store with 0 manual steps beyond supplying configuration.
- **SC-002**: After the first start, the store holds exactly 2 accounts, 8 colors, 14 sizes,
  12 categories, 18 products, 177 variants, 37 images and 8 orders; after any number of further
  starts, those counts are unchanged.
- **SC-003**: 100% of the invalid writes in User Story 3 are rejected by the database, and 100%
  of the matching valid writes succeed.
- **SC-004**: In a concurrent stock update test, 0 updates are silently lost: exactly one
  succeeds and the other is reported as a conflict.
- **SC-005**: The dashboard statistics for the last 30 days with threshold 3 match every value in
  User Story 5, scenario 1, in a single call.
- **SC-006**: No account password appears in source code or committed configuration.
- **SC-007**: Seeding two fresh databases produces identical stock for all 177 variants.
- **SC-008**: A seeding failure injected partway through the catalog group leaves 0 catalog or
  order rows behind.
- **SC-009**: With seeding switched off, starting the application on a fresh database leaves 0
  rows in every runtime-seeded table.

## Assumptions

- Revenue is the sum of order **totals** (shipping included); this is what makes the expected
  revenue 5,200.
- Orders are placed in a date range by their created date; there is no separate delivered date.
- "Now" for the dashboard is the current UTC time supplied by the caller; the procedure does
  not read the clock itself.
- Colors and sizes are part of the schema, so they exist before either seeding group runs.
- Product numbers (001–018) in SKUs are global across the catalog, in the order of the products
  table; category, color and size codes are as listed in Seed Data.
- Product descriptions are short generated texts (one or two sentences per product).
- Seeded product creation dates are earlier than the oldest demo order (e.g. 60 days before
  seeding), so the catalog predates its orders.
- Image URLs are stored as relative paths to where the API serves the seed images; serving them
  is out of scope here.
- The demo customer's phone and shipping address are fixed demo values (Cairo, Egypt).
- A user with orders cannot be deleted; deleting a user removes their cart items. User
  management is out of scope; this only fixes the database behavior.
- Deleting a variant removes it from any carts.
- The database does not check that total = subtotal + shipping fee; that rule belongs to the
  order logic (a later feature). Seeded orders satisfy it.
- Out of scope: API endpoints, authentication logic, business services, all frontend work, and
  producing the image files (they are provided).
