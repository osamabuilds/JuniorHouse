# Authorization matrix: how it was made and reviewed

`authorization-matrix.csv` lists every route of the host (method, route pattern, the permission it needs). It is the single list the tests enforce: `AuthorizationMatrixTests` fails when a live route is missing from it, differs from it, or is open without being on the anonymous list (roles AC-9, AC-10).

Values in the `permission` column:

- a permission code (`catalog.write`, ...): the route needs a signed-in caller of a full session who holds it;
- `session`: any signed-in caller, whatever the session scope (sign-out, the caller's own profile, own password, MFA enrolment);
- `anonymous`: open to everyone. Only the routes in the test's explicit list may be open: health probes, sign-in step 1 and 2, refresh, sign-out, and the public storefront (`/api/storefront/*`, `/media/{name}`).

## How the first version was made (R4)

`AuthorizationMatrixCapture` (an opt-in tool in the integration tests, run with `ROMP_MATRIX_OUT=<file>`) listed every route with the policy it carried before Sprint 8 and proposed a permission mechanically: `Staff` plus GET became `<area>.read`, `Staff` plus a mutating method `<area>.write`, `Admin` became `ops.admin`, `Inventory` became `inventory.write`; vendors and purchase orders map to `procurement`, quality routes to `qc`; stock, adjustment, cycle-count and alert reads map to `inventory.read`; the storefront admin screens (settings, search) map to `catalog`.

## Review by hand (R5), 2026-10-26

Every row was read against the roles spec. Changes and decisions:

- `POST /api/catalog/slugs/suggest` is a query (it only proposes a slug), so it needs `catalog.read`, not `catalog.write`.
- `POST /api/labelling/work-items/{id}/resolve` looks like a query but it mutates (it resolves the work item), so it stays `labelling.write`.
- `GET /api/warehouse/cycle-counts/{id}/review` (the approval screen with the variances) keeps `inventory.write`: a GET that needs a write permission is stricter, not looser, and the approval is an Owner-only action (roles spec AC-15).
- `PUT /api/warehouse/cycle-counts/{id}/lines` (entering counts) is `warehouse.write`; starting, cancelling and confirming a count are `inventory.write` (they were `Inventory` routes).
- `POST /api/labelling/print-jobs/{id}/cancel` and `POST /api/labelling/variants/{id}/void` stay `ops.admin` (voids, spec table).
- `GET /api/warehouse/stock/consistency` is `ops.admin` (it was an `Admin` route).
- The storefront settings and search administration (`/api/admin/storefront-settings/*`, `/api/admin/storefront/search/*`) were guarded by two storefront-only policies; they become `catalog.read` and `catalog.write` because the storefront is the presentation of the catalogue. Only the Owner and the Catalogue role can change them. If the owner wants a separate storefront permission, it is a lookup row and a one-word change in the endpoint declarations.
- `POST /api/admin/storefront/rebuild` and its status are `ops.admin`.
- No mutating GET and no read-only POST was found besides the slug suggestion.
