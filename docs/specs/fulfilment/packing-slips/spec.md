# Packing slips

- **Status:** Draft, applied by delegation (owner away, 2026-11-09)
- **Jira:** to be created
- **BRD sections:** §5.16, §5.17, §5.6
- **Requirement IDs:** §5.16, §5.17, NFR-16

## Problem & intent
Every parcel carries a slip that tells the customer what is inside and how to return it, and tells the courier rider how much cash to collect for a cash on delivery order.

## Requirements (quoted from the BRD)
> **§5.16:** Branded packaging: Romp-branded poly bag/box, a thank-you card/insert, and — per v8.0 Section 5.6 — a pre-printed return/exchange instruction slip included in every parcel (reduces 'how do I return this' support load).

> **§5.16:** COD orders: cash-collection amount is printed clearly on the shipping label / invoice slip inside, since courier riders collect against what's printed, not what's in a backend system they can't see.

> **§5.17:** Invoice / Packing Slip: Tells the customer what's inside and how to return it. Printed by the Romp system, at pack station (goes inside the parcel). Key contents: Order number, itemised SKUs/sizes/quantities/prices, return window & instructions, support contact.

## Acceptance criteria

| ID | Given / When / Then | Source |
|---|---|---|
| SLP-1 | Given a packed order, when the slip is made, then it lists the order number, each line with SKU, size, colour, quantity and price, and the totals | §5.17 |
| SLP-2 | Given a cash on delivery order, when the slip is made, then the amount to collect is printed large and clearly | §5.16 |
| SLP-3 | Given an online order that is paid, when the slip is made, then it says "Paid" and shows no amount to collect | §5.16 (new) |
| SLP-4 | Given the shop settings, when the slip is made, then it shows the exchange window and the support contact from those settings, not from text typed into a template | §5.17 |
| SLP-5 | Given the slip is requested again, when printed, then it is the same content, with a "reprint" mark, and the order is unchanged (new) | §5.16 |
| SLP-6 | Given printing goes through the printer port and queue, when the printer is down, then the pack is not blocked and the slip prints on retry | NFR-SC-02 |
| SLP-7 | Given a slip, then it shows the recipient name and the city, but not the full phone number or the email (new) | SEC-25 |
| SLP-8 | Given the slip is read on screen, then it is a semantic page with one heading and a print stylesheet (new) | NFR-19 |

## Out of scope
The courier shipping label and its barcode (S15). Tax invoice rules beyond what NFR-16 requires, to be checked with the business before launch.

## Open questions
- [ ] **Q-SLP-1** Does the business need a tax invoice number on the slip (FBR rules, NFR-16)? Recommendation: not in S14; ask the business and add it as a setting later.
