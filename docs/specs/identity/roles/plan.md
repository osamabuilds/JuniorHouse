# SCRUM-202: Roles and permissions, technical plan

- **Spec:** [spec.md](spec.md) · **Module plan:** [../plan.md](../plan.md)

## Domain model
- `Role` and `Permission` are lookups (rows, not enums). `Role.AllPermissions` marks Owner. `StaffUser` holds role ids.
- `EffectivePermissions` (query result): the set of permission codes for a user: all permissions when any role has `AllPermissions`, otherwise the union through `ROLE_PERM_MAP`, restricted to active rows.

## Application layer
| Command / Query | Input | Output | Covers |
|---|---|---|---|
| `GetEffectivePermissionsQuery` | user id | set of codes (one query) | AC-2, AC-3 |
| `AssignRolesCommand` | user id, role ids | none | AC-6 (last-Owner rule shared with staff-accounts) |
| `ListRolesQuery` | none | roles with permission codes (two queries, no N+1) | AC-6 |

## Cross-module surface (BuildingBlocks.Web)
- `Permissions` static class: one `PermissionCode` per permission code (`CatalogWrite = new("catalog.write")`, ...). **As built (G4):** a `PermissionCode` struct, not a `const string`, because `MapSecuredGroup(prefix, string policy)` already exists and two string overloads cannot coexist; the type also stops a policy name being passed where a permission is expected. `Permissions.All` lists them. A test proves the constants and the seeded `PERM_LKP` rows are the same set (AC-1).
- `RequirePermission(code)` endpoint extension adds an authorization requirement and `PermissionMetadata` (the matrix reads the last entry as the route's effective permission; a group's read and a route's write both apply, so a role holding a write code must also hold its read code, pinned by `PermissionCodesTests`); `MapSecuredGroup(prefix, permission)` replaces the policy argument. Reads are declared on the group; mutating routes add `.RequirePermission(<area>.write)` (the group default is the read permission).
- `IdentityPermissionHandler` (in `Identity.Infrastructure`) evaluates the requirement per request using the principal's user id and `GetEffectivePermissionsQuery`, and honours session scope.
- The four old policy names and `Policies.cs` are deleted once the migration below is done.

## API endpoints
`GET /api/identity/roles`, `PUT /api/identity/staff/{id}/roles` (`staff.manage`).

## Events
None.

## Data
`ROLE_LKP`, `PERM_LKP`, `ROLE_PERM_MAP` seeded by migration from the spec's tables. Later sprints add permissions and mapping rows by migration only.

## The Sprint 1-7 enforcement verification pass (AC-9 to AC-13)

1. **Capture before changing.** A test helper lists every endpoint in the host (`EndpointDataSource`: method, route pattern, current policy names) and writes `docs/specs/identity/roles/authorization-matrix.csv` with a proposed permission per row derived mechanically from the module, HTTP method and old policy (`Staff` + GET becomes `<area>.read` (areas: ref, catalog, procurement, production, qc, labelling, warehouse; stock, ledger, adjustment, count and alert reads become `inventory.read`), `Staff` + mutating becomes `<area>.write`, `Catalogue` becomes `catalog.write`, `Inventory` becomes `inventory.write`, `Admin` becomes `ops.admin`; Vendor routes map to `procurement`, Quality routes to `qc`).
2. **Review by hand.** Every row is checked against the spec's permission table and the roles; anything surprising (for example a GET that mutates, or a POST that is really a query, such as previews and resolves) is fixed in the matrix and in the endpoint, and listed in the PR.
3. **Migrate the endpoints** group by group (Reference, Catalog, Vendor, Production, Quality, Labelling, Warehouse, Admin), each followed by its module's tests.
4. **Enforce:** the matrix test (integration project, `WebApplicationFactory`) fails when any live endpoint is missing from the matrix, differs from it, or is anonymous without being listed; an architecture test fails when a POST/PUT/PATCH/DELETE route's required permission ends in `.read`, when `RequireRole`/`IsInRole`/role strings appear outside the handler, or when a policy name from the old set remains.
5. **Behaviour per role:** with real authentication enabled, seeded users for each role call one representative endpoint of every route group and the expected 2xx/403/401 is asserted from the matrix (AC-11). This is a data-driven test, so a new route group needs only a matrix row.
6. **Report honestly:** these integration tests run in the Linux container or CI, not on this machine; the verification report in the PR says which ran where.

## NFR / security design
- Handler loads permissions once per request and caches them in `HttpContext.Items`, never across requests (SEC-08). One indexed query.
- 403 body never names the permission; the security event log does (category `Romp.Security`).

## Frontend
`features/identity/roles/`: read-only role and permission matrix page for `staff.manage`; the staff screen's role picker. `core/auth` exposes `hasPermission()` and a route guard; menu entries and buttons use it for display only.

## Risks & alternatives considered
- **Mis-mapping a mutating GET or a read-only POST.** Mitigated by step 2 above and the architecture rule.
- **Support and Marketing hold almost nothing today.** Intentional (AC-8); their permissions arrive with the features they need.
- Open owner question on supply-chain roles ([spec](spec.md)) changes only seed rows, not code.

## Security & coupling review

| Area | How this feature meets it |
|---|---|
| Authorization | This feature is the mechanism: permission per route, checked per request on the server; deny by default (routes without a declaration fail the matrix test). |
| Input validation | Role ids validated against existing active rows; bodies under 1 KB. |
| Output encoding | Not applicable beyond JSON; no HTML or raw SQL. |
| Outbound calls | None. |
| Resource limits | Role assignment capped at 5 roles per user; list endpoints unpaged only because the role set is tiny and fixed. |
| Concurrency | `xmin` on `USR_MAIN`; last-Owner invariant via conditional update. |
| Idempotency | Assign is a replace-set operation: repeating it yields the same state. |
| Audit | Role changes audited; denied calls raise security events. |
| Data protection | Permission names are not personal data. |
| Secrets and dangerous config | Dev handler cannot be enabled outside Development/Testing (start-up check kept from ADR 0011). |
| Error responses | 401/403 problem details without detail about the missing permission. |
| New dependencies | None. |
| Coupling | Other modules depend only on `BuildingBlocks.Web` codes and extension; none references Identity. |
| Abuse cases | (1) A new endpoint added without a permission: matrix and `MapSecuredGroup` default-deny test fail the build. (2) A user keeps access after losing a role: permissions are not cached beyond the request. (3) Privilege escalation by assigning self Owner: needs `staff.manage`, audited, last-Owner rule. (4) UI-only hiding bypassed with a direct call: server check applies (AC-3, AC-7). |
