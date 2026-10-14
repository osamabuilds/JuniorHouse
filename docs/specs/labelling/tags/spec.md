# Tags: what is printed on the price/barcode tag

- **Status:** Implemented (2026-10-14; split from the approved Sprint 5 spec, acceptance criteria renumbered `TAG-n`; see "Implementation notes" in the [module README](../README.md))
- **Jira:** SCRUM-198 (epic SCRUM-16)
- **BRD sections:** §5.12 (What Gets Printed, and Where)
- **Requirement IDs:** FR-SC-06 (scannable identity), NFR-SC-02 (rendering must not depend on the printer)
- **Module context:** [README](../README.md), [module design](../plan.md), [ADR 0010](../../../adr/0010-label-printing-queue-and-printer-port.md)

## Problem & intent

The tag is the only place the GTIN physically exists. It has to show the right content, the barcode has to scan, and the text on it (product names are staff-entered) must never be able to alter the printer's commands. Staff also need to see what will be printed before they print it.

## Requirements (quoted from the BRD)

> **Price / Barcode Tag** — Attached to: Garment (swing tag, string or safety-pin attached). Contains: Brand logo, product name, size, GTIN barcode, MRP/price, 'Made in Pakistan'.

> Each sellable variant (style × colour × size) gets its own GTIN-13 (EAN-13) …

Decisions that apply (see [README](../README.md)): D4 (price from Catalog at print time), D5 (no care or carton labels), D6 (concession mark), D7 (ZPL first, PDF preview).

## User flow

1. When a job is queued or previewed, this feature builds the **tag document** from the variant (SKU, GTIN), the style (name, price from Catalog), the colour and size names, and the outcome (Accepted or Concession).
2. For preview it renders a **PDF**; for the printer it renders **ZPL**. Both are made from the same tag data.
3. A Concession tag shows the same price plus a small "2ND" corner mark (D6).

## Acceptance criteria

| ID | Given / When / Then | Source |
|---|---|---|
| TAG-1 | Given a variant, when a tag is built, then it contains brand logo, product name, size, GTIN barcode, price and "Made in Pakistan". | §5.12 |
| TAG-2 | Given the rendered EAN-13, when it is checked, then it encodes the stored 13 digits with a valid check digit (the PDF encoder is verified against known EAN-13 patterns; ZPL uses the printer's own EAN-13 command). | §5.12 |
| TAG-3 | Given the price, when it is built, then it is the style's target retail price read from Catalog at print time (a `decimal`), formatted for PKR, never entered by hand and never a float. | D4, CLAUDE.md money rule |
| TAG-4 | Given a Concession quantity, when its tag is built, then the price equals Accepted stock's and a small "2ND" mark is added; no discount is printed. | D6, §5.11 |
| TAG-5 | Given a reprint, when the tag is built, then it uses the current price, so reprinting is the remedy for a price change. | D4 |
| TAG-6 | Given text containing printer-command characters (`^`, `~`, `_`, `\`, quotes, control characters), when ZPL is produced, then those characters are hex-escaped or removed so no input can add, change or end a printer command. | (new, security) |
| TAG-7 | Given a long product name or unusual characters, when the tag is built, then text is shortened deterministically to fit with an ellipsis, only printable characters are kept, and the barcode is never altered. | (new) |
| TAG-8 | Given the same job, then the preview PDF and the ZPL are built from identical tag data, so what staff approve is what prints. | (new) |
| TAG-9 | Given the tag builder, then it has no dependency on any printer, file system or network, so it is testable in isolation. | NFR-SC-02 (new) |

## Non-functional constraints

NFR-SC-02 (rendering is independent of the printer), plus the security baseline in the [plan](plan.md).

## Out of scope

Care labels, carton labels, shipping labels (D5); queueing and printing (print-jobs); the artwork of the brand logo beyond a fixed bitmap/ZPL graphic supplied by the owner.

## Open questions

- [ ] The brand logo file for ZPL (a monochrome bitmap) and the label size in millimetres are needed before the first real print (D7 allows building against defaults: 50 mm × 30 mm until told otherwise).
