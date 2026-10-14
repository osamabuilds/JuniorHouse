# Variants: SKU codes and GTIN-13 assignment

- **Status:** Implemented (2026-10-14; split from the approved Sprint 5 spec, acceptance criteria renumbered `VAR-n`; see "Implementation notes" in the [module README](../README.md))
- **Jira:** SCRUM-198 (epic SCRUM-16); tasks SCRUM-96 (FR-SC-06)
- **BRD sections:** §5.12 (Barcode Standard & Structure), §9.10.1
- **Requirement IDs:** FR-SC-06, NFR-SC-03, NFR-SC-05, NFR-FT-08
- **Module context:** [README](../README.md), [module design](../plan.md), [ADR 0009](../../../adr/0009-gtin-allocation-and-numbering.md)

## Problem & intent

Every sellable unit needs a unique, scannable identity. Each variant (style × colour × size) must get exactly one GTIN-13 and a readable SKU code that staff can say aloud, the numbers must never repeat or be reused, and Osama must be able to retire a number without it ever coming back.

## Requirements (quoted from the BRD)

> **FR-SC-06:** System shall generate a unique GTIN-13 per style×colour×size variant and prevent duplicate assignment.

> **NFR-SC-03:** GTIN assignment must be strictly sequential/non-reusable — a rejected/voided assignment is retired, never recycled to a different product.

> Each sellable variant (style × colour × size) gets its own GTIN-13 (EAN-13) … Internally, Romp's own SKU code (human-readable, e.g. ROMP-RMP-RED-0203) maps 1:1 to the GTIN in the database — staff work with the readable SKU, systems scan the GTIN.

## User flow

1. When `label-work` hands over lines for a style, this feature finds or creates each **variant** and, if it has no active GTIN, assigns the next one (company prefix + next sequential item reference + check digit) and the **SKU code** `ROMP-{STYL_CODE}-{CLR_CODE}-{SIZE_CODE}` (D3). A variant that already has a GTIN keeps it (D9).
2. Staff browse the **SKU register**: variant, SKU, GTIN, status (Active / Retired), print history.
3. Staff can **void** a variant's GTIN at any time with a reason (D8). It becomes Retired; its number is never issued again; the variant's next accepted quantity gets the next number. Units already tagged are not touched.
4. A banner warns when 80% of the GTIN capacity is used.

## Acceptance criteria

| ID | Given / When / Then | Source |
|---|---|---|
| VAR-1 | Given a variant with no GTIN, when its first accepted quantity arrives, then it gets exactly one valid GTIN-13 (13 digits, correct check digit, starting with the configured company prefix). | FR-SC-06 |
| VAR-2 | Given a variant that has an active GTIN, when more accepted quantity arrives (later delivery or rework re-accept), then the same GTIN and SKU are reused. | FR-SC-06 |
| VAR-3 | Given two different variants, then their GTINs differ, and one variant can never hold two active GTINs (enforced by a partial unique index, not only code). | FR-SC-06 |
| VAR-4 | Given many assignments at the same moment, when they run concurrently, then every GTIN and item reference is unique. | FR-SC-06, NFR-SC-03 (new) |
| VAR-5 | Given a GTIN has been voided, when later assignments happen, then the voided number is never issued again to any variant. | NFR-SC-03 |
| VAR-6 | Given an assignment, then its item reference is the next number in a strictly increasing sequence for that prefix; a rolled-back assignment leaves no gap and no duplicate. | NFR-SC-03 |
| VAR-7 | Given a variant, when its SKU code is built, then it is `ROMP-{STYL_CODE}-{CLR_CODE}-{SIZE_CODE}`, unique because `STYL_CODE` is unique, and maps 1:1 to the variant's active GTIN. | §5.12, D3 |
| VAR-8 | Given a void request at any time, then it needs a reason code and acting person, the GTIN becomes Retired, and the time, reason and person are recorded. | NFR-SC-03, NFR-SC-05, D8 |
| VAR-9 | Given a variant whose GTIN was voided, when more accepted quantity arrives, then it gets a new GTIN and the same SKU code. | NFR-SC-03 (new) |
| VAR-10 | Given a new assignment, when it commits, then `VariantGtinAssigned` v1 (style, colour, size, SKU, GTIN) is in the outbox in the same transaction; a void publishes `GtinVoided` v1. Payloads hold no personal data. | §9.10.3 (new), NFR-FT-08 |
| VAR-11 | Given the configured company prefix, when the application starts, then it is validated (7-9 digits); a test prefix (starting `02`) is refused when the environment is Production. | D2 |
| VAR-12 | Given the item reference would pass the capacity of the prefix, when an assignment is attempted, then it fails with a clear error and assigns nothing (no wrap-around); at 80% usage the capacity endpoint reports a warning. | ADR 0009 (new) |
| VAR-13 | Given the SKU register, when it loads, then it is paged (1..100), searchable by SKU or GTIN, ordered by a unique key, with no N+1, and renders as a semantic, keyboard-operable screen. | NFR-17, NFR-19, CONVENTIONS.md |
| VAR-14 | Given a GTIN value from outside (an import or request), when it is parsed, then a wrong length, non-digit or bad check digit is rejected. | (new, security) |

## Non-functional constraints

NFR-SC-03, NFR-SC-05, NFR-FT-08, plus the security baseline in the [plan](plan.md).

## Out of scope

Printing and reprinting (print-jobs); what putaway or picking do when they scan a Retired GTIN (Q14, decided in Sprints 6 and 14).

## Open questions

- [ ] Q14 as in the [module README](../README.md); default adopted.
