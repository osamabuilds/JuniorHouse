# SCRUM-201: Product media, technical plan

- **Spec:** [spec.md](spec.md) · **Module design:** [../plan.md](../plan.md) (image handling, settings, shared security review)
- **Module(s):** Catalog

## Domain model
- **`ProductImage`** (entity of the product): storage key, content type, byte size, width, height, optional colour, sort order, alt text (nullable while draft).
- **`AltText`** (value object, pure): `Validate(text, productName)` rejects empty/whitespace, file-name patterns (`IMG_1234.jpg`, `DSC…`, anything ending in an image extension), more than 125 characters, a word repeated more than 3 times, or only digits/punctuation; `Suggest(productName, colourName, viewIndex)` gives "{name} in {colour}, view {n}" for staff to edit.
- **`ImageSniffer`** (pure, Infrastructure): decides type from leading bytes and reads width and height (JPEG SOF marker, PNG IHDR, WebP VP8/VP8L/VP8X); anything else is refused.
- **Gate (`IMediaGate`)**: at least one image, and every image has valid alt text; returns one failure per offending image, naming it by position and file name (MED-1).

## Application layer
| Command / Query | Input | Output | Covers |
|---|---|---|---|
| `UploadProductImage` | product id, stream, colour (optional) | image with a suggested alt | MED-7, MED-9, MED-10 |
| `SetImageAlt` | image id, text | image | MED-2, MED-3 |
| `ReorderImages` | product id, ordered ids (must be exactly the product's images) | images | MED-8 |
| `DeleteImage` | image id | none (file deleted after commit; a failed delete is logged and retried by a cleanup job, never rolls back) | `(new)` |
| `ListProductImages` | product id | ordered images | MED-4, MED-8 |
| `MediaGate.Evaluate` | product id | failures | MED-1, MED-6 |

## API endpoints
| Method | Route | Auth / role | Ownership check |
|---|---|---|---|
| GET | `/api/catalog/products/{id}/images` | `Catalogue` | n/a (staff data) |
| POST | `/api/catalog/products/{id}/images` (multipart, one file) | `Catalogue` (strict rate limit) | product exists and is not archived |
| PUT | `/api/catalog/images/{imageId}/alt` | `Catalogue` | image belongs to a non-archived product |
| PUT | `/api/catalog/products/{id}/images/order` | `Catalogue` | all ids belong to the product |
| DELETE | `/api/catalog/images/{imageId}` | `Catalogue` | blocked if it would leave a published product with no image |
| GET | `/api/catalog/images/{imageId}/file` | `Catalogue` | n/a; returns the original with `Content-Type` from our record, `nosniff`, `Content-Disposition: inline` |

## Events
`ProductChangedEvent` (from Products) is raised when an image of a published product changes. No new event type.

## Data
`PRDT_IMG` (module plan). Check constraint: `ALT` null or `length(btrim(ALT)) BETWEEN 1 AND 125`. Trigger: a product may not move to Published, and a published product's image may not be set to a null/empty alt or be inserted without alt (MED-5). Index on (`PRDT_ID`, `SORT_SEQ`). Reordering is one update statement.

## NFR / security design
Body size limit on the upload route; type by sniffing; generated storage key; original kept; dimensions stored for layout stability (MED-10). Files served through our endpoint with `X-Content-Type-Options: nosniff` (security headers exist from S5). Rate limit `strict` on upload.

## Frontend
Images tab in the product editor: file picker (labelled), a list with thumbnails (`<img>` with `width`/`height`, alt text shown in the editable field), up/down buttons for ordering (keyboard operable; drag and drop optional later), per-image alt field with the suggestion pre-filled and error text, delete with confirmation. Never a clickable `div`.

## Risks & alternatives considered
No image library (licence and size); header parsing covers only the three accepted formats. Derivatives are deferred to S9 (D5). Orphan files (a delete that fails after commit) are tolerated and swept by a small cleanup, because a storage error must not undo a catalogue change.

## Security & coupling review
Shared rows: see the [module plan](../plan.md#security--coupling-review-sprint-level). Feature-specific:

| Area | How this feature meets it |
|---|---|
| Authorization | `Policies.Catalogue`; archived products' media is read-only |
| Input validation | sniffing, size, count, alt rules; multipart with exactly one file part |
| Output encoding | alt text is data; the file endpoint sets `nosniff` and our content type, never the client's |
| Resource limits | `MaxImageBytes`, `MaxImagesPerProduct`, request size limit, strict rate limit |
| Abuse cases | a script or HTML file renamed `.jpg`; a polyglot file with a valid image header (served with our fixed type and `nosniff`); 1,000 uploads to exhaust disk (count and rate limits); alt text of `<script>` (stored as text, escaped on render) |
| Coupling | storage only through `IFileStorage`; no other module touches the files |
