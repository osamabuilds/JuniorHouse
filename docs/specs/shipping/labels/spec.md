# Labels

- **Status:** Draft, applied by delegation (owner away, 2026-11-09)
- **Jira:** to be created
- **BRD sections:** §5.17
- **Requirement IDs:** FR-SC-13

## Problem & intent
The rider and every handler need one label with where the parcel goes, how much cash to collect, and the courier's own barcode. It can only be printed after the booking, because Romp cannot invent a waybill.

## Requirements (quoted from the BRD)
> **§5.17:** Shipping Label: Tells the courier and any handler where the parcel goes. Printed by the Romp system, at pack station. Key contents: Recipient name, full address, phone, COD amount (if applicable), courier's own tracking barcode/waybill number, fragile/handling icons if relevant.

> **§5.17:** ... label printing must happen after (or as part of) the booking call, not before.

> **§5.16:** COD orders: cash-collection amount is printed clearly on the shipping label.

## Acceptance criteria

| ID | Given / When / Then | Source |
|---|---|---|
| LBL-1 | Given a booked shipment, when the label is opened, then it shows the recipient name, full address, phone, city, the order number and the waybill with its courier | §5.17 |
| LBL-2 | Given a shipment that is not booked yet, when the label is requested, then it is refused with the reason (no label before a waybill) | §5.17 |
| LBL-3 | Given the waybill, then it is shown as a Code 128 barcode that a scanner can read, and as text | §5.17 |
| LBL-4 | Given a cash on delivery order, then the amount to collect is printed large; a paid order says "Paid" and no amount (new) | §5.16 |
| LBL-5 | Given the label is requested again, then it is the same content with a "reprint" mark and the shipment is unchanged (new) | §5.17 |
| LBL-6 | Given the label page, then it is a semantic page with one heading and a print stylesheet that leaves out the screen furniture (new) | NFR-19 |
| LBL-7 | Given the label holds the customer's phone and address, then only staff with the permission can open it, and nothing about it is logged (new) | SEC-08, SEC-25 |

## Out of scope
The courier's own label PDF (a later change), thermal-printer formats.

## Open questions
See Q-SHP-3.
