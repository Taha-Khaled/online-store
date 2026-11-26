# Feature Specification: Backend Shopping — Cart, Checkout & Customer Orders

**Feature Branch**: `feat/003-backend-shopping`

**Created**: 2026-11-25

**Status**: Draft

**Input**: User description: "Backend shopping for the online clothing & shoes store: the customer's
cart, checkout, and the customer's own orders. Backend only; no frontend work. Builds on
001-database and 002-backend-core (auth, error shape, catalog, TimeProvider, test fixtures)."
(The full description, the 13 acceptance criteria and the out-of-scope list supplied by the
developer are reproduced in the requirements below.)

**Tracking**: GitHub issue #7 (whole feature). Commit work items use `#003-T0xx`.

## Clarifications

### Session 2026-11-25

- Q: Constitution IV required `outOfStock` on a checkout conflict; this feature retries and uses
  `cartChanged` / `checkoutConflict`. Which wins? → A: The constitution is amended (1.7.3) to the
  retry with `cartChanged` / `checkoutConflict` (FR-018).
- Q: Error code when all cancel attempts conflict? → A: 409 `orderConflict` (FR-025).

### Session 2026-11-26

- Q: Parallel adds of the same variant by the same customer can lose an increment (cart lines have
  no concurrency token). Accept? → A: Yes. The cart still has one line, the quantity stays 1–5, it
  is never a 500, and checkout re-validates everything.
- Q: Must order numbers follow commit order? → A: No; they increase in the order they are
  assigned, and gaps are allowed (FR-020).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Customer keeps a cart on the server (Priority: P1)

A logged-in customer adds variants to a cart, changes quantities, removes items, and always sees
current prices, availability and totals, so they know whether they can check out.

**Why this priority**: checkout is built from the server cart; nothing else in this feature works
without it.

**Independent Test**: log in as a customer, add/update/remove items and read the cart on the
seeded store.

**Acceptance Scenarios**:

1. **Given** an empty cart, **When** adding Basic Ribbed Top White/S ×2, **Then** the cart has 1
   item, subtotal 600, shipping 50, total 650, and can be checked out.
2. **Given** that cart, **When** adding the same variant ×3, **Then** the quantity is 5; **When**
   adding 1 more, **Then** the response is `maxQuantityExceeded` and the cart is unchanged.
3. **Given** Classic Cotton T-Shirt Black/M has stock 1, **When** adding it ×2, **Then** the
   response is 409 `insufficientStock` with `maxQuantity` 1; adding ×1 succeeds.
4. **Given** White/L has stock 0, **When** adding it, **Then** the response is 409
   `insufficientStock` with `maxQuantity` 0.
5. **Given** a cart item whose product is deactivated afterwards, **When** the cart is read,
   **Then** the item shows `unavailable` and the cart cannot be checked out.
6. **Given** a product whose price changes after it was added, **When** the cart is read, **Then**
   the item shows the new price.
7. **Given** a cart with 30 different items, **When** adding a 31st variant, **Then** the response
   is `cartFull`.

---

### User Story 2 - Customer checks out without overselling (Priority: P1)

A customer turns the cart into a cash-on-delivery order with shipping details. Stock is deducted,
the order keeps a snapshot of what was bought, and the cart is emptied — all or nothing. When
several customers buy the last units at the same moment, the store never sells more than it has.

**Why this priority**: this is the store's revenue path and its most costly failure (overselling,
constitution IV).

**Independent Test**: build carts for one or several customers and check out sequentially and in
parallel; compare orders, stock and carts.

**Acceptance Scenarios**:

1. **Given** a cart with Basic Ribbed Top White/S ×2, **When** checking out with valid shipping
   details, **Then** a Pending order is created with total 650, an order number after ORD-0008,
   and an item snapshot (name, color, size, price 300, quantity 2); the variant's stock drops by 2;
   the cart is empty; the response is 201 with the full order.
2. **Given** two customers each with Classic Cotton T-Shirt Black/M ×1 (stock 1), **When** both
   check out at the same moment, **Then** exactly one order is created, the other gets 409
   `cartChanged`, and the stock ends at 0.
3. **Given** ten customers each with the same variant ×1 (stock 3), **When** all check out at the
   same moment, **Then** exactly 3 orders are created, 7 get 409, and the stock ends at 0.
4. **Given** one customer's cart, **When** checkout is sent twice at the same moment, **Then**
   exactly one order is created.
5. **Given** a cart with a deactivated product, **When** checking out, **Then** the response is 409
   `cartChanged` listing it, and no order is created and no stock changes.
6. **Given** an empty cart, **When** checking out, **Then** the response is 400 `cartEmpty`.
7. **Given** a missing city or a phone containing letters, **When** checking out, **Then** the
   response is 400 with field errors.

---

### User Story 3 - Customer sees and cancels their own orders (Priority: P2)

A customer lists their orders, opens one with its items and totals, and cancels it while it is
still Pending; the items go back into stock.

**Why this priority**: needed for a complete shopping experience, but not to place an order.

**Independent Test**: log in as the demo customer, list and open orders, cancel Pending and
non-Pending orders, and check stock.

**Acceptance Scenarios**:

1. **Given** the demo customer, **When** listing orders, **Then** 8 orders return, ORD-0008 first.
2. **Given** another customer's order id, **When** opening or cancelling it, **Then** the response
   is 404 `orderNotFound`.
3. **Given** ORD-0008 (Pending, Classic Cotton T-Shirt Black/L ×2), **When** the demo customer
   cancels it, **Then** it becomes Cancelled, its updated date changes, Black/L's stock goes up by
   2, and the response is the updated order.
4. **Given** ORD-0008 already cancelled, or ORD-0006 (Confirmed), or ORD-0001 (Delivered), **When**
   cancelling, **Then** the response is 409 `orderCannotBeCancelled` and no stock changes.

---

### User Story 4 - Guest cart is merged after login (Priority: P2)

A shopper who filled a cart as a guest logs in; the guest cart is merged into the server cart, and
the shopper is told what could not be added or was reduced.

**Why this priority**: the constitution keeps the guest cart in the browser and merges it on
login; without merging, guest shoppers lose their cart.

**Independent Test**: merge lists with valid, out-of-stock and unknown variants into empty and
non-empty carts; merge the same list twice.

**Acceptance Scenarios**:

1. **Given** an empty cart, **When** merging [Black/M ×1, White/L ×1, unknown variant ×1], **Then**
   only Black/M is added; White/L is reported out of stock and the unknown variant as skipped.
2. **Given** that result, **When** sending the same merge again, **Then** the cart is unchanged.
3. **Given** a cart with a variant ×3, **When** merging the same variant ×2, **Then** the quantity
   stays 3 (the higher one, never the sum).

---

### User Story 5 - Only customers shop (Priority: P3)

Every shopping endpoint is for logged-in customers only.

**Why this priority**: a guard on all stories; admins and anonymous callers must not shop.

**Independent Test**: call every endpoint without a token and with the admin's token.

**Acceptance Scenarios**:

1. **Given** no token, **When** calling any endpoint of this feature, **Then** the response is 401.
2. **Given** the admin's token, **When** calling any endpoint of this feature, **Then** the response
   is 403 `forbidden`.

---

### Edge Cases

- Cart item id that belongs to another customer or does not exist: 404 `cartItemNotFound`; it is
  never changed.
- Quantity 0, negative, or above 5 in a request: 400 validation error.
- A variant whose stock dropped below the cart quantity: status `quantityReduced`, its
  `maxQuantity` shows what can still be bought, and it does not count in the subtotal.
- A product deactivated, or a variant out of stock, after it was added: the item stays in the cart
  with status `unavailable` / `outOfStock` until the customer removes it; checkout fails while it
  is there.
- Updating the quantity of an item whose product is now inactive: 404 `variantNotFound`.
- Two add requests for the same variant at the same moment: the cart ends with one line for that
  variant (no duplicate lines, never a 500); one increment may be lost, the quantity stays 1–5,
  and checkout re-validates everything (accepted, Clarifications 2026-11-26).
- Merge list with the same variant twice: treated as one entry with the higher quantity.
- Merge list over 30 entries, or an entry with quantity outside 1–5: 400 validation error.
- Checkout when stock is enough on the first check but taken by another checkout before saving:
  stock is re-read and checked again (up to 3 attempts in total).
- Cancelling at the same moment as a checkout of the same variant: both succeed and stock stays
  correct.
- Order list page beyond the last: empty list with correct totals; page size above 50: 400.

## Requirements *(mandatory)*

### Functional Requirements

**Access**

- **FR-001**: Every endpoint in this feature MUST require a logged-in user with role Customer:
  401 without a valid token, 403 `forbidden` for any other role.
- **FR-002**: A customer MUST only see and change their own cart and orders. Another customer's
  order returns 404 `orderNotFound`; another customer's cart item returns 404 `cartItemNotFound`.

**Cart**

- **FR-003**: `GET /api/cart` MUST return, per item: item id, variant id, product name, slug, color
  name, size name, current unit price, line total, the main image URL for that color, quantity,
  `maxQuantity` = min(stock, 5), and a status: `available`, `quantityReduced` (stock above 0 but
  below the cart quantity), `outOfStock` (stock 0), or `unavailable` (product inactive).
- **FR-004**: The cart MUST also return the item count, the subtotal of `available` items, the
  shipping fee, the total, and `canCheckout` (true only when the cart has items and every item is
  `available`).
- **FR-005**: Prices in the cart MUST always be the product's current price.
- **FR-006**: `POST /api/cart/items` MUST accept a variant id and a quantity (1–5). If the variant
  is already in the cart, the quantities add up. The resulting quantity MUST be ≤ 5
  (`maxQuantityExceeded`, 400) and ≤ current stock (`insufficientStock`, 409, including the current
  `maxQuantity`). Unknown variant or inactive product → 404 `variantNotFound`. Returns the updated
  cart.
- **FR-007**: `PUT /api/cart/items/{itemId}` MUST set the quantity (1–5) with the same stock check.
  Returns the updated cart.
- **FR-008**: `DELETE /api/cart/items/{itemId}` MUST remove the item and return the updated cart.
- **FR-009**: `DELETE /api/cart` MUST empty the cart and return 204.
- **FR-010**: A cart MUST hold at most 30 different items (`cartFull`, 409).
- **FR-011**: Cart operations MUST NOT reserve or change stock.

**Merge**

- **FR-012**: `POST /api/cart/merge` MUST accept up to 30 entries of { variant id, quantity 1–5 }.
  For each entry, the resulting quantity is the higher of the existing cart quantity and the guest
  quantity (never the sum), capped at min(5, stock). Merging the same list twice changes nothing.
- **FR-013**: Entries that cannot be added (unknown variant, inactive product, out of stock, cart
  full) MUST be skipped, never an error. The response MUST be the updated cart plus a list of the
  skipped or reduced entries with the reason (`variantNotFound`, `outOfStock`, `cartFull`,
  `quantityReduced`).

**Checkout**

- **FR-014**: `POST /api/orders` MUST accept shipping name (2–100), phone (same rule as
  registration: up to 20, ASCII digits with an optional leading `+`), city (2–100), address line
  (5–300) and optional notes (up to 500). Payment is always cash on delivery.
- **FR-015**: The order MUST be created from the customer's server cart. An empty cart returns 400
  `cartEmpty`.
- **FR-016**: Before creating anything, every item MUST be checked against current data. If any
  item is not `available`, nothing is created and the response is 409 `cartChanged` listing each
  affected item with its status and current `maxQuantity`.
- **FR-017**: In one transaction, checkout MUST: deduct stock for every item, create the order with
  status Pending, store a snapshot per item (product name, color name, size name, current unit
  price, quantity), store the shipping fee (50 EGP from configuration), subtotal and total, and
  empty the cart. If any step fails, nothing is saved.
- **FR-018**: Stock changes MUST use the variant's concurrency token. On a conflict, checkout MUST
  re-read the current stock and retry, up to 3 attempts in total. If the stock is no longer enough,
  it returns 409 `cartChanged`; if all 3 attempts conflict, 409 `checkoutConflict` (constitution
  IV as amended in 1.7.3).
- **FR-019**: Two checkouts of the same cart at the same moment MUST create at most one order; the
  other gets 400 `cartEmpty` or 409, never a second order.
- **FR-020**: The order number MUST be unique, `ORD-` followed by a number, increasing in the
  order numbers are assigned (gaps allowed), safe under concurrent checkouts, and never collide
  with the seeded ORD-0001 to ORD-0008.
- **FR-021**: Successful checkout MUST return 201 with the full order (as FR-024).
- **FR-022**: The domain MUST enforce: a variant's stock never goes below 0; a new order always
  starts as Pending; the order owns its items and its status transitions.

**Customer orders**

- **FR-023**: `GET /api/orders` MUST return the customer's orders, newest first, paged (page
  default 1, page size default 10, maximum 50): id, order number, status, item count, total,
  created date.
- **FR-024**: `GET /api/orders/{id}` MUST return: order number, status, created and updated dates,
  shipping details, notes, items (snapshot fields, quantity, line total), subtotal, shipping fee,
  total, and `canCancel` (true only when Pending).
- **FR-025**: `POST /api/orders/{id}/cancel` MUST be allowed only while the order is Pending
  (customer rule); otherwise 409 `orderCannotBeCancelled`. Cancelling sets the status to Cancelled,
  updates the order's updated date and returns every item's quantity to its variant's stock, in one
  transaction, with the same concurrency token and retry as checkout; if all 3 attempts conflict,
  409 `orderConflict`. Returns the updated order.
- **FR-026**: The domain `Order` MUST allow `Cancel` from Pending or Confirmed only (domain rule)
  and reject any other transition; the Pending-only customer rule is enforced by the application.

**Regression**

- **FR-027**: All 001 and 002 tests MUST still pass.

### Key Entities

- **Cart item** (from 001): one line in a customer's cart: customer, variant, quantity (1–5),
  added date. Unique per customer and variant.
- **Order** (from 001): number, customer, status, shipping details, notes, subtotal, shipping fee,
  total, created/updated dates; owns its items and status transitions; has a concurrency token
  protecting status changes.
- **Order item** (from 001): snapshot of product name, color, size, unit price and quantity, plus a
  link to the variant.
- **Product variant** (from 001): stock per color and size, with a concurrency token; stock changes
  only at checkout and cancel.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All 13 acceptance criteria from the description pass as automated tests against the
  001 seed data.
- **SC-002**: In every concurrent checkout test, stock never goes below 0 and the number of orders
  equals the number of units available (0 oversold units).
- **SC-003**: A failed checkout leaves no trace: 0 orders, 0 stock changes, and the cart unchanged.
- **SC-004**: A customer can go from an empty cart to a placed order in 3 calls (add, read, check
  out), each feeling instant on the local machine (under 1 second).
- **SC-005**: No response reveals another customer's cart or orders, or whether another customer's
  order id exists.
- **SC-006**: Merging the same guest cart twice gives the same cart as merging it once (100%).

## Assumptions

- The cart item id in `PUT`/`DELETE` is the cart line id; ids from another customer's cart behave
  like unknown ids (404 `cartItemNotFound`, an error code added by this feature).
- `maxQuantityExceeded` is a 400 validation-style error; `insufficientStock` and `cartFull` are 409.
- For an empty cart (or a cart with no `available` items), the subtotal is 0, the shipping fee is 0
  and the total is 0; the shipping fee applies only when at least one item is `available`.
- Merge only touches the variants in the guest list; existing cart lines not in the list are left
  as they are. An existing line is never reduced by merge below its current quantity unless stock
  forces it (then it is reported `quantityReduced`).
- The image URL in the cart is the product's main image for the variant's color (lowest display
  order, then id), or null if none exists.
- Checkout validates shipping details before looking at the cart (an invalid body is a 400 even
  when the cart is empty).
- Order list totals and line totals come from the stored order values, never recalculated from
  current prices.
- Cancelling ORD-0008 in tests uses the demo customer; other tests that change stock or orders use
  their own database or freshly registered customers.

## Dependencies

- 001-database: cart, order, order item and variant tables, rowversion on variants, seed data.
- 002-backend-core: authentication and roles, error shape, validation helper, catalog rules
  (`maxQuantity`, main image), `TimeProvider`, API test fixtures.
- Constitution v1.7.3: Inventory & Concurrency (IV), Data Integrity (V), Business Rules (max 5,
  shipping 50, customer cancels Pending only, cancel returns stock).

## Out of Scope

- Admin order management and status changes after Pending (004-backend-admin).
- Online payment, editing an order, saved addresses, stock reservation while items sit in the cart.
- Frontend.
