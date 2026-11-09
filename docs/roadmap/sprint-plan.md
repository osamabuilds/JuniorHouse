# Romp: Sprint plan (MVP)

- **Cadence:** 1-week sprints, Monday to Friday. **Team:** one developer working with Claude.
- **Order:** the BRD business flow, start to end: plan & buy (§5.9) → produce (§5.10) → receive & inspect (§5.11) → label (§5.12) → shelve (§5.13–5.14) → publish & sell (§5.1–5.5) → pick, pack, ship (§5.15–5.19) → returns (§5.6, §5.20).
- **Every sprint delivers a working module:** database (following `docs/db/naming.md`) → API → admin/storefront UI → automated tests. It runs locally with `docker compose` and can be demoed on Friday.
- **Descoped:** the Fit Finder and Virtual Try-On. **No login until S8**, and nothing is deployed to a server before S8.
- **Total:** 19 sprints plus S12b (close-out of S12). S19 is a buffer. See "Remaining sprints" below for the plan from 2026-11-09.

Weekly rhythm (spec-driven, see `CLAUDE.md`): **Monday** spec + plan, approved by you → **Tuesday to Thursday** test-first build → **Friday** demo, spec updated to match what was built, merge.

## Start these now (external lead times)

| Item | Needed by |
|---|---|
| GS1 Pakistan company prefix (barcodes) | S5 (a test prefix is used until it arrives) |
| Thermal label printer + barcode scanner | S5 / S14 |
| VPS provider, Cloudflare, domain | S8 |
| Card payment gateway merchant account | S12 |
| WhatsApp Business API approval | S13 |
| Primary + secondary courier API accounts | S15 |

## Sprints

| # | Business step | What works at the end of the sprint | Requirements |
|---|---|---|---|
| **S1** | **Plan & buy (part 1)** | Staff define reference data (sizes, colours, fabrics, categories, …) and styles, create vendors, and raise a purchase order with a size × colour breakdown, moving it from Draft to Sent to Acknowledged (or Cancelled). Runs end to end in the admin app. | FR-SC-01 (vendor record), FR-SC-02 (PO), §5.9. Foundation: EF Core with the naming convention, MediatR pipeline, outbox writer. |
| **S2** | Plan & buy (part 2) | POs can be amended as new versions (the original terms are kept), tech packs and attachments can be uploaded, and cost sheets recorded. The outbox dispatcher runs. | FR-SC-03, FR-SC-02 (tech pack), §5.9 edge cases |
| **S3** | Produce | Production milestones against a PO, the PP-sample approval gate before bulk production, expected vs actual dates, and vendor delivery notes, including partial deliveries. (GRN for each delivery is built in S4, so SCRUM-94 is split.) Jira: SCRUM-183..196 under epic SCRUM-74. | §5.10, FR-SC-04 |
| **S4** | Receive & inspect | A GRN is recorded against the PO and delivery note, with counts reconciled. QC by batch (AQL sampling, child-safety checks) with outcomes Accept / Concession / Reject / Rework, and partial-batch rejection. The vendor scorecard is updated. | §5.11, FR-SC-04, FR-SC-05, FR-SC-01 (scorecard) |
| **S5** | Label | Accepted stock gets SKUs and GTIN-13 barcodes, and price/care tags are printed through a queue with retry. Tags can be reprinted without a new barcode. | §5.12, FR-SC-06, FR-SC-07, NFR-SC-02, NFR-SC-03 |
| **S6** | Shelve | Bins are defined, putaway is recorded by scan, and stock becomes available only after putaway. A stock ledger, adjustments with reason codes, cycle counts and low-stock alerts. | §5.13–5.14, FR-SC-08, FR-54, NFR-SC-05, NFR-SC-06 |
| **S7** | Publish | A style becomes a sellable product: storefront content, images (required alt text), size chart, composition and care, SEO slugs, category and collection placement. | FR-53, FR-08, FR-09, SEO-02, SEO-07 |
| **S8** | Secure & deploy | Staff login, roles, admin MFA and an audit trail on every admin action. Observability. **Staging VPS with a GitHub Actions deploy pipeline.** | FR-59, FR-60, SEC-01, SEC-03, SEC-04, SEC-08, SEC-22, SEC-25 |
| **S9** | Sell: browse | Server-rendered storefront: navigation, listings, filter/sort, search, SKU lookup, product page, empty states. Full SEO baseline and Lighthouse budgets in CI. | FR-01..07, SEO-01, 03, 04, 05, 06, 08, 10, NFR-01, NFR-02 |
| **S10** | Sell: customers | Registration, login, password reset, login throttling, addresses, newsletter, wishlist, basic Child Profiles, notify-me. | FR-11, FR-17, FR-18, FR-21, FR-38..41, SEC-02, SEC-05, NFR-27, NFR-29 |
| **S11** | Sell: cart | Cart, cart merge on login, live stock checks, discount codes (customer side + admin), server-side totals and shipping. | FR-10, FR-22, FR-24..26, FR-34, FR-35, FR-57, SEC-09, SEC-14 |
| **S12** | Sell: checkout | Guest and registered checkout, COD + card, stock reservation, idempotent orders, verified webhooks, payment reconciliation, COD confirmation with OTP and limits, cancellation, circuit breakers. | FR-23, FR-27..30, FR-32, FR-33, FR-SC-09, SEC-10..13, SEC-21 |
| **S13** | Confirm | Order confirmation by email and WhatsApp, with guaranteed delivery (retry, DLQ, fallback channel, resend from admin). Admin order queue. | FR-31, FR-55, FR-61..65, NFR-FT-01..08 |
| **S14** | Pick & pack | FIFO allocation, pick lists, scan-to-confirm, pick exceptions, pack station with weighing, packaging rule and packing slip. Warehouse PWA. | §5.15–5.16, FR-SC-10..12, NFR-SC-01, NFR-SC-07 |
| **S15** | Ship & track | Courier booking with waybill and label, secondary-courier fallback, end-of-day manifest, tracking webhooks, customer order history and guest tracking. | §5.17–5.19, FR-SC-13..16, FR-36, FR-37, SEC-06, SEC-07 |
| **S16** | Settle & return requests | COD reconciliation against courier remittance. Return/exchange requests with windows, the sale-item rule, defect claims, agent override and an ops queue. | FR-SC-17, FR-42..46 |
| **S17** | Reverse logistics | Reverse QC, RTO restock, refunds only after QC, linked exchange orders, contact form, WhatsApp support channel. | §5.20, FR-SC-18..20, FR-47, FR-48 |
| **S18** | Measure & harden | Reports, funnel analytics, Merchant Center feed, a load test to 500 users / 3× sale traffic, a WCAG audit, security alerts. | FR-58, NFR-04, NFR-19, NFR-35, SEO-09, SEC-26 |
| **S19** | Launch readiness (buffer) | Production with DB HA and Cloudflare DDoS protection, a backup restore drill, the incident runbook, Search Console, the compliance check, UAT, spill-over. | SEC-19, SEC-20, SEC-27, NFR-07, NFR-16, SEO-11 |

**Status:** S1 to S7 are implemented. S8 (Secure & deploy) is implemented and verified locally except the staging deployment, which waits for the domain, the VPS and the accounts: staff sign-in, MFA, roles and the audit trail are built (docs/specs/identity/, README "Try Sprint 8"); S9 (storefront browse) ran first, split into S9a (read model, navigation, listings) and S9b (product pages, search, SEO). S7 (Publish) shipped with the products, product-media, size-charts, composition-care and urls features under `docs/specs/catalog/`; the storefront that shows published products starts in S9.

## Remaining sprints (re-planned 2026-11-09)

**Real status.** Done and live on docker: S1–S11, S13 (notifications: all 28 tasks), order queue, COD confirmation, guest order look-up, wishlist, newsletter, notify-me, recommendations. **S12 (checkout) is only half done**: placement, COD and reservations work, but online payments are not built. Child Profiles (FR-18/21) are parked. S8's staging deploy still waits for the domain, VPS and accounts. Dates below are indicative (1 week each, Monday starts, no allowance for public holidays).

| Sprint | Week of | Module / features (docs/specs/…) | Delivers |
|---|---|---|---|
| **S12b Close S12 + clean-up** (built 2026-11-09, see [close-out](../specs/orders/close-out.md); the clean-up chores are still open) | 11-09 | `orders/payments` (Y1–Y9), P8, H3, H5, Z1/Z2; `cart` D6 | Fake payment gateway behind `IPaymentGateway`, signed webhooks, reconciliation, circuit breaker, unpaid-order expiry, own-order reads, Terms/Privacy placeholders. **Also:** push and merge `SCRUM-148-seo` (read the CI Lighthouse job first), commit and PR `SCRUM-order-lookup`, create the missing Jira issues, fix the 8 failing e2e tests (the 1024 px overflow is a real bug). |
| **S14 Pick & pack** | 11-16 | new module `fulfilment`: `allocations`, `pick-lists`, `pick-exceptions`, `pack-station`, `packing-slips`, `warehouse-app` | Confirmed order → FIFO allocation of bin-located stock (reservations from `warehouse` become allocations) → pick list → scan bin + GTIN → pick exception falls back to "delayed" and notifies the customer → pack re-scan, packaging rule, weigh and flag deviation, packing slip with COD amount and return slip. Warehouse PWA for pick and pack. |
| **S15 Ship & track** | 11-23 | new module `shipping`: `courier-port`, `bookings`, `labels`, `manual-booking-queue`, `manifests`, `tracking`; `orders/order-history` | `ICourierGateway` port with a fake first, then the real primary and secondary couriers. Book → waybill → combined shipping label, automatic fallback to the secondary courier, manual-booking queue with retry, end-of-day manifest reconciled with the parcel count, tracking webhooks (polling fallback) feeding the customer timeline, My Orders history, order look-up by tracking number (OLK-8), failed-attempt warning to the customer. |
| **S16 Settle & return requests** | 11-30 | `shipping/cod-reconciliation`; new module `returns`: `return-requests`, `return-policy`, `defect-claims`, `agent-override`, `returns-queue` | COD delivered vs courier remittance, mismatches flagged. Returns from My Orders: configurable window from delivery date, sale items size-exchange only, defect claims with photos on their own window, audited agent override, ops queue with approve/reject and a resolution log. |
| **S17 Reverse logistics** | 12-07 | `returns/reverse-qc`, `returns/rto`, `returns/refunds`, `returns/exchange-orders`; `support/contact-form`, `support/whatsapp-inbox` | Pickup booking (reuses S15), receive against the RMA, outcome Restock / Restock as seconds / Reject, refund or credit only after reverse-QC, intact-seal RTO restocks immediately, linked exchange order (`ParentOrderId`) without a new checkout, contact form, WhatsApp support log tied to customer and order. |
| **S18 Measure & harden** | 12-14 | new module `reporting` (read models): `sales-reports`, `funnel`; `storefront/merchant-feed`; `platform/security-alerts`, `platform/load-test` | Sales, stock-out and return-rate reports by date and category (FR-58), funnel analytics view → cart → checkout → purchase (NFR-35, without the descoped Fit Finder), Merchant Center feed (SEO-09), load test at 500 users / 3× traffic (NFR-04), WCAG audit fixes, security-event alerts (SEC-26). |
| **S19 Launch readiness (buffer)** | 12-21 | `platform/production`, `platform/runbook` | Production with Postgres HA, Cloudflare DDoS tier, backup restore drill (RPO 24 h / RTO 4 h), incident runbook (SEC-27), Search Console and Bing connected (SEO-11), consumer-law check (NFR-16), UAT, spill-over. |

**How each sprint runs.** Same weekly rhythm as above. The first day is the spec, plan and tasks for the sprint's features, stopping for your approval at each step. Each external integration (courier, payment gateway, WhatsApp) is built behind a port with a fake first, so a late approval never blocks the sprint. Each sprint ends with a docker rebuild so you can try it.

**Dependencies between sprints**
- S14 needs the reservation and putaway data from `warehouse` and the `OrderConfirmed` event from `orders`.
- S15 needs S14's "packed" state, and also unlocks S16 (delivery date starts the return window) and the order look-up by tracking number.
- S17 reuses S15 courier booking for return pickups and `payments` for refunds (the gateway port needs a refund call; add it in S12b's port).
- S18 reads from every earlier module, so it only works from their events or contracts, never their tables.
- S19 and the S8 staging deploy both wait on the domain, VPS and Cloudflare. If those are not ready by S15, staging slips and S19 grows.

**Each new module needs:** an entry in `ModuleNames` (architecture tests), its own schema and abbreviations in `docs/db/naming.md`, a row in the authorization matrix for every endpoint, and an ADR where there is a new architectural choice (new ones expected: `fulfilment` allocation and scan flow, courier port and fallback, returns state machine and refunds, reporting read models).

**Decisions needed from the owner (blocking the sprint named)**
1. **S14:** packaging rule thresholds (grams for poly bag vs box), expected-weight tolerance, and scanner type (keyboard-wedge USB scanner vs phone camera in the PWA).
2. **S15:** which primary and secondary courier, and whether their API accounts exist. One real courier is enough for launch; the second can stay a fake until its account arrives.
3. **S16:** return window length, defect-claim window, who pays return shipping for non-defect returns, and the COD remittance cycle per courier.
4. **S17:** how a COD order is refunded (bank transfer, store credit, JazzCash/Easypaisa?), and whether graded "seconds" stock is sold at a fixed discount.
5. **S18:** which payment gateway for card payments (the real one replaces the fake in S12b/S19), and whether Google Merchant Center is in the MVP.
6. **Child Profiles (FR-18/21):** build them in a sprint (about 3 days) or keep them parked until after launch?
7. **Slack:** S19 is the only buffer. If the external accounts arrive late, the cut line is S18 (reports and the feed move after launch; security alerts and the load test stay).

## After launch (backlog, not scheduled)
Loyalty, referral, abandoned cart, birthday rewards (FR-49..52). BNPL, gift cards, gift wrap. Reviews, live chat, shareable registry. POS PWA (§9.12).

## Risks
- **S1 is the heaviest sprint.** It sets up EF Core, the naming convention, lookup tables and the first full module at the same time. If it overruns, the PO status workflow moves into S2 and S1 still ends with vendors and styles fully working.
- **External approvals** (GS1, payment gateway, WhatsApp, couriers) are the likeliest source of delay. Each integration is built behind an interface with a fake implementation first.
- **FR-39 vs SEC-02** (login lockout policy) must be decided before S10.
