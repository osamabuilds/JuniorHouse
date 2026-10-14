# URL convention and slugs: tasks

- [x] **U1** `SlugPolicy.Normalise/Validate/WithSuffix` (ASCII fold, lowercase, hyphens, 80 characters, reserved words, pattern). *Proof:* `SlugPolicyTests` (URL-4, URL-6, URL-7, URL-8; table of inputs including diacritics, `../`, `%2e%2e`, Unicode look-alikes, a 200-character name).
- [x] **U2** `ProductPath` (build, parse, canonicalise lowercase and trailing slash). *Proof:* `ProductPathTests` (URL-1, URL-5, URL-10).
- [x] **U3** `PathHistory` rules inside `Product.ChangePath`: old triple kept, chains collapse by design, a triple owned by another product is refused. *Proof:* `ProductPathHistoryTests` (URL-2, URL-3, URL-11).
- [x] **U4** `PathResolution` (`Current`, `Moved`, `Gone`, `NotFound`) in a domain service. *Proof:* `PathResolutionTests` (URL-2, URL-9).
- [x] **U5** EF configuration: unique (`GNDR_ID`,`CATG_ID`,`SLUG`) on `PRDT_MAIN`, `PRDT_SLUG_HIST` with the unique triple, check constraint on the slug pattern. *Proof:* `UrlSchemaTests` (duplicate slug refused, bad pattern refused by the database).
- [x] **U6** `SuggestSlug` (first free slug, one query). *Proof:* `SuggestSlugTests.TwoProductsWithTheSameName_GetStableDistinctSlugs` (URL-4, including 10 parallel creates).
- [x] **U7** `SetProductPath` command with token and event for a published product. *Proof:* `SetProductPathTests` (URL-1..3, URL-11, event written in the same transaction).
- [x] **U8** `ResolvePath` query and `IProductPathQueries` contract. *Proof:* `ResolvePathTests` (one indexed query; Archived gives `Gone`).
- [x] **U9** Endpoints (suggest, set path, resolve, history) behind `Policies.Catalogue`. *Proof:* `UrlEndpointsTests` over HTTP (409 on a taken slug, 400 on a reserved one); `EndpointSecurityTests`.
- [x] **U10** Write ADR status to Accepted and the URL tab in the product editor (read-only path, change form, history list). *Proof:* `UrlTabComponent` spec (labels, preview, 301 warning text) and the reducer spec.
- [x] **U11** e2e: change a slug of a published product and see the old path resolve as moved. *Proof:* `urls.e2e.spec.ts`.
- [x] **U12** Card → Implemented; traceability rows URL-1..11, SEO-02. *Proof:* `StructureSyncTests`.

Built as planned unless noted in the spec's "As built" section. The test classes named in the Proof column were refined during the build; the tests that actually prove each criterion are listed in [../traceability.md](../traceability.md).
