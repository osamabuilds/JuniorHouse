# SCRUM-201: Product media (images and alt text)

- **Status:** Approved (owner decisions 2026-10-14; open questions below use the stated defaults)
- **Jira:** SCRUM-201 (epic SCRUM-12); source tickets SCRUM-78 (media), SCRUM-154
- **BRD sections:** §7.2 (FEAT-PDP-01), §12.3.2, §14.1
- **Requirement IDs:** SEO-07, FR-53, FR-07 (gallery, rendered in S9), NFR-19

## Problem & intent
Shoppers buy clothes by looking, and screen-reader and image-search users need every picture described. Staff must be able to upload, order and describe product images, and a product with an undescribed image must not go live.

## Requirements (quoted from the BRD)
> **SEO-07:** The ProductImages.AltText column (Section 12.3.2) shall be a required, non-empty field at publish time, not optional — every product image needs descriptive alt text.

> **§12.3.2** `ProductImages`: PK Id; FK ProductId; VariantId FK (nullable); Url; SortOrder; AltText. "Gallery images (FEAT-PDP-01); variant-specific images override product-level ones."

> **FR-53:** Admin users shall create/edit products and variants (size × colour), pricing, media, size chart, and care info.

## User flow
1. Staff upload JPEG, PNG or WebP images for a product, or for one colour of it. Uploads are validated (type, size, content sniffed, not trusted by file name).
2. Each image gets a suggested alt text built from the product name, colour and view, which staff edit.
3. Staff set the order. The first image is the main one.
4. Drafts can be saved without alt text. **Publishing is blocked** until every image has meaningful alt text.

## Acceptance criteria
| ID | Given / When / Then | Source |
|---|---|---|
| MED-1 | Given an image without alt text, when publishing, then publish is blocked with a message naming the image | SCRUM-154 AC-1 |
| MED-2 | Given alt text that is empty, whitespace, a file name (e.g. `IMG_1234.jpg`), or a repeated-keyword string, then it is rejected | SCRUM-154 AC-2 |
| MED-3 | Given alt text longer than 125 characters, then it is rejected | SCRUM-154 |
| MED-4 | Given valid alt text, then it is returned with the image for rendering as the `alt` attribute, and is HTML-escaped when rendered | SCRUM-154 AC-3 |
| MED-5 | Given a published product, then the database refuses an empty alt on any of its images (defence in depth) | SCRUM-154 AC-5 |
| MED-6 | Given a product with no image, then publish is refused | SCRUM-78 (behaviour 4) |
| MED-7 | Given a file that is not JPEG, PNG or WebP (by content), or is over the size limit, then it is refused in plain words | SCRUM-78 (behaviour 3) |
| MED-8 | Given the images of a product, then their order can be changed and is returned in that order; a colour-specific image overrides product-level images for that colour | BRD §12.3.2 |
| MED-9 | Given an image, then the original is kept and the stored file is served with a reference only (no path traversal, no user-controlled names) | ADR 0003, secure-coding baseline `(new)` |
| MED-10 | Given the width and height of every image, then both are stored so the storefront can reserve layout space (no layout shift) | CLAUDE.md SEO images `(new)` |
| MED-11 | Given a bulk import row without alt text, then it is rejected | SCRUM-154 AC-4 `(out of scope with bulk import)` |

## Non-functional constraints
NFR-19 (WCAG 2.1 AA). Upload size limit and content sniffing per the existing storage building block (Sprint 2). No personal data in images (child photos are Phase 2: SEC-15/16 not applied).

## Out of scope
Resized WebP/AVIF derivatives and CDN delivery (see Q-MED-1), image editing/cropping, video, 360° views, try-on assets (descoped), bulk import.

## Decisions (owner, 2026-10-14)
- **D5 (was Q-MED-1)** Store the original image and its dimensions only. WebP/AVIF derivatives and their sizes are decided in Sprint 9, when the storefront defines the breakpoints it needs.

## Open questions
- [ ] **Q-MED-2 Limits.** Maximum file size (proposed 5 MB) and images per product (proposed 10).
- [ ] **Q-MED-3 Alt language.** English only for MVP (SCRUM-154 Q1). Proposed yes.
- [ ] **Q-MED-4 Where files live.** The shared storage building block from Sprint 2 stores files on local disk in development. Proposed: acceptable until the S8 staging server (object storage per ADR 0003 then).
- [ ] **Q-MED-5 Colour-specific images.** The BRD lets a colour's images override the product-level ones. Proposed: an image belongs to the product or to one colour of the style; the main image of a colour is its first image.
