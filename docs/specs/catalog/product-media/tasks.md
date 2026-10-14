# Product media: tasks

- [ ] **M1** `AltText.Validate` and `Suggest`. *Proof:* `AltTextTests` (MED-2, MED-3: empty, whitespace, `IMG_1234.jpg`, `DSC00012.JPG`, repeated keywords, 126 characters, digits only; valid text accepted).
- [ ] **M2** `ImageSniffer` for JPEG, PNG and WebP (VP8, VP8L, VP8X): type from leading bytes and dimensions from the header. *Proof:* `ImageSnifferTests` (MED-7, MED-10: valid files give type and size; a renamed text/HTML/EXE file, a truncated header and a zero-size image are refused; sample bytes built in the test, no binary fixtures).
- [ ] **M3** `ProductImage` entity and `MediaGate` (at least one image, every image valid alt, one failure per offending image). *Proof:* `MediaGateTests` (MED-1, MED-6).
- [ ] **M4** `PRDT_IMG` configuration, check constraint on alt, trigger for published products. *Proof:* `MediaSchemaTests` (MED-5: the database refuses an empty alt on a published product's image by any route).
- [ ] **M5** `UploadProductImage` (size and count limits, generated key via `IFileStorage`, dimensions stored). *Proof:* `UploadProductImageTests` (MED-7, MED-9: client file name never used, over-limit refused, 11th image refused, storage failure leaves no database row).
- [ ] **M6** `SetImageAlt`, `ReorderImages` (exactly the product's images), `DeleteImage` (blocked when it would leave a published product without an image; file removal after commit). *Proof:* `ImageEditTests` (MED-2, MED-8, delete rule, failed file delete does not roll back).
- [ ] **M7** `ListProductImages` and the file endpoint (our content type, `nosniff`). *Proof:* `ImageQueryTests`, `ImageEndpointsTests` (headers asserted).
- [ ] **M8** Endpoints behind `Policies.Catalogue`, body size limit and strict rate limit on upload. *Proof:* `MediaEndpointsTests` over HTTP (multipart with two files refused, oversize 413), `EndpointSecurityTests`.
- [ ] **M9** `ProductChangedEvent` when an image of a published product changes. *Proof:* `MediaEventTests`.
- [ ] **M10** Web Images tab: picker with label, list with `<img width height alt>`, editable alt with suggestion and error text, order buttons, delete confirmation. *Proof:* component and reducer specs (keyboard operable, errors shown, first image marked main).
- [ ] **M11** e2e: upload, leave alt empty, see the gate name the image, fix it, publish. *Proof:* `product-media.e2e.spec.ts`; screen in the layout suite.
- [ ] **M12** Card → Implemented; traceability rows MED-1..11, SEO-07. *Proof:* `StructureSyncTests`.
