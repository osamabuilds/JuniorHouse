# Child Profiles (basic)

- **Status:** Parked (owner, 2026-10-25): the spec and plan are kept, but nothing is built until the owner says
- **Jira:** SCRUM-38 (FR-18), SCRUM-41 (FR-21) under epic SCRUM-7; SCRUM-125 (SEC-05)
- **BRD sections:** §7.3, §6.4 (TC-ACC-11, 12, 14), §9, §12.3.5, §12.9
- **Requirement IDs:** FR-18, FR-21, NFR-27, NFR-29, SEC-05, SEC-25

## Problem & intent
A parent records the children they shop for, so later features can suggest sizes and ages. This data is about minors, so it is visible to the owning parent only, never used for marketing, and can be exported or erased on request. The Fit Finder is descoped; this is the profile record itself.

## Requirements (quoted from the BRD)
> **FR-18:** Registered users shall be able to create, edit, and delete one or more Child Profiles, each with a name/nickname, birth month-year, height, and weight.

> **FR-21:** Deleting a Child Profile shall remove the profile and its size-preference history but shall not alter or delete past orders.

> **NFR-27:** Child Profile data (name/nickname, birth month-year, height, weight) is personal data about a minor and shall be collected only with the parent/guardian account holder's action, stored with access restricted to that parent's account, and never used for external marketing/ad-targeting purposes without explicit separate consent.

> **NFR-29:** The platform shall provide a mechanism for a parent to export or delete all data associated with a Child Profile (data portability/right-to-erasure equivalent), even before this is legally mandated locally, as a trust-building baseline for a kids' brand.

> **SEC-05:** Every API endpoint that accepts a resource ID (Child Profile, order, address, wishlist item) shall verify server-side that the resource belongs to the authenticated caller, independent of any UI-level restriction — closing the IDOR gap identified for Child Profile access.

## User flow
1. Account → Child profiles lists a panel per child (initial avatar, nickname, age from birth month-year).
2. **Add a child** dialog: nickname, birth month and year, optional usual size `(Q-CHD-2)`. (Height and weight dropped, Q-CHD-1.)
3. **Edit** and **Delete** (confirmation says past orders are kept). **Download my child's data** (export) `(see NFR-29, Q-CHD-3)`.
4. The page states that only the account holder can see these profiles.

## Acceptance criteria

| ID | Given / When / Then | Source |
|---|---|---|
| CHD-1 | Given a signed-in customer, when they enter nickname, birth month-year, height and weight, then a profile is saved and appears in their list. | TC-ACC-11, FR-18 |
| CHD-2 | Given a second child, then both profiles exist independently and editing one never changes the other. | TC-ACC-12 |
| CHD-3 | Given a profile, when edited, then the new values are shown immediately. | FR-18 |
| CHD-4 | Given inputs, then birth month-year is not in the future and not older than a sensible child age limit (default 16 years), and height and weight are within plausible ranges; otherwise guidance is shown. | FR-18 (new, plausibility ranges in Q-CHD-1) |
| CHD-5 | Given a profile id belonging to another customer, when read, edited, deleted or exported, then the response is 404 and nothing changes; no endpoint takes a customer id. | SEC-05, NFR-27 |
| CHD-6 | Given a signed-out visitor, then every Child Profile endpoint returns 401 and no profile content is in any server-rendered HTML. | NFR-27 |
| CHD-7 | Given a delete, then the profile and any history linked to it are permanently erased (not merely hidden), and past orders are untouched. | TC-ACC-14, FR-21, NFR-29 |
| CHD-8 | Given an export request, then the parent receives all stored data for that profile in a machine-readable file (JSON) and a readable view. | NFR-29 |
| CHD-9 | Given logs, traces and error bodies, then they never contain a full profile record, the nickname or the measurements; only the profile id. | SEC-25 |
| CHD-10 | Given the profile data, then no other module, report, export or marketing list can read it except through this module's owner-scoped contract (none is offered this sprint). | NFR-27 |
| CHD-11 | Given a limit (default 8 profiles per account, configurable), then adding more is refused clearly. | (new) |
| CHD-12 | Given the dialogs, then focus is trapped and returned, Esc closes, fields are labelled, and the page is `noindex`. | NFR-19, SEO-05 |

## Non-functional constraints
Encryption at rest follows the platform decision (disk-level at minimum; column-level for nickname and birth month-year evaluated, BRD §12.9). The plan states which. Rows live in the `CUST` schema; deleting an account's children does not touch other modules.

## Out of scope
Fit Finder, size recommendations, growth tracker/prediction, measurement history, personalised homepage (descoped or P2); photos (Virtual Try-On descoped); sharing a profile with another adult.

## Open questions
- [x] **Q-CHD-1 Height and weight (owner, 2026-10-25): dropped.** The Fit Finder is descoped, so nothing uses them, and collecting minors' measurements without a use goes against data minimisation. A profile holds nickname, birth month-year and an optional usual size. **This deviates from FR-18's wording**; it is logged on the BRD errata (SCRUM-168) and the columns are added later by a migration if a feature needs them. CHD-1 and CHD-4 are read without height and weight.
- [ ] **Q-CHD-2 Usual size.** The design offers an optional "usual size". The BRD does not. Add it (a lookup of size labels) or leave it out?
- [ ] **Q-CHD-3 Export form.** Default: a JSON download plus an on-screen view, available per child. Also an export of all children at once?
- [ ] **Q-CHD-4 Delete vs soft delete.** The BRD data model keeps an `IsDeleted` flag and a separate hard-erasure job. FR-21 and NFR-29 are met more simply by deleting the row for good, because no order refers to a child. Default: permanent delete, no soft flag. If orders later reference a profile (e.g. size chosen for a child), that reference is set to null.
- [ ] **Q-CHD-5 Size-preference history.** FR-21 mentions it, but no feature records one. Default: nothing to delete this sprint; the deletion path is written so a future history table is included.
