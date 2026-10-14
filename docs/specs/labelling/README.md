# Labelling

Label (BRD §5.12): SKU and GTIN-13 generation, the tag print queue, and reprints. Accepted stock gets an identity and a physical tag before it can be shelved (S6) or sold (S7+). Implemented in Sprint 5 (SCRUM-198).

**Schema:** `SKU` · **Verification:** [traceability](traceability.md) · **Code:** `src/api/src/Modules/Labelling/` · **Web:** `features/labelling/` · **Jira:** SCRUM-198 under epic SCRUM-16 · **Rules:** [constitution](../../CONSTITUTION.md) · **Module design:** [plan.md](plan.md)

## Features

| Feature | What it does | Status |
|---|---|---|
| [label-work](label-work/README.md) · [spec](label-work/spec.md) · [plan](label-work/plan.md) | Accepted batches awaiting tags; the worklist | Implemented |
| [variants](variants/README.md) · [spec](variants/spec.md) · [plan](variants/plan.md) | SKU variants, GTIN assignment, void, SKU register | Implemented |
| [print-jobs](print-jobs/README.md) · [spec](print-jobs/spec.md) · [plan](print-jobs/plan.md) | Print queue, retry, reprint | Implemented |
| [tags](tags/README.md) · [spec](tags/spec.md) · [plan](tags/plan.md) | Tag content and rendering (ZPL, PDF preview) | Implemented |

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

- [x] **Q13: Catalog contract.** `IStyleQueries` has no price, name or price-change time, and only returns active styles. Default: add `TGT_PRIC_CHG_DTE` to `CTLG.STYL_MAIN` (set only when the price changes) and a `GetLabelInfoAsync(styleIds)` query returning code, name, price, that timestamp and the active flag. Touches Catalog (additive migration).
- [ ] **Q14: Voided GTIN on tagged stock.** Scan handling of a Retired barcode (putaway S6, pick S14) is decided in those specs.
- [x] **Q15: Retired style.** Default: labelling proceeds once staff confirm; the UI shows the style is retired.

Back to the [spec index](../README.md).

## Implementation notes (what was built, and where it differs from the plan)

The spec describes what was built; these are the places the build chose differently from the plan or went beyond it.

1. **No PDF library.** The tag preview is a one-page PDF written by hand (`TagPdf`), so ADR 0010's PDFsharp package was not added. The tests verify the cross-reference table and the escaping.
2. **Printers are lookup rows.** `SKU.PRTR_LKP` (smallint id, format, address), seeded with `FAKE-PDF` and `FAKE-JAM`, rather than a `PRTR_MAIN` table. The fake printers are refused unless `Labelling:AllowFakePrinters` is on, and the host will not start in Production with it on.
3. **The callee declares the interface.** The plan said the caller declares the port; in the build each feature publishes its own public interface (`IVariantAssigner` and `IVariantReader` in variants, `IPrintProgress` in print-jobs, `ITagDocumentBuilder` in tags). The dependency direction is as planned and is enforced by `LabellingFeatureDependencyTests`. Tags reads variant identity through `IVariantReader`, so tags depends on variants (one way).
4. **Queueing from a work item lives in label-work.** `QueueTagsCommand` (and `POST /api/labelling/work-items/{id}/print-jobs`) works out how many tags are still allowed under a row lock and then sends print-jobs' `QueueOriginalPrintJobCommand`. This keeps print-jobs ignorant of work items. print-jobs owns the queue, retry, reprint and history.
5. **Reprint and history screens.** The SKU register links to the Print queue screen (`?variantId=…&reprint=1&sku=…`), which owns the reprint form and the per-SKU history; variants never imports print-jobs.
6. **Price flag (LW-5).** It compares the style's last price change with the latest non-cancelled job (original or reprint) for the variant. The "price changed" filter evaluates it over the variants that have jobs, because the price lives in Catalog.
7. **SKU length.** The SKU column and pattern allow 120 characters (three 30-character parts plus the brand). Parts are upper-cased and reduced to letters, digits and single hyphens; the unique index is the final guard against two styles producing the same SKU.
8. **Brand on the tag** is the text "ROMP"; the monochrome logo bitmap and the real label size are still to be supplied (the defaults are 50 mm × 30 mm).
9. **Poison QC messages.** A message the mapper refuses throws in the consumer, so the dispatcher retries and then dead-letters it with the reason, as the spec asks.
10. **Quick "Try again" on a parked item** uses the remembered acting person, or "Warehouse staff" when none is remembered.
11. **Security baseline (ADR 0011) implemented with this sprint:** `MapSecuredGroup` for every endpoint of every module, the development-only authentication handler, the deployment gate (the host refuses to start outside Development/Testing without real authentication), per-client rate limits with a strict policy, baseline response headers, an explicit request-size ceiling, and `/api/admin/outbox` moved under the `Admin` policy.
12. **Migrations:** `InitialLabelling` and `RenameManualActionColumn` (the column is `MNL_BY`, because `ACT` is the glossary abbreviation for "active"). Catalog gained `AddStylePriceChangedAt` (`TGT_PRIC_CHG_DTE`).

Known gaps carried forward: the real Zebra/TSC printer has only been exercised against a loopback TCP listener (hardware is not chosen, D7); what putaway and picking do when they scan a retired barcode is decided in Sprints 6 and 14 (Q14); the GS1 company prefix is still the test range until the real one is issued (D2); and no authentication exists until Sprint 8, so "who" is the acting-person name plus the system actor.
