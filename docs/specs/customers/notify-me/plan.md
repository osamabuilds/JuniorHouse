# Notify Me: technical plan

- **Spec:** [spec.md](spec.md) · **Module:** Customers · **Module plan:** [../plan.md](../plan.md) · **ADR:** [0022](../../../adr/0022-email-sender-port.md)

## Domain model
`NotifyRequest` (`Id`, `VariantId` (Labelling variant id, as the stock events and the read model carry it), `Email` normalised, `Status` Open/Notified/Expired, `NotifiedAt?`, `SentAt?`). Rules: at most one **open** request per (variant, email); at most `MaxNotifyPerVariant` (5,000) open requests per variant; an open request becomes Notified exactly once. `RestockRule` is a pure function: the event's `Available > 0` and open requests exist.

## Application layer
| Command / Query | Output | Covers |
|---|---|---|
| `CreateNotifyRequestCommand(variantId, email)` | neutral result; checks the variant is real and sold out through `IStorefrontCatalogQueries.GetVariantSummariesAsync([id])` | NTM-1, 2, 3, 6 |
| `ProcessStockChangeCommand` (from the mapped event) | marks open requests for the variant Notified and queues one `CustomerEmailRequestedEvent(Kind=RestockNotice, RelatedId=requestId)` each, in one transaction, in batches of 500 | NTM-4, 8 |
| `SendRestockNoticeHandler` (outbox handler) | loads the request, variant summary (name, size, colour, slug) from the contract, renders and sends; sets `SENT_DTE` | NTM-4, 5, 9 |
| `PurgeNotifyRequestsJob` (hosted, daily) | expires Open requests older than 90 days, deletes Notified/Expired older than 30 days | retention (Q-NTM-2) |
| `ListNotifyDemandQuery` (paged) | per variant: count of Open requests, oldest request date; ordered by count desc then variant id; then one bulk summary call for names | NTM-11, 12, 13 |

## API endpoints
| Method | Route | Auth | Ownership check (SEC-05) |
|---|---|---|---|
| POST | `/api/notify-me` | `Public`, `strict` | n/a: the email is the subject; identical response for new and repeated requests |
| GET | `/api/admin/customers/notify-demand?page&pageSize` | `Policies.CustomerDemand` | counts only, no email column selected |

## Events
| Event | Published by | Consumed by | Via outbox? |
|---|---|---|---|
| `StockLevelChangedEvent` v1 | Warehouse | `StockChangedConsumer` (inbox-guarded, through `StockChangedMapper`) | existing |
| `CustomerEmailRequestedEvent` | this feature | `SendRestockNoticeHandler` | yes |

## Data
`CUST.NTFM_REQ`: `ID`, `VRNT_ID` bigint, `EML` varchar(254), `STS_ID` smallint FK, `NTFD_DTE` timestamptz null, `SENT_DTE` timestamptz null, audit, `xmin`. **Partial unique** `UQ_NTFM_REQ_VRNT_ID_EML_OPEN` on (`VRNT_ID`, lower(`EML`)) where status is Open; indexes on `STS_ID`, (`VRNT_ID`, `STS_ID`) for the consumer and the demand group-by. Lookup `NTFM_STS_LKP`: OPEN, NTFD, EXPD (seeded).

## NFR / security design
- **SLA:** the dispatcher interval plus one send; the integration test measures stock-event to message captured and must stay within the Q-NTM-1 default of 15 minutes (expected seconds).
- **NFR-FT-08:** the email is a separate outbox message, so an SMTP failure retries the message and never touches stock or the request state; unsent mail is visible as Notified with `SENT_DTE` null (this refines NTM-8: the request is closed at queue time and the *email* retries).
- **Dedup (NTM-3):** partial unique index with `ON CONFLICT DO NOTHING`; the Notified state means a later request for the same variant and email is a new open request.
- **Demand list:** one grouped SQL query, then one bulk contract call; no email selected; page size clamped.
- The product-page form posts to the SSR server and works without JavaScript; the confirmation is a live region when enhanced (NTM-10).

## Frontend
- Storefront `features/customers/notify-me`: replaces the sold-out placeholder on the product page with an email form per sold-out variant (prefilled for the stand-in customer).
- Admin `features/customers/notify-me` (existing admin look): table of product, colour, size, SKU, waiting, oldest request, with the shared pagination; route `customers/notify-me`; menu entry. No export, no addresses.

## Risks & alternatives
Email may arrive seconds before the product page shows stock (module plan). Per-variant, not per-size, requests (default Q-NTM-5): the size picker selects a concrete variant, so sold-out detection is per variant. Alternative of reading stock from Warehouse directly was rejected: the shopper saw the storefront's view (Q-NTM-4).

## Security & coupling review
| Area | How this feature meets it |
|---|---|
| Authorization | public create is `Public` + `strict`; demand list `Policies.CustomerDemand`; no per-user resources (SEC-05 n/a: the list is anonymous aggregate counts) |
| Input | email length/format; `variantId` positive; variant must exist and be sold out per the contract |
| Output encoding | email template HTML-encodes product names; links from slugs |
| Outbound calls | SMTP only (ADR 0022) |
| Resource limits | 5,000 open per variant; batches of 500; rate limit; page size 1..100 |
| Concurrency | partial unique index; consumer updates rows with a conditional `UPDATE … WHERE STS_ID = open` |
| Idempotency | inbox on the consumer and the email handler; repeated events notify nobody twice |
| Audit | audit columns; status and times on the row |
| Data protection | addresses stored only in `NTFM_REQ`, deleted by retention; not in events, logs or the admin response (NTM-12) |
| Secrets and config | retention and cap options validated |
| Error responses | neutral on the public endpoint; problem details on admin |
| New dependencies | none beyond ADR 0022 |
| Coupling | Warehouse event via one mapper; `Storefront.Contracts` for display data; no table reads across schemas |
| Abuse cases | (1) Flooding one variant with fake addresses to mail-bomb third parties: one email per address and variant, cap, rate limit, no marketing content. (2) Probing which addresses are registered: identical responses. (3) Staff or a report extracting email lists: admin response has counts only |
