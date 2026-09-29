# Romp: Sprint plan (MVP)

- **Cadence:** 1-week sprints, Monday to Friday. **Team:** one developer working with Claude.
- **Order:** the BRD business flow, start to end: plan & buy (§5.9) → produce (§5.10) → receive & inspect (§5.11) → label (§5.12) → shelve (§5.13–5.14) → publish & sell (§5.1–5.5) → pick, pack, ship (§5.15–5.19) → returns (§5.6, §5.20).
- **Every sprint delivers a working module:** database (following `docs/db/naming.md`) → API → admin/storefront UI → automated tests. It runs locally with `docker compose` and can be demoed on Friday.
- **Descoped:** the Fit Finder and Virtual Try-On. **No login until S8**, and nothing is deployed to a server before S8.
- **Total:** 19 sprints, roughly 4.5 months. S19 is a buffer.

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

## After launch (backlog, not scheduled)
Loyalty, referral, abandoned cart, birthday rewards (FR-49..52). BNPL, gift cards, gift wrap. Reviews, live chat, shareable registry. POS PWA (§9.12).

## Risks
- **S1 is the heaviest sprint.** It sets up EF Core, the naming convention, lookup tables and the first full module at the same time. If it overruns, the PO status workflow moves into S2 and S1 still ends with vendors and styles fully working.
- **External approvals** (GS1, payment gateway, WhatsApp, couriers) are the likeliest source of delay. Each integration is built behind an interface with a fake implementation first.
- **FR-39 vs SEC-02** (login lockout policy) must be decided before S10.
