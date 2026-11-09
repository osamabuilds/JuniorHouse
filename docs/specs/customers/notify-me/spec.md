# Notify Me (back in stock)

- **Status:** Built (2026-11-04), see "As built"
- **Jira:** SCRUM-31 (FR-11) under epic SCRUM-6
- **BRD sections:** §5.3, §6.2 (TC-PDP-04..06), FEAT-PDP
- **Requirement IDs:** FR-11, NFR-26, NFR-FT-08 (a failed email never rolls back stock changes)

## Problem & intent
When a size is sold out the shopper should not hit a dead end. They leave an email address and are told once when that variant is available again. No customer account is needed.

## Requirements (quoted from the BRD)
> **FR-11:** Out-of-stock variants shall offer a deduplicated 'Notify Me' capture, triggering a restock email within a defined SLA once stock is replenished.

> **TC-PDP-04:** Select sold-out size → Add-to-Cart replaced with Notify Me. **TC-PDP-05:** Submit Notify Me twice with the same email for the same variant → system deduplicates; does not send two restock emails. **TC-PDP-06:** Admin restocks a notified variant → email sent within the defined SLA (e.g. same business day).

## User flow
1. On a product page, a sold-out size shows **Notify me** instead of an add-to-bag control (the product page already shows the sold-out state; the form was deferred from S9).
2. The shopper enters an email (prefilled for signed-in customers) and submits; the screen confirms.
3. When the variant becomes available (putaway or adjustment in the warehouse), one email goes to each distinct subscriber with a link to the product, then the request is closed.

## Acceptance criteria

| ID | Given / When / Then | Source |
|---|---|---|
| NTM-1 | Given a sold-out variant on the product page, then a Notify me form replaces the add control; an in-stock variant never shows it. | TC-PDP-04 |
| NTM-2 | Given a valid email for a sold-out variant, when submitted, then one open request exists for that email and variant. | FR-11 |
| NTM-3 | Given the same email submitted again for the same variant, then no second request exists and the shopper sees the same confirmation. | TC-PDP-05 |
| NTM-4 | Given a variant that returns to stock, then each open request produces exactly one restock email and is closed, within the SLA. | TC-PDP-06, FR-11 |
| NTM-5 | Given the restock email, then it names the product and size, links to its page, carries no marketing, and is sent whether or not the person subscribed to the newsletter. | FR-41 (new) |
| NTM-6 | Given an invalid email or a variant that is not sold out or does not exist, then the form says what to fix and nothing is recorded. | (new) |
| NTM-7 | Given the public endpoint, then it is rate limited and answers identically for new and repeated addresses. | NFR-15 (new) |
| NTM-8 | Given an email send that fails, then the stock movement that triggered it is unaffected and the request stays open for retry (full retry, DLQ and resend arrive in S13). | NFR-FT-08 |
| NTM-9 | Given a variant that sells out again before the email goes out, then the email is still sent once, with its availability stated as of sending `(Q-NTM-3)`. | (new) |
| NTM-10 | Given the form, then it is a labelled form with an accessible confirmation (live region) and works without JavaScript through a normal POST. | NFR-19, SEO-01 |

| NTM-11 | Given staff in the admin app, when they open **Notify Me demand**, then they see a read-only, paged list of variants with open requests: product, colour, size, SKU, number waiting, oldest request date; sorted by number waiting, highest first. | Q-MOD-1 (owner, 2026-10-25) |
| NTM-12 | Given the demand list, then it shows counts only: no email address or other personal data appears on screen, in the API response or in logs. | NFR-26, SEC-25 |
| NTM-13 | Given the demand endpoint, then it reads counts from this module's own table in one grouped query (no per-row lookups) and is guarded by the staff policy used by the other admin endpoints (real roles arrive in S8). | CONVENTIONS, SEC-08 |

## Admin screen (owner decision, 2026-10-25)
One read-only screen, `features/customers/notify-me` in the admin app, in the current admin look. No export, no editing, no email addresses.

## Non-functional constraints
Stock events come from the Warehouse stock events the Storefront already consumes; this feature subscribes through the integration event, never by reading warehouse tables. The email is written through the outbox (ADR 0004). Addresses are personal data (NFR-26) and are deleted when the request is closed or after a retention period (Q-NTM-2).

## Out of scope
WhatsApp or SMS alerts; "price drop" alerts; guaranteed delivery and resend from admin (S13); waitlists that reserve stock.

## Open questions
- [x] **Q-NTM-1 SLA (owner, 2026-11-04: defaults accepted).** "A defined SLA" is not defined. Default: within 15 minutes of the stock event. The BRD example says "same business day"; which do you want?
- [x] **Q-NTM-2 Retention (owner, 2026-11-04: defaults accepted).** Default: an open request expires after 90 days; a closed one is deleted after 30 days.
- [x] **Q-NTM-3 Availability at send time (owner, 2026-11-04: send anyway).** If the variant sells out again before sending, send anyway (default) or skip and keep the request open?
- [x] **Q-NTM-4 Which module owns the sold-out check (owner, 2026-11-04: default accepted).** Default: the Notify Me endpoint checks the variant against the Storefront read model through its contract, because that is what the shopper saw.
- [x] **Q-NTM-5 Variant level (owner, 2026-11-04: per variant).** Request per variant (size and colour), as FR-11 says. The sold-out size picker may be per size across colours; confirm per variant.

## As built (2026-11-04)

What was built matches the criteria above, with these differences from the plan and the draft:

- **Sold-out sizes can now be chosen on the product page.** The product page spec (PDP-4, PDP-16) made a sold-out size plain struck-through text that cannot be chosen, which left no way to reach the form (TC-PDP-04 says "select sold-out size"). The owner chose to make a sold-out size a link, still worded "Sold out". `SizeOptionDto.Selectable` now means "can be chosen": true for every size except "not yet available". Only `InStock` and `LowStock` can be bought, so Add to bag stays disabled for a sold-out size and the page shows the Notify me form for it (NTM-1). [Product pages PDP-4](../../storefront/product-pages/spec.md) is amended to match.
- **Data.** `CUST.NTFM_REQ` with `VRNT_ID` (another module's id, so no foreign key), `EML` (stored lower case, a check constraint keeps it so), `STS_ID` to `NTFM_STS_LKP` (OPEN, NTFD, EXPD), `NTFD_DTE`, `SENT_DTE` and the audit columns. A partial unique index `UQ_NTFM_REQ_VRNT_ID_EML_OPEN` allows one open request per variant and address; a closed request does not block a new one. The entity is `NotAudited` (the address is not copied into the audit trail).
- **Capture (NTM-2, 3, 6, 7).** `CreateNotifyRequestCommand` checks the variant through `IStorefrontCatalogQueries.GetVariantSummariesAsync` (new, one bulk call, up to 200 ids; an unknown, unsellable or unpublished variant is absent), refuses a variant that is in stock, takes an advisory lock per variant and address (it replaces the plan's `INSERT ... ON CONFLICT`, like the newsletter), and answers the same for a new and a repeated address. A variant at its cap (`MaxNotifyPerVariant`, 5,000) is refused with a message. The refusals are `ValidationException`s, so the API answers 400 with the reason.
- **Restock (NTM-4, 8).** `StockChangedConsumer` handles Warehouse's `StockLevelChangedEvent` behind the inbox through `StockChangedMapper`. `ProcessStockChangeCommand` closes the open requests in batches of 500 inside one transaction and queues one `CustomerEmailRequestedEvent(RestockNotice, requestId)` each. A row-version conflict with the purge job retries the whole message. `CustomerEmailConsumer` moved to `Infrastructure/Abstractions` because two features now use it.
- **Email (NTM-5, 9).** `SendRestockNoticeCommand` reads the request and the variant at send time, sends once (a request with `SENT_DTE` set, or not in Notified, gets nothing), and sends even if the variant sold out again (Q-NTM-3). A product that is no longer published sends nothing. A send failure throws; the request stays Notified with no `SENT_DTE` and the dispatcher retries only the message. **Unsubscribe link (owner decision 2026-11-05, replaces the earlier "none"):** one email closes the request, so there is nothing to unsubscribe from there, and the email still says it is the only one about the request. But when the address is currently subscribed to the newsletter, the email adds a footer link to the existing newsletter unsubscribe page (signed token as in NLS-3, never the address). Non-subscribers get no link. No `List-Unsubscribe` headers: the email is not marketing.
- **Retention (Q-NTM-2).** `PurgeNotifyRequestsWorker` runs `PurgeNotifyRequestsCommand` daily (first run five minutes after start): open requests older than 90 days expire, closed ones are deleted 30 days after they closed. Both are set-based statements. An expired request waits the closed period before deletion.
- **Endpoints.** `POST /api/notify-me` (Public, `strict` rate limit, 2 kB body, `Origin` check, `no-store`). `GET /api/admin/customers/notify-demand?page&pageSize` is guarded by the existing **`inventory.read`** permission: the plan's `Policies.CustomerDemand` does not exist, and a new permission would have meant a role-seed change nobody asked for. Both are in the authorization matrix. The list is one grouped query plus a count, then one bulk Storefront call for names; page size 1..100; a variant that is no longer published still shows its count with "(no longer published)".
- **Storefront.** `features/customers/notify-me`: store per size, API service, `rp-notify-me-form` (labelled, polite live region, `aria-invalid`), and a `/notify-me/requested` result page (`noindex`, `no-store`). Without JavaScript the form posts to `POST /notify-me` on the SSR server, which calls the API and redirects (303). **Not prefilled for a signed-in customer:** the session carries no email address, and adding one was not in scope. The form uses component-level token styles like the sold-out state, because the design system has no Notify Me component.
- **Admin.** `features/customers/notify-me` at `customers/notify-me`, a read-only paged table (product, colour, size, SKU, waiting, oldest request), with a menu entry for `inventory.read`. No address, export or editing.
- **Configuration.** `Customers:MaxNotifyPerVariant` (1..100,000, default 5,000), `NotifyOpenDays` (1..365, default 90), `NotifyClosedDays` (1..365, default 30), validated at start.
- **Tests.** Domain, query, command, email, outage, purge and migration tests run on a real PostgreSQL; `NotifyMeEndpointsTests` and the authorization matrix run through the host; the architecture tests keep Notify Me independent of the other Customers features and check the demand query never reads the address; e2e `notify-me.e2e.spec.ts` covers the journey (including the email in Mailpit), the in-stock case and the no-JavaScript post, with axe.
