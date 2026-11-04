# Address book

- **Status:** Draft (2026-10-25)
- **Jira:** SCRUM-61 (FR-40) under epic SCRUM-9; SCRUM-125 (SEC-05)
- **BRD sections:** §7.5, §6.4 (TC-ACC-10), §12.3.1
- **Requirement IDs:** FR-40, SEC-05, NFR-26, NFR-19

## Problem & intent
A returning customer should not retype a delivery address. They keep several addresses (home, grandparents, office), mark one as the default, and later pick one at checkout (S12). This sprint builds the book and its account screen; checkout does not exist yet.

## Requirements (quoted from the BRD)
> **FR-40:** Registered users shall maintain multiple saved addresses selectable at checkout.

> **SEC-05:** Every API endpoint that accepts a resource ID (Child Profile, order, address, wishlist item) shall verify server-side that the resource belongs to the authenticated caller, independent of any UI-level restriction — closing the IDOR gap identified for Child Profile access.

BRD data model (§12.3.1, "Addresses"): `PK Id; FK UserId; CityId FK; ProvinceId FK; Line1/2; Phone; IsDefault` — "Registered users' saved address book (FR-40)."

## User flow
1. Account → Addresses lists the panels, the default marked "Default" (a word, not colour alone).
2. **Add / Edit:** recipient name `(new)`, phone, province, city, address lines, optional landmark/postcode `(new, see Q-ADD-2)`.
3. **Make default**, **Remove** (with a confirmation). The first address becomes the default automatically `(new)`; removing the default promotes the most recent remaining one `(new)`.
4. Checkout (S12) reads the list through a customer contract; this feature exposes the query, not the checkout UI.

## Acceptance criteria

| ID | Given / When / Then | Source |
|---|---|---|
| ADD-1 | Given a signed-in customer, when they add, edit or remove an address, then the change persists and is the list shown next time. | TC-ACC-10 |
| ADD-2 | Given several addresses, then exactly one is the default at any time (none only when the book is empty); marking another default clears the previous one in the same transaction. | FR-40 (new) |
| ADD-3 | Given the customer's addresses, when another customer's address id is sent to read, edit, remove or make default, then the response is 404 and nothing changes; no endpoint takes a customer id. | SEC-05 |
| ADD-4 | Given province and city, then they are chosen from lookup tables (city belongs to its province) and a mismatch is rejected. | data model, naming rules |
| ADD-5 | Given a phone number, then it is validated to the format decided in Q-ACT-7 and stored in one normalised form. | FR-24 (new) |
| ADD-6 | Given a book at the limit (default 10 addresses, configurable), then adding one more is refused with a clear message. | (new) |
| ADD-7 | Given the list endpoint, then it is paged and loads cities and provinces without per-row queries. | CONVENTIONS |
| ADD-8 | Given the address screen, then every field has a label, errors say what to do, the remove dialog traps focus and returns it, and the page is `noindex`. | NFR-19, SEO-05 |

## Non-functional constraints
Addresses are personal data (NFR-26): never logged in full. List p95 under 200 ms.

## Out of scope
Selecting an address at checkout and guest addresses (S12); address validation against a courier's coverage (S15); map pins.

## Open questions
- [ ] **Q-ADD-1 Provinces and cities.** The BRD calls for lookups but gives no list. Default: Pakistan's four provinces, ICT and the main cities as seed data, extendable by migration. Does the business supply the list (couriers will constrain it in S15)?
- [ ] **Q-ADD-2 Fields.** Default: recipient name, phone, province, city, line 1, line 2 (optional), postcode (optional). Anything else the couriers will need?
- [ ] **Q-ADD-3 Limit.** Is 10 addresses per account acceptable?
