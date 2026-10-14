# Stock alerts: low-stock and stock-out

- **Status:** Approved (2026-10-14; plan next)
- **Jira:** SCRUM-79 (FR-54, epic SCRUM-12 Admin back office)
- **BRD sections:** §4 (FEAT-ADM-02), §5.14, §7.8
- **Requirement IDs:** FR-54, FEAT-ADM-02, NFR-08, NFR-FT-08, NFR-SC-05, SEC-08, NFR-17, NFR-19; test cases TC-STK-02, 03, 08 (Jira comment on SCRUM-79)
- **Module context:** [README](../README.md)

## Problem & intent

The business must notice before a popular size runs out. Admin users need a clear list of variants that are low or out of stock, a threshold they can tune per variant, and a way to mark an alert as seen.

## Requirements (quoted from the BRD)

> **FR-54:** Admin users shall manage stock per variant with low-stock/stock-out alerting.

> **FEAT-ADM-02 (MVP), Stock management + low-stock alerts:** Per-variant stock levels and thresholds.

## User flow

1. Admin users set a **low-stock threshold** per variant (a whole number ≥ 0). A global default applies until a variant has its own. (new)
2. When a variant's available quantity falls to or below its threshold, a **Low stock** alert opens; when it reaches 0, it becomes **Stock-out**.
3. Staff open **Warehouse → Alerts** to see open alerts (variant, available, threshold, since when), and can acknowledge them. An alert closes by itself when stock rises above the threshold.
4. The navigation shows a count of open alerts. Each new alert also publishes `StockAlertRaisedEvent` through the outbox for later notification wiring (Q6).

## Acceptance criteria

| ID | Given / When / Then | Source |
|---|---|---|
| ALR-1 | Given a threshold of N for a variant, when its available quantity falls to ≤ N (and > 0) after a stock change, then exactly one open Low-stock alert exists for it (fires once per crossing, no spam). | TC-STK-02 (SCRUM-79 AC-2) |
| ALR-2 | Given available quantity reaches 0, when the change commits, then the alert becomes Stock-out (not a second alert) and the published stock event carries available = 0 so the storefront can show the size as unavailable (the storefront side is Sprint 9). | TC-STK-03 (SCRUM-79 AC-3) |
| ALR-3 | Given further movements while the alert is open, when they commit, then no duplicate alert is created; given stock rising above the threshold (restock), the alert closes automatically. | TC-STK-08 |
| ALR-4 | Given a variant that has never been put away, when it has zero stock, then no alert exists for it. | (new) |
| ALR-5 | Given an open alert, when staff acknowledge it, then it records who and when, stays open until stock recovers, and drops out of the "unacknowledged" count. | (new) |
| ALR-6 | Given a threshold change, when it is saved, then it is validated (integer, 0..100000), attributable and timestamped, and alerts are re-evaluated. | NFR-SC-05 (new) |
| ALR-7 | Given an alert opens or changes state, when it commits, then `StockAlertRaisedEvent` is in the outbox in the same transaction and a delivery failure never rolls back the stock change. | NFR-FT-08 |
| ALR-8 | Given the alerts list, when it loads, then it is paged (1..100), ordered by a unique key, N+1-free, filterable by state (Low / Stock-out / Acknowledged), semantic and keyboard-operable. | NFR-17, NFR-19, CONVENTIONS.md |
| ALR-9 | Given a stock change, when evaluating its alert fails, then the stock change is not rolled back, and the evaluation is retried and the failure logged. | NFR-FT-08 (new) |

## Non-functional constraints

NFR-FT-08, NFR-SC-05, NFR-17, NFR-19. Alerts are evaluated from ledger changes inside the module; no other module's tables are read.

## Out of scope

Hiding add-to-cart for a stock-out size (storefront, Sprint 9), notify-me flows (Phase 2), email/WhatsApp/push delivery (Q6), reorder suggestions or automatic purchase orders, sales-velocity-based thresholds, reports (FR-57-style).

## Open questions

- [x] **Q6 (module): alert channel.** SCRUM-79 wants a dashboard badge plus email/WhatsApp to ops, but recipients and channels are listed there as open and the codebase has no email or WhatsApp sender yet. Default: dashboard badge, in-app list and outbox event now; the email/WhatsApp send is added when a notifications feature exists. Who decides: owner (recipients and channels).
- [x] **Q-ALR-1: Default threshold and category overrides.** SCRUM-79 asks for a threshold "per variant/category" and lists the default and the overrides as open. Default: a configuration value of 5 units, per-variant overrides only. Category overrides need a Catalog contract (variant to category), so they wait for Sprint 7. Who decides: owner.
- [x] **Q-ALR-3: Role.** Setting thresholds and acknowledging need the inventory-capable role (FR-60). Default: the endpoints use the inventory policy (declared now, enforced in S8, like every other policy).
- [ ] **Q-ALR-2: Alert granularity.** Default: per variant (all bins and grades together), matching FR-54's "per variant".
