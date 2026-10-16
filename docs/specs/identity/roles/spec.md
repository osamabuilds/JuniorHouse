# SCRUM-202: Roles and permissions

- **Status:** Approved (owner, 2026-10-16)
- **Jira:** SCRUM-202; sources SCRUM-85, SCRUM-128
- **BRD sections:** §7.8, §8.4, §11.3
- **Requirement IDs:** FR-60, NFR-14, SEC-08

## Problem & intent
Sprints 1 to 7 declared who may call each endpoint (`Staff`, `Admin`, `Inventory`, `Catalogue`) but enforced nothing: a development handler lets everyone through. Once real sign-in exists, every endpoint must be enforced against the right permission, a person must hold only what their job needs, and a founder running everything alone must not be blocked by roles written for a staffed office.

## Requirements (quoted from the BRD)
> **FR-60:** Admin access shall be role-based (catalogue admin, order-ops, support, marketing) following least-privilege.

> **NFR-14:** Admin/back-office access shall be role-based, following least privilege, per FR-60.

> **SEC-08:** Admin API endpoints shall re-validate the caller's role on every request server-side; role-based UI hiding alone is not sufficient authorization.

## User flow
1. Roles and permissions are lookup data (D-4). A role is a named set of permissions; **Owner** holds all of them, including ones added later.
2. An administrator assigns one or more roles to a person. Effective permissions are the union.
3. Each endpoint declares the permission it needs. The server checks it on every request; the admin app hides what the person cannot use, as a convenience only.
4. Sprint 8 also verifies that all endpoints from Sprints 1 to 7 map to the intended permission (AC-9 to AC-12).

## Permission catalogue (proposed; names are lookup codes)

| Permission | Covers (existing endpoints) | Replaces |
|---|---|---|
| `ref.read`, `ref.write` | Reference lookups | `Staff` |
| `catalog.read`, `catalog.write` | Styles, products, media, size charts, composition, URLs, publish | `Catalogue` for writes, `Staff` for reads |
| `procurement.read`, `procurement.write` | Vendors, purchase orders, revisions, files (carry cost and advance terms) | `Staff` |
| `production.read`, `production.write` | Runs, milestones, samples, deliveries | `Staff` |
| `qc.read`, `qc.write` | Inbound deliveries, GRNs, batches, inspections | `Staff` |
| `labelling.read`, `labelling.write` | Work items, variants, print jobs | `Staff` |
| `warehouse.read`, `warehouse.write` | Bins, putaway | `Staff` |
| `inventory.read` | Stock levels, ledger, adjustment and count history, alerts (view) | `Staff` |
| `inventory.write` | Adjustments, cycle-count approval, alert thresholds | `Inventory` |
| `ops.admin` | Outbox and operational data, voids | `Admin` |
| `staff.manage` | Staff accounts, roles, MFA reset | new |
| `audit.read` | Audit trail | new |

| Role | Permissions |
|---|---|
| Owner | all (superset flag, not a copied list) |
| Catalogue | `ref.read`, `catalog.read`, `catalog.write`, `warehouse.read` |
| Order-ops | `ref.read`, `catalog.read`, `warehouse.read`, `inventory.read` (no write permission; see AC-14, AC-15) |
| Support | `ref.read`, `catalog.read` (order permissions arrive with S13 and S16) |
| Marketing | `ref.read`, `catalog.read` (promotion permissions arrive with S11) |

## Acceptance criteria

| ID | Given / When / Then | Source |
|---|---|---|
| AC-1 | Given a fresh database, then migrations seed the permission lookup, the five roles and the role-permission mapping; no code constant lists permissions beyond the codes used in endpoint declarations. | D-4 |
| AC-2 | Given a person with several roles, then their effective permissions are the union; given the Owner role, then every permission, including ones added by later migrations, is granted without a mapping row. | D-4 |
| AC-3 | Given a request, then the permission check runs on that request against the database (no permission claims in the cookie or token); a role change or deactivation takes effect on the person's next request. | SEC-08 |
| AC-4 | Given the code base, then no `RequireRole`, `IsInRole` or role-name string comparison exists outside the permission handler; an architecture test fails otherwise. | D-4 |
| AC-5 | Given a signed-in caller without the permission, then the response is 403 problem details that does not name the permission; given no valid session, 401. | SEC-08 |
| AC-6 | Given a caller with `staff.manage`, then they can list roles and permissions and assign or remove roles; assignments are audited; the last active Owner cannot lose the Owner role. | FR-60 |
| AC-7 | Given the admin app, then menu entries and buttons are hidden when the permission is missing, and the server check of AC-3 still applies to a direct call. | SEC-08 |
| AC-8 | Given Support or Marketing, then they hold no write permission on any Sprint 1 to 7 endpoint (least privilege); permissions are added by the sprint that introduces the feature. | FR-60 (new) |
| AC-9 | **Authorization matrix.** Given every route registered in the host, then a checked-in matrix file lists method, route pattern and required permission; a unit test enumerates the host's endpoints and fails if any endpoint is missing from the matrix, has a different permission, or is anonymous without being on the explicit anonymous list. | Verification pass |
| AC-10 | **Anonymous list.** Given the host, then only these routes allow anonymous calls: health probes, sign-in step 1, sign-in step 2, refresh, and the OpenAPI document in Development. | Verification pass |
| AC-11 | **Per-role behaviour.** Given real authentication enabled in an integration test, then for each role and each endpoint group of Sprints 1 to 7 the response is 2xx or the expected domain result when the matrix allows it and 403 when it does not, and 401 with no session. | Verification pass |
| AC-12 | **Nothing left declared-only.** Given the policies `Staff`, `Admin`, `Inventory` and `Catalogue`, then each existing use is migrated to a permission (reads and writes separated), the old names are removed, and an architecture test fails if a mutating endpoint (POST, PUT, PATCH, DELETE) requires only a read permission. | Verification pass |
| AC-13 | Given the Development and Testing environments, then the integration tests of earlier sprints keep running with a dev handler that signs in as an Owner; the real handler is also exercised in every sprint's integration run through AC-11. | (new) |
| AC-14 | Given the Owner is the only role holding `procurement.*`, `production.*`, `qc.*`, `labelling.*`, `warehouse.write` and `inventory.write`, then a user with every other role gets 403 on each of those endpoints, and the permissions can be given to a new role by inserting lookup rows only (test adds a role row in a migration-free fixture and the endpoint opens). | Owner decision 2026-10-16 |
| AC-15 | Given a user with only the Order-ops role, then stock and ledger views succeed, and stock adjustments, cycle-count approval and alert thresholds return 403. | Owner decision 2026-10-16 |

## Non-functional constraints
- The permission lookup per request is one indexed query (user to roles to permissions) and adds under 5 ms p95; result is held for the duration of the request only.
- The role-permission mapping is data: adding a role or moving a permission is a migration of lookup rows, with no code change.

## Out of scope
- Per-record ownership rules (SEC-05) for customers: S10.
- Approval workflows (four-eyes).
- Named roles beyond the five (a Warehouse role is a later lookup row).

## Open questions
- [x] ~~Supply-chain access~~ Decided by the owner (2026-10-16): vendors, purchase orders, production, QC, labelling and stock writes stay **Owner-only** as separate granular permissions (`procurement.*`, `production.*`, `qc.*`, `labelling.*`, `inventory.write`), so a future Warehouse role is one lookup row, not code. No new named roles in Sprint 8. Reason: PO and vendor data carries cost and advance terms Order-ops should not see.
- [x] ~~Order-ops and stock writes~~ Decided: Order-ops gets `inventory.read` only. Adjustments and cycle-count approval stay Owner-only, because the only controls on write-offs today are the audit trail and same-person reconfirm, so the role that dispatches orders must not also write stock off.
- [ ] `warehouse.write` (bins, putaway) was not named in the owner's list. It is given to the Owner only, like the other supply-chain writes, and Order-ops gets `warehouse.read`. Owner confirms or says which role should receive putaway.
