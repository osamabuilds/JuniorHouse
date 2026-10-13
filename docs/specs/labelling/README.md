# Labelling

Label (BRD §5.12): SKU and GTIN-13 generation, the tag print queue, and reprints. Accepted stock gets an identity and a physical tag before it can be shelved (S6) or sold (S7+). Planned for Sprint 5; no code exists yet.

**Schema:** `SKU` · **Code:** `src/api/src/Modules/Labelling/` · **Web:** `features/labelling/` · **Jira:** SCRUM-198 under epic SCRUM-16 · **Rules:** [constitution](../../CONSTITUTION.md) · **Module design:** [plan.md](plan.md)

## Features

| Feature | What it does | Status |
|---|---|---|
| [label-work](label-work/README.md) · [spec](label-work/spec.md) · [plan](label-work/plan.md) | Accepted batches awaiting tags; the worklist | Planned |
| [variants](variants/README.md) · [spec](variants/spec.md) · [plan](variants/plan.md) | SKU variants, GTIN assignment, void, SKU register | Planned |
| [print-jobs](print-jobs/README.md) · [spec](print-jobs/spec.md) · [plan](print-jobs/plan.md) | Print queue, retry, reprint | Planned |
| [tags](tags/README.md) · [spec](tags/spec.md) · [plan](tags/plan.md) | Tag content and rendering (ZPL, PDF preview) | Planned |

A feature marked Planned has no code yet, so the structure test excludes it until it is built.

## Requirement IDs for the module

FR-SC-06, FR-SC-07, NFR-SC-02, NFR-SC-03, NFR-SC-05, NFR-FT-08, NFR-17, NFR-19. No `TC-*` cases exist in the BRD for this area, so every acceptance criterion is derived from the BRD text and marked `(new)` where the BRD is silent. Acceptance-criterion IDs are prefixed by feature: `LW-` (label-work), `VAR-` (variants), `PJ-` (print-jobs), `TAG-` (tags).

## BRD text (shared by all four features)


> **§5.12 SKU & Barcode Generation, Physical Labelling.** Every sellable unit needs a unique, scannable identity before it can be shelved or sold. The industry-standard mechanism is GS1's GTIN (Global Trade Item Number) — the same barcode standard used by every retailer worldwide, and the only one that will still work cleanly if Romp ever sells through a marketplace or physical retail partner later.
>
> **Barcode Standard & Structure**
> - Romp registers a GS1 Pakistan company prefix (one-time registration + annual fee) — this prefix is the root of every barcode Romp will ever issue.
> - Each sellable variant (style × colour × size) gets its own GTIN-13 (EAN-13) — e.g. a red romper in size 2-3Y and the same romper in size 4-5Y are two different GTINs, per standard apparel practice.
> - Carton/outer packaging (for vendor-to-warehouse bulk shipping, not customer parcels) can optionally use GS1-128 logistics labels with an SSCC (Serial Shipping Container Code) once volumes justify it — not needed at MVP.
> - Internally, Romp's own SKU code (human-readable, e.g. ROMP-RMP-RED-0203) maps 1:1 to the GTIN in the database — staff work with the readable SKU, systems scan the GTIN.
>
> **What Gets Printed, and Where**
> - Price / Barcode Tag — attached to the garment (swing tag, string or safety-pin attached). Contains: Brand logo, product name, size, GTIN barcode, MRP/price, 'Made in Pakistan'.
> - Care Label — sewn into the garment (neck or side seam). Contains: Fabric composition, wash/dry/iron instructions, size, safety notice per age group where applicable.
> - Carton Label (inbound, vendor→warehouse) — bulk shipping cartons from the vendor. Contains: PO number, style, quantity, size/colour breakdown — vendor-generated or Romp-generated at receipt.
>
> Tag printing happens in-house on a barcode/label printer (thermal transfer, e.g. Zebra/TSC-class) once QC accepts a batch — not outsourced per-order, since the same SKU is printed in bulk once and applied to every accepted unit of that batch.
>
> Edge case: a re-print is needed (tag lost/damaged in warehouse) → the system must allow re-printing an existing GTIN's tag without generating a new barcode, so historical sales data for that SKU stays continuous.

> **FR-SC-06:** System shall generate a unique GTIN-13 per style×colour×size variant and prevent duplicate assignment.

> **FR-SC-07:** System shall support re-printing an existing SKU's tag without generating a new GTIN.

> **NFR-SC-02:** Label/tag printers must support a queued, retry-on-jam printing model — a single printer fault must not halt the SKU-GEN or PACK workflow for other items in queue.

> **NFR-SC-03:** GTIN assignment must be strictly sequential/non-reusable — a rejected/voided assignment is retired, never recycled to a different product.

> **NFR-SC-05 (inventory-adjusting actions):** All inventory-adjusting actions … must be attributable to a user/session and timestamped …

> **§9.10.1 SKU & Barcode Generation (SKU-GEN):** GTIN/barcode assignment, price-tag and care-label data, tag print jobs.

> **§9.10.2:** Catalog (v8.0) gains a hard dependency on SKU-GEN — a product variant cannot go 'live' on the storefront until it has a GTIN and passed QC.

> **§9.10.3 Event catalogue:** `QC.BatchAccepted` — published by QC, consumed by SKU-GEN (on accept).

> **§12.11:** SKU & Barcode core tables: ProductVariantGTINs, TagPrintJobs.

*(Scope notes. (1) Putaway, bins and "available" stock are Sprint 6 (FR-SC-08); this sprint only produces the identity and the physical tag. (2) The storefront product variant (`ProductVariants`) is created in Sprint 7 (Publish), so the GTIN is keyed on style × colour × size, the same three ids PROD and QC already use; S7 attaches it to the variant. (3) Carton/GS1-128 labels are "not needed at MVP" per the BRD and are out of scope. (4) The GS1 company prefix has not arrived (roadmap lead-time item), so a test prefix is used until it does: D2.)*


## Builds on Sprints 1-4 as delivered

- QC publishes `QcBatchAccepted` v1 (`Quality.Contracts`) once per accepted or concession sub-batch: batch id, sub-batch id, GRN id, PO id/no, outcome code (`Accepted` | `Concession`) and lines (size id, colour id, qty). It fires only for released quantity. It does **not** carry a style id.
- `IQualityQueries` returns released lines per PO or per batch, never rejected, rework, pending or undecided quantity.
- `Vendor.Contracts.IPurchaseOrderQueries` gives a PO's in-force terms, including its single style id. `Catalog` owns `Style`; sizes, colours and fabrics are `Reference` lookups.
- The outbox, per-module inbox idempotency, business numbers (ADR 0006), and the acting-person name (no authentication until S8) work as in S3 and S4.

## Decisions (owner answers, 2026-10-14)

- **D1: Tickets.** Sprint 5 tickets are created now; this sprint is SCRUM-198 under epic SCRUM-16. (Jira handed out SCRUM-197 first, which Sprint 4 already used as its provisional key, so that issue was repurposed as Sprint 4's ticket and this one was created as SCRUM-198.)
- **D2: Test prefix.** Use the GS1 restricted-circulation range 020-029 as the test company prefix. A configuration switch makes the app refuse to start with a test prefix in a production environment. When the real Pakistan prefix is applied for, request a long-term-capacity prefix (7-8 digits) rather than the cheapest tier.
- **D3: SKU format.** `ROMP-{STYL_CODE}-{CLR_CODE}-{SIZE_CODE}`, from the style's unique `STYL_CODE` and the `CODE` column of the colour and size lookups. No new abbreviation algorithm or collision check; uniqueness is inherited from `STYL_CODE`.
- **D4: Tag price.** Print the style's target retail price (decimal, from Catalog) at print time. Do not wait for S7 and do not ask for manual entry. A batch printed before the latest target-price revision gets a non-blocking flag; the existing FR-SC-07 reprint is the remedy (AC-23a).
- **D5: Care and carton labels.** Romp neither prints nor sews care labels (the vendor sews them in; QC already checks their content). SKU-GEN keeps no copy of care-label data; it reads whatever it needs from the Catalog style. No carton labels.
- **D6: Concession mark.** Same price as Accepted stock plus a small "2ND" corner mark. The discount stays a Publish-time (S7) commercial decision and is never printed on the tag.
- **D7: Printer.** A printer interface with a fake/PDF-preview implementation now, and ZPL as the first real format.
- **D8: Void.** A GTIN can be voided at any time with a mandatory reason. Voiding blocks future prints and reprints and prevents reuse of the number, and never touches units already tagged.
- **D9: Lazy assignment.** A GTIN is assigned only when a variant's first accepted quantity arrives.
- **D10: Spare tags.** A spare-tag allowance is a configuration value, default 0%.
- **D11: One style per PO.** Every PO maps to exactly one style (multi-style POs are out of scope since Sprint 1/2), so SKU-GEN can rely on it.
- **D12: Reprints.** Logged, no limit.


## Module decisions

D1-D12 above apply to every feature of this module. Technical decisions: [ADR 0009](../../adr/0009-gtin-allocation-and-numbering.md) (GTIN numbering), [ADR 0010](../../adr/0010-label-printing-queue-and-printer-port.md) (print queue and printer port), [ADR 0011](../../adr/0011-security-and-coupling-baseline.md) (security and coupling baseline).

## Out of scope for the module

- Putaway, bins, stock ledger, available-to-promise (Sprint 6, FR-SC-08).
- Storefront product variants, publishing and prices (Sprint 7); only the GTIN and SKU are handed over.
- Carton/GS1-128 SSCC labels and inbound carton labels (not needed at MVP, D5); care labels (D5).
- Shipping labels, waybills and packing slips (Sprints 14-15); scan-to-confirm at pick/pack (NFR-SC-01, Sprint 14).
- Buying or integrating a specific printer model (D7).

## Open questions (carried from the approved spec; each has a default the plans adopt)

- [ ] **Q13: Catalog contract.** `IStyleQueries` has no price, name or price-change time, and only returns active styles. Default: add `TGT_PRIC_CHG_DTE` to `CTLG.STYL_MAIN` (set only when the price changes) and a `GetLabelInfoAsync(styleIds)` query returning code, name, price, that timestamp and the active flag. Touches Catalog (additive migration).
- [ ] **Q14: Voided GTIN on tagged stock.** Scan handling of a Retired barcode (putaway S6, pick S14) is decided in those specs.
- [ ] **Q15: Retired style.** Default: labelling proceeds once staff confirm; the UI shows the style is retired.

Back to the [spec index](../README.md).
