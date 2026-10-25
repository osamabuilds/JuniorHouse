# Newsletter consent: technical plan

- **Spec:** [spec.md](spec.md) · **Module:** Customers · **Module plan:** [../plan.md](../plan.md) · **ADR:** [0022](../../../adr/0022-email-sender-port.md)

## Domain model
`NewsletterSubscription` (`Id`, `Email` normalised, `Status` Subscribed/Unsubscribed, `CustomerId?`, `ChangedAt`) with an append-only `ConsentEvent` history (`Action` Subscribed/Unsubscribed, `Source` Registration/Account/Footer/Link, time). Rules: one subscription per normalised email; **an unsubscribe is never reversed except by an explicit subscribe action** (NLS-4): the Registration source only subscribes when the checkbox is ticked, and never touches an already-unsubscribed record otherwise. `UnsubscribeToken` is `base64url(subscriptionId) + "." + base64url(HMAC-SHA256(key, subscriptionId))` using `Customers:LinkSigningKey`; it carries no email and cannot be forged or used for another record (NLS-3).

## Application layer
| Command / Query | Output | Covers |
|---|---|---|
| `SubscribeNewsletterCommand(email, source)` | always the same neutral result; queues `NewsletterWelcome` mail on a new or re-subscribed record | NLS-1, 6 |
| `UnsubscribeByTokenCommand(token)` | neutral result; idempotent | NLS-3 |
| `GetMyNewsletterQuery`, `SetMyNewsletterCommand(on/off)` | the signed-in customer's preference, matched by their email | NLS-2 |
| `INewsletterAudience` (`Customers.Contracts`) | `ListSubscribedAsync(afterId, take ≤ 500)`: emails of subscribed records only | NLS-4, 7 |
| `CustomerEmailRequestedEvent(Kind=NewsletterWelcome, RelatedId=subscriptionId)` | outbox | ADR 0022 |

## API endpoints
| Method | Route | Auth | Ownership check (SEC-05) |
|---|---|---|---|
| POST | `/api/newsletter/subscribe` | `Public`, `strict` | n/a: the email is the subject; response never reveals prior state |
| GET, POST | `/api/newsletter/unsubscribe?token=` | `Public`, `strict` | the signed token names the record; GET performs it (idempotent) and POST supports `List-Unsubscribe-Post` |
| GET, PUT | `/api/customers/me/newsletter` | `Customer` | customer from `ICurrentCustomer`; record found by their own email |

## Events
`CustomerEmailRequestedEvent` (module plan). Consent changes are not published (no consumer; the audience contract is the read path).

## Data
`CUST`: `NWSL_SUBS` (`ID`, `EML` varchar(254) unique on lower-case, `CUST_ID` bigint null, `STS_ID` smallint FK, audit, `xmin`), `NWSL_HIST` (`ID`, `SUBS_ID` FK, `ACTN_ID`, `SRC_ID`, `INSR_DTE`; append-only, no updates), lookups `NWSL_STS_LKP` (SUBD, UNSB), `NWSL_SRC_LKP` (REGN, ACCT, FOOT, LINK), `NWSL_ACTN_LKP` (SUBD, UNSB). Seeded by migration. Indexes on every FK. `SUBS_ID` in the history has a declared FK.

## NFR / security design
Public endpoints answer identically for known/unknown/already-subscribed emails and are rate limited (NLS-6). Welcome email carries `List-Unsubscribe` and the signed link; HTML encodes all values. A short **email format check** (RFC-lite plus length ≤ 254) is enough. No email address is ever put in a URL: the unsubscribe token carries a subscription id only. The footer form posts to the SSR server and works without JavaScript (SEO-01, NFR-19).

## Frontend
Storefront: footer subscribe form (added back to the chrome, Q-NLS-2 default yes) in `features/customers/newsletter`; `/newsletter/unsubscribe` confirmation page (`noindex`); Account → Preferences toggle (`/account/preferences`). The registration checkbox is wired in the auth sprint.

## Risks & alternatives
Single opt-in (default Q-NLS-1); double opt-in is a status addition later (a `PEND` lookup row, no schema break). Mail scanners that prefetch GET will unsubscribe silently: accepted (errs toward not mailing).

## Security & coupling review
| Area | How this feature meets it |
|---|---|
| Authorization | public endpoints named `Public` + `strict`; account endpoints `Policies.Customer`; token is HMAC-signed, constant-time compared |
| Input | email length/format validated; token length-limited and parsed strictly |
| Output encoding | email HTML-encoded; header values stripped of CR/LF (header injection) |
| Outbound calls | SMTP via ADR 0022 only |
| Resource limits | rate limit; body limit; audience page ≤ 500 |
| Concurrency | unique email index; `INSERT … ON CONFLICT` |
| Idempotency | subscribe/unsubscribe repeat-safe; welcome mail sent once per (re)subscription (inbox + `SENT_DTE`) |
| Audit | `NWSL_HIST` append-only with source and time |
| Data protection | emails are personal data: never in logs, events or URLs; never shared (NFR-26) |
| Secrets and config | `LinkSigningKey` secret read from configuration (the rule that it must be set outside Development is parked for S8) |
| Error responses | neutral problem-free responses on public endpoints |
| New dependencies | none beyond ADR 0022 |
| Coupling | `INewsletterAudience` contract for a future sender; no cross-module table access |
| Abuse cases | (1) Subscribing a stranger's email repeatedly (mail bombing): neutral response, one welcome per transition, rate limit. (2) Forging an unsubscribe link for others: HMAC check. (3) Enumerating subscribers: identical responses |
