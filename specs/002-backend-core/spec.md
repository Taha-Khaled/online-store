# Feature Specification: Backend Core — API Foundation, Authentication & Public Catalog

**Feature Branch**: `feat/002-backend-core`

**Created**: 2026-11-25

**Status**: Draft

**Input**: User description: "Backend core for the online clothing & shoes store: API foundation,
authentication, and the public catalog. Backend only; no frontend work. Builds on feature
001-database (schema, seed data, stored procedure already exist)." (The full description, the 20
acceptance criteria and the out-of-scope list supplied by the developer are reproduced in the
requirements below.)

**Tracking**: GitHub issue #5 (whole feature). Commit work items use `#002-T0xx`.

## Clarifications

### Session 2026-11-25

- Q: Does reusing a refresh token revoked by logout trigger reuse detection? → A: No. Only a
  reused **rotated** token revokes all of the user's tokens; a logged-out token just returns 401
  (FR-023, FR-028).
- Q: What does logout do with a token that is not active? → A: Always 204; it revokes the token
  only if active, and a rotated token at logout does not trigger reuse detection (FR-029).
- Q: Are size names case-insensitive like colors? → A: Yes (`size=xl` matches `XL`) (FR-033).
- Q: With both color and size filters, must one variant have both? → A: Yes, one in-stock variant
  must have a requested color and a requested size (FR-035).
- Q: Does AC 6 `category=sneakers` mean the slug `shoes-sneakers`? → A: Yes; `sneakers` alone is
  an unknown slug (404).
- Q: A rotated token that has also expired: reuse detection or plain 401? → A: "Rotated" is
  checked before "expired", so it triggers reuse detection (FR-023).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Shopper browses the catalog (Priority: P1)

Anyone, logged in or not, sees the category tree, lists active products with search, filters,
sorting and pages, and sees each product's main image.

**Why this priority**: browsing is the store's core value and the first thing every visitor does;
it needs no account.

**Independent Test**: start the API on the 001 seed data and call the category and product list
endpoints with different search, filter, sort and page values; check counts and order.

**Acceptance Scenarios**:

1. **Given** the seeded store, **When** the category tree is requested, **Then** it returns 3
   parents (Men, Women, Shoes), each with its 3 children, using the 001 seed slugs.
2. **Given** the seeded store, **When** the product list is requested with defaults, **Then** the
   total is 18, page 1 has 12 products, page 2 has 6, ordered newest first.
3. **Given** the seeded store, **When** filtering by featured, **Then** exactly the 6 featured
   products return; by category `shoes`, the 6 shoe products; by `sneakers`
   (slug `shoes-sneakers`), 2.
4. **Given** the seeded store, **When** sorting by price low to high, **Then** Basic Ribbed Top
   (300) is first; high to low, Leather Derby Shoes (2200) is first.
5. **Given** the seeded store, **When** searching `shoes`, **Then** all 6 shoe products return,
   including those without "shoes" in their name (matched through their category).
6. **Given** the seeded store, **When** searching `shoes` with size `42`, **Then** 5 products
   return; Everyday White Sneakers is excluded because its size 42 is out of stock.
7. **Given** the seeded store, **When** searching `JEANS`, **Then** the result equals searching
   `jeans`.
8. **Given** page size 5, **When** paging through all products in every sort order, **Then** each
   of the 18 products appears exactly once.
9. **Given** the seeded store, **When** the image `/images/products/classic-cotton-t-shirt-black.jpg`
   is requested without a login, **Then** the image is returned.
10. **Given** a product deactivated directly in the database, **When** the list is requested,
    **Then** the product is not in it.

---

### User Story 2 - Shopper views a product's details (Priority: P1)

A shopper opens a product by its slug and sees its description, category, colors with their
images, sizes, and which color/size combinations can be bought and how many.

**Why this priority**: a shopper cannot choose a variant (and later add it to the cart) without
this page.

**Independent Test**: request the details of seeded products and of unknown or inactive slugs.

**Acceptance Scenarios**:

1. **Given** the seeded store, **When** `classic-cotton-t-shirt` is requested, **Then** it shows
   3 colors, sizes S–XL in display order, and 12 variants; White/L has `inStock` false; Black/M
   has `maxQuantity` 1; every variant with stock 8–20 has `maxQuantity` 5.
2. **Given** an unknown slug or a deactivated product, **When** its details are requested,
   **Then** the response is 404 `productNotFound`.
3. **Given** any product, **When** its details are returned, **Then** no exact stock number
   appears anywhere in the response.

---

### User Story 3 - Customer registers and logs in (Priority: P2)

A visitor creates a customer account and is logged in immediately; an existing user logs in with
email and password, sees their own info, and stays logged in on several devices.

**Why this priority**: needed before any customer-only feature (cart sync, checkout, orders in
003), but browsing works without it.

**Independent Test**: register, log in with the seeded accounts, and call the current-user
endpoint with and without a token.

**Acceptance Scenarios**:

1. **Given** valid registration data, **When** registering, **Then** a Customer is created and
   the response is 201 with an access token and a refresh token that work immediately.
2. **Given** an existing email, **When** registering it again in any letter case, **Then** the
   response is 409 `emailAlreadyExists`.
3. **Given** the seeded demo customer, **When** logging in as `customer@store.local`, **Then** an
   access token and a refresh token are returned, and the current-user endpoint with that access
   token returns Demo Customer with role Customer.
4. **Given** no token, or an access token that is 15 minutes old or older, **When** calling the
   current-user endpoint, **Then** the response is 401.
5. **Given** a wrong password, or an email that does not exist, **When** logging in, **Then**
   both return the same 401 `invalidCredentials`.
6. **Given** the 001 seeded accounts, **When** they log in with their seeded passwords after the
   seeder change, **Then** login succeeds for both.
7. **Given** an empty email on registration, **When** registering, **Then** the response is 400
   with the error shape and a field error for email.

---

### User Story 4 - Customer stays logged in safely (Priority: P2)

A logged-in customer's app renews the session with a refresh token without asking for the
password, and logs out of one device without affecting others. A stolen refresh token stops
working once the real user has refreshed.

**Why this priority**: short-lived access tokens are only usable with renewal; reuse detection
limits the damage of a leaked token.

**Independent Test**: log in, refresh, reuse old tokens, log out, and check which tokens still
work; inspect the stored token values in the database.

**Acceptance Scenarios**:

1. **Given** a valid refresh token, **When** refreshing, **Then** a new access token and a new
   refresh token are returned, and the old refresh token then returns 401.
2. **Given** a refresh token that was already rotated, **When** it is used again, **Then** the
   response is 401 and all of that user's refresh tokens are revoked, so the newest one also
   stops working.
3. **Given** a user logged in on two devices, **When** logging out on the first, **Then** the
   first refresh token returns 401 and the second still works, in whichever order they are
   tried.
4. **Given** a refresh token older than 7 days, **When** refreshing, **Then** the response is 401.
5. **Given** any number of logins and refreshes, **When** the stored refresh tokens are inspected,
   **Then** no token appears in plain text.

---

### User Story 5 - Developer runs and explores the API (Priority: P3)

The developer starts the API with its required settings and exercises every endpoint from the
interactive documentation, including endpoints that need a token. Every error has one shape.

**Why this priority**: it makes the feature verifiable by hand and is the contract the frontend
(a later feature) builds against.

**Independent Test**: start the API with and without the required settings; send invalid
requests and check the error shape and status codes.

**Acceptance Scenarios**:

1. **Given** a required authentication setting is missing or the signing key is too short,
   **When** the API starts, **Then** it refuses to start with a message naming the setting.
2. **Given** Development, **When** the API runs, **Then** the interactive documentation is
   available and accepts a Bearer token; outside Development it is not available.
3. **Given** an invalid sort value or a page size of 49, **When** listing products, **Then** the
   response is 400 with the error shape and field errors.
4. **Given** an unexpected failure, **When** a request fails, **Then** the response is 500 with a
   generic message, and the details are only in the logs.

---

### Acceptance criteria (developer numbering)

The developer's 20 acceptance criteria, as cited by plan.md and tasks.md ("AC n"), mapped to the
scenarios above:

| AC | Scenarios / requirement | AC | Scenarios / requirement |
|---|---|---|---|
| 1 | US3-1, US3-2 | 11 | US1-9 |
| 2 | US3-3, US3-4 | 12 | US3-6 |
| 3 | US3-5 | 13 | US4-1, US4-2 |
| 4 | US1-1 | 14 | US4-3 |
| 5 | US1-2 | 15 | US4-4, US4-5 |
| 6 | US1-3 (`sneakers` = slug `shoes-sneakers`, confirmed 2026-11-25) | 16 | US1-5, US1-6 |
| 7 | US1-4 | 17 | US1-7 |
| 8 | US2-1 | 18 | US1-8 |
| 9 | US1-10, US2-2 | 19 | US3-4 |
| 10 | US3-7, US5-3 | 20 | FR-042 |

### Edge Cases

- Page beyond the last page: empty list with the correct total count and total pages.
- Unknown category slug in the product list or the filter panel: 404 `categoryNotFound`.
- Unknown color or size names: they match nothing (empty result for that filter), not an error.
- Min price greater than max price, page below 1, page size 0 or above 48, search longer than 100
  characters, unknown sort value: 400 validation error.
- Search containing `%`, `_` or `[`: those characters are matched literally.
- Empty or whitespace-only search: ignored.
- A product with no in-stock variant: still listed (with "in stock" false) unless a color or size
  filter is given; it never matches a color or size filter.
- Two registrations with the same email at the same moment: one succeeds, the other gets 409
  `emailAlreadyExists` (never a 500).
- A refresh token that is malformed, unknown, expired or revoked: 401, without revealing which.
- A valid access token whose user no longer exists: 401 on the current-user endpoint.
- Access token signed with another key, wrong issuer or wrong audience: 401.
- Image file that does not exist: 404; paths outside the images folder are never served.

## Requirements *(mandatory)*

### Functional Requirements

**API foundation**

- **FR-001**: Every error response MUST use one shape: `code` and `message`; validation failures
  MUST also include `errors`, mapping each field to its messages. Error codes MUST be defined
  once, in one place.
- **FR-002**: Status codes MUST be: 400 validation, 401 not authenticated, 403 not allowed,
  404 not found, 409 conflict, 500 unexpected. A 500 MUST carry a generic message; details go
  only to the logs. Framework errors (e.g. 405 wrong method, 415 unsupported media type, any
  other error status without a body) MUST use the same shape.
- **FR-003**: One global error handler MUST convert failures to this shape.
- **FR-004**: Logs MUST carry metadata only (ids, codes, counts, durations). They MUST NEVER
  contain request bodies, passwords, tokens or personal data (names, emails, phones, addresses).
- **FR-005**: Input MUST be validated by the application services before any work is done, using
  the project's validation library, called explicitly by each service.
- **FR-006**: Interactive API documentation MUST be available in Development only and MUST allow
  sending a Bearer token.
- **FR-007**: Product images MUST be served publicly at `/images/products/<file>` from the seed
  images folder. This is the only part of the API the browser calls directly.
- **FR-008**: All times MUST come from the injectable time provider in UTC; no code reads the
  local clock.
- **FR-009**: The API MUST refuse to start, with a clear message naming each missing or invalid
  key, when `Jwt:Key` (at least 32 bytes), `Jwt:Issuer` or `Jwt:Audience` is missing.
  These come from User Secrets locally and environment variables in production; never from
  committed files.

**Authentication**

- **FR-010**: `POST /api/auth/register` MUST accept full name, email, phone and password, and MUST
  always create a Customer. Admins are never created through the API.
- **FR-011**: Email MUST be unique regardless of letter case and stored lowercase. A duplicate
  returns 409 `emailAlreadyExists`.
- **FR-012**: Successful registration MUST log the new customer in: status 201 with the same body
  as login.
- **FR-013**: Registration validation: full name 2–100 characters; valid email up to 256
  characters; phone up to 20 characters, digits with an optional leading `+`; password at least
  8 characters with at least one letter and one digit.
- **FR-014**: `POST /api/auth/login` MUST accept email and password and return an access token, a
  refresh token, and the user's id, full name, email and role. Login validation only checks that
  both are present (the registration password rules do not apply).
- **FR-015**: Wrong email and wrong password MUST return the same 401 `invalidCredentials`.
- **FR-016**: `GET /api/auth/me` MUST require a valid access token and return the current user's
  id, full name, email and role.
- **FR-017**: The access token MUST carry user id, email and role, be signed with the configured
  key, and expire exactly 15 minutes after issue, with no clock-skew allowance.
- **FR-018**: The API MUST read the token only from the `Authorization: Bearer` header and MUST
  NEVER set cookies.
- **FR-019**: The refresh token MUST be a long random value that expires 7 days after issue.
- **FR-020**: Only a hash of each refresh token MUST be stored, in a new `RefreshTokens` table
  (user, token hash, created, expires, revoked date, replacing token), added through one new
  named migration.
- **FR-021**: Each login and registration MUST create a separate refresh token, so a user can be
  logged in on several devices.
- **FR-022**: `POST /api/auth/refresh` MUST return a new access token and a new refresh token and
  revoke the presented token immediately, recording which token replaced it (rotation). There is
  no grace period.
- **FR-023**: Using a refresh token that was already rotated MUST revoke all of that user's
  refresh tokens and return 401, even if the rotated token has also expired.
- **FR-024**: `POST /api/auth/logout` MUST revoke the presented refresh token only. Other devices'
  tokens are unaffected.
- **FR-025**: Logout MUST NOT invalidate access tokens already issued; they stay valid until they
  expire (at most 15 minutes).
- **FR-026**: Password hashing MUST go through an application-level password hasher that hashes
  and verifies from the password alone, using the ASP.NET Core password hashing algorithm required
  by the constitution, so hashes created by the 001 seeder stay valid.
- **FR-027**: The user entity MUST receive and validate a real, non-empty hash when it is created.
  The 001 account seeder MUST use the new hasher, and its temporary empty-hash step MUST be
  removed.
- **FR-028**: Using a refresh token that was revoked by logout MUST return 401 and MUST NOT revoke
  any other token. Reuse detection (FR-023) applies only to rotated tokens.
- **FR-029**: Logout MUST always return 204. It revokes the presented token only if that token is
  active; an unknown, expired, already revoked or rotated token has no effect (in particular, a
  rotated token at logout does not trigger reuse detection), and the response never reveals
  which case applied.

**Public catalog**

- **FR-030**: Only active products MUST ever be returned by public endpoints (list, details,
  filter panel).
- **FR-031**: `GET /api/categories` MUST return all categories as a tree (parents with their
  children), each with name and slug.
- **FR-032**: `GET /api/products` text search MUST match part of the product name, the
  description, the product's category name or its parent category name; case-insensitive
  regardless of database collation; `%`, `_` and `[` matched literally; empty search ignored;
  over 100 characters is a validation error.
- **FR-033**: Filters: category slug (a parent includes its children's products; unknown slug →
  404 `categoryNotFound`), one or more color names, one or more size names (both
  case-insensitive, e.g. `size=xl` matches `XL`), min price, max price (min > max is a validation
  error), featured only. Unknown color or size names match nothing.
- **FR-034**: Search and all given filters MUST combine with AND; several values of one filter
  combine with OR.
- **FR-035**: A product MUST match a color or size filter only if it has an in-stock variant with
  that color or size. When both are given, one in-stock variant MUST have a requested color AND a
  requested size (the shopper can actually buy that combination).
- **FR-036**: Sorting MUST be one of a fixed set: newest (default), price low to high, price high
  to low, name A–Z. Any other value is a validation error. Every order MUST end with a fixed
  tiebreaker: name then id; newest is creation date descending then id descending.
- **FR-037**: Pagination: page (default 1, minimum 1) and page size (default 12, 1–48). The
  response MUST include total count and total pages. A page beyond the last returns an empty list
  with correct totals.
- **FR-038**: Each list item MUST contain id, name, slug, price, category name, featured flag,
  main image URL, the product's colors (name + hex), and whether any variant is in stock.
- **FR-039**: The main image MUST be the product's main image with the lowest display order, then
  the lowest id. Colors MUST be listed in the order of their images.
- **FR-040**: `GET /api/products/{slug}` MUST return id, name, slug, description, price, category
  (name, slug, parent name), colors (each with its images in display order), sizes in display
  order, and every variant with color, size, SKU, `inStock` and `maxQuantity` = min(stock, 5).
  Exact stock MUST NOT be exposed. Unknown or inactive slug → 404 `productNotFound`.
- **FR-041**: `GET /api/products/filters` MUST return the colors and sizes that have at least one
  in-stock variant on an active product, optionally limited to a category slug (with its
  children), plus the minimum and maximum product price.

**Regression**

- **FR-042**: The 001 schema tests MUST be updated for the `RefreshTokens` table, and all 001
  tests MUST still pass.

### Key Entities

- **User** (from 001): now created only with a real password hash. Owns many refresh tokens.
- **Refresh token** (new): one login session on one device. Belongs to a user; holds the token's
  hash (never the token), creation and expiry times, the revocation time if revoked, and the
  token that replaced it if rotated. Active = not revoked and not expired.
- **Product, Category, Color, Size, Product variant, Product image** (from 001): read-only in this
  feature.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All 20 acceptance criteria from the description pass as automated tests against the
  001 seed data.
- **SC-002**: A developer can start the API and complete register → login → current user →
  refresh → logout from the interactive documentation in under 5 minutes.
- **SC-003**: Paging through the whole catalog in any sort order and any page size from 1 to 48
  shows every product exactly once.
- **SC-004**: 100% of error responses (validation, auth, not found, conflict, unexpected) use the
  single error shape.
- **SC-005**: A copy of the database alone never yields a usable refresh token: 0 tokens stored in
  plain text.
- **SC-006**: A refresh token stolen and used after the real user refreshed is rejected 100% of
  the time, and its use ends all of that user's sessions.
- **SC-007**: Product list and details responses on the seeded catalog feel instant to a shopper
  (under 1 second on the local machine).
- **SC-008**: No response ever reveals whether an email is registered (except registration's own
  409) or a variant's exact stock.

## Assumptions

- Login matches the email case-insensitively (emails are stored lowercase), and leading/trailing
  spaces in email and full name are trimmed before validation and storage.
- Error code names other than those named in the description (for example for validation,
  not-authenticated, not-allowed and unexpected errors, and an invalid refresh token) are chosen
  in the plan and defined once with the others.
- Color and size filter values are user-supplied names compared as query parameters against the
  stored names; the set of sort and filter options is predefined, per the constitution's Security
  principle (clarified in v1.7.2).
- A product's colors are the colors of its variants (in stock or not), ordered by their first
  image's display order; a color without an image comes last, by name. A product with no main
  image has no main image URL (null).
- Product details list every variant, including out-of-stock ones.
- The category tree lists every category, even ones with no active products.
- The filter panel's price range uses the active products in the same scope (category if given),
  regardless of stock; an unknown category slug returns 404 `categoryNotFound`.
- The refresh endpoint, logout and register need no access token; only the current-user endpoint
  requires one. No endpoint in this feature is admin-only.
- 403 is part of the shared error contract but no endpoint in this feature produces it yet.
- Images are served from the same seed images folder the 001 seeder checks; a missing file is a
  404 with the error shape, like any other unknown path.
- The existing local database keeps its seeded data; the seeder change affects only how new
  accounts are hashed.

## Dependencies

- Feature 001-database: schema, seed data (accounts, 18 products, categories, images) and the
  seeding pipeline.
- Constitution v1.7.2: Clean Architecture, Data Access, Security (PasswordHasher, Bearer-only
  JWT, enum-based sorting), API Conventions, approved dependencies (FluentValidation, JwtBearer,
  Swashbuckle, Mvc.Testing).

## Out of Scope

- Cart, checkout, customer orders (003-backend-shopping).
- All admin endpoints, image uploads and the dashboard (004-backend-admin).
- Frontend.
- Password reset, email verification, rate limiting.
