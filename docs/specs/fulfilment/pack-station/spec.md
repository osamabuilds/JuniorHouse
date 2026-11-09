# Pack station

- **Status:** Draft, applied by delegation (owner away, 2026-11-09)
- **Jira:** to be created
- **BRD sections:** §5.16
- **Requirement IDs:** FR-SC-10, FR-SC-12, NFR-SC-01

## Problem & intent
A second, independent check before the parcel is sealed catches what the pick scan missed. The parcel is also weighed: a weight that does not fit the order is itself a sign something was missed or double-packed.

## Requirements (quoted from the BRD)
> **§5.16:** Picked items for one order arrive at the pack station; packer re-confirms items against the order (a second check, independent of the pick-scan, catches pick-list-vs-physical mismatches before the parcel is sealed).

> **§5.16:** Packaging material selection: poly mailer bag for soft goods (most apparel), or a corrugated box for multi-item / gift orders — driven by a simple rule (single soft item < X grams → poly bag; multi-item or > X grams → box), refined over time.

> **§5.16:** Parcel is weighed at the pack station — actual weight feeds both the courier's rate calculation and a discrepancy check against expected weight (a significant weight mismatch is itself a signal something was missed or double-packed).

> **FR-SC-12:** System shall capture parcel weight at the pack station and flag significant deviation from expected weight.

> **FR-SC-10:** ...require scan-to-confirm at both pick and pack stages.

## User flow
The packer opens a picked order, scans every unit again, sees the packaging to use, puts the parcel on the scale and enters the weight. If the weight fits, the pack is complete and the slip is printed. If not, the packer is asked to check, and may confirm with a note.

## Acceptance criteria

| ID | Given / When / Then | Source |
|---|---|---|
| PAK-1 | Given a picked order, when the packer scans each unit, then each GTIN is matched to a line and counted separately from the pick | §5.16 |
| PAK-2 | Given a unit that is not on the order or a surplus unit, when scanned, then it is refused and the parcel cannot be completed until it is removed (new) | §5.16 |
| PAK-3 | Given a missing unit, when the packer tries to complete, then completion is refused and the missing SKUs are listed (new) | §5.16 |
| PAK-4 | Given the order's items and weights, when packing starts, then the recommended packaging is shown by the rule in the module README (Q-FUL-1) | §5.16 |
| PAK-5 | Given the parcel is weighed, when the weight is entered in grams, then it is stored with the order and the staff member | FR-SC-12 |
| PAK-6 | Given a weight within the tolerance of the expected weight, when entered, then packing can complete | FR-SC-12 |
| PAK-7 | Given a weight outside the tolerance, when entered, then the packer is warned and must re-check; completing anyway needs a note and is flagged for staff (new) | FR-SC-12 |
| PAK-8 | Given a weight that is zero, negative or absurd (over 30 kg), when entered, then it is refused (new) | FR-SC-12 |
| PAK-9 | Given a completed pack, then the order is marked packed, an `OrderPackedEvent` is published (S15 books the courier from it) and the slip can be printed | §5.16, §5.17 |
| PAK-10 | Given an order that is not fully picked, when packing is opened, then it is refused (new) | §5.15 |
| PAK-11 | Given a cash on delivery order, when packing, then the amount to collect is shown large on screen | §5.16 |
| PAK-12 | Given a scan, when the server resolves it, then it answers in under 300 ms | NFR-SC-01 |
| PAK-13 | Given the pack is completed twice, then there is one packed state and one event (new) | NFR-09 |

## Out of scope
The courier booking, the shipping label and the waybill (S15). Gift wrap and gift cards (post-launch). Multi-parcel orders (one parcel per order in S14; a later change).

## Open questions
See the module README: Q-FUL-1 (packaging thresholds), Q-FUL-2 (tolerance), Q-FUL-3 (where garment weights come from).
