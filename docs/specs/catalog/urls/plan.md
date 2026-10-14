# SCRUM-201: URL convention and slugs, technical plan

- **Spec:** [spec.md](spec.md) · **Module design:** [../plan.md](../plan.md) · **Decision:** [ADR 0013](../../../adr/0013-product-url-convention.md)
- **Module(s):** Catalog (gender and category codes via the Reference contract)

## Domain model
- **`SlugPolicy`** (pure static rules): `Normalise(text)` → ASCII fold (strip diacritics, drop anything outside `a-z0-9`), lowercase, collapse separators to one hyphen, trim hyphens, cut at `MaxSlugLength` on a hyphen boundary; `Validate(slug)` → pattern `^[a-z0-9]+(-[a-z0-9]+)*$`, length 1..80, not reserved; `WithSuffix(slug, n)` → `slug-n` kept within the maximum.
- **`ProductPath`** (value object): (gender code, category code, slug) → `/{gender}/{category}/{slug}`; `Parse(path)` returns the parts or `Invalid`; `Canonicalise(path)` lowercases and strips the trailing slash (URL-5; the storefront uses it).
- **`PathHistory`**: the set of triples a product has had. `Product.ChangePath(...)` adds the old triple, keeps it forever, and refuses a triple that belongs to another product's history or current path (URL-11).
- **`PathResolution`**: `Current(productId)`, `Moved(newPath)`, `Gone`, `NotFound`.

## Application layer
| Command / Query | Input | Output | Covers |
|---|---|---|---|
| `SuggestSlug` | product name, gender, category | the first free slug (suffix chosen by probing existing slugs in one query) | URL-4, URL-6 |
| `SetProductPath` | product id, gender, category, slug, token | product with new path and history entry; `ProductChangedEvent` if published | URL-1..3, URL-7, URL-8, URL-11 |
| `ResolvePath` | path string | `PathResolution` | URL-2, URL-3, URL-9, URL-10 |
| `GetPathHistory` | product id | triples and when they were retired | `(new)` |

## API endpoints
| Method | Route | Auth / role | Ownership check |
|---|---|---|---|
| POST | `/api/catalog/slugs/suggest` | `Catalogue` | n/a |
| PUT | `/api/catalog/products/{id}/path` | `Catalogue` | token |
| GET | `/api/catalog/paths/resolve?path=` | `Catalogue` for now (S9 decides how the storefront calls it: in-process through `IProductPathQueries`) | n/a |
| GET | `/api/catalog/products/{id}/path-history` | `Catalogue` | n/a |

## Events
`ProductChangedEvent` on a path change of a published product (the storefront invalidates the old and new path, and S9's sitemap swaps the entry).

## Data
Unique (`GNDR_ID`,`CATG_ID`,`SLUG`) on `PRDT_MAIN` and unique triple on `PRDT_SLUG_HIST`; the current triple is also written to history at first publish, so a lookup only needs history. Resolution is one indexed query; a `Gone` result is a product with status Archived.

## NFR / security design
NFR-01: one indexed read, no joins beyond the product row. Slug input is normalised to a closed alphabet so no traversal, encoding or look-alike tricks survive. Reserved-word list in configuration.

## Frontend
URL tab: shows the current path read-only with a "Change URL" form (gender and category are changed on the Content tab; the slug is edited here with the suggestion and a live preview of the final path), the path history as a list with dates, and a warning that changing a published URL creates a 301 from the old one.

## Risks & alternatives considered
Unique per (gender, category) means moving a product between categories can collide: the move is refused with the colliding product named. A global slug was rejected: it forces ugly suffixes between boys and girls items.

## Security & coupling review
Shared rows: see the [module plan](../plan.md#security--coupling-review-sprint-level). Feature-specific:

| Area | How this feature meets it |
|---|---|
| Authorization | `Policies.Catalogue` |
| Input validation | slug alphabet, length and reserved words enforced by the pure policy and by a check constraint on the pattern |
| Output encoding | paths are built from validated codes and slugs only; the resolve endpoint echoes a path it generated, not the user's input |
| Concurrency | unique triple; a lost race on a slug returns 409 and the user picks another |
| Abuse cases | `../`, `%2e%2e`, mixed case, a very long or Unicode look-alike slug (normalised or refused); a reserved word such as `admin`; claiming an old slug of another product to hijack its traffic (history rows are permanent and unique) |
