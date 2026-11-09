# Newsletter consent

- **Status:** Built (2026-11-04), see "As built"
- **Jira:** SCRUM-62 (FR-41) under epic SCRUM-9
- **BRD sections:** §7.5, §6.4 (TC-ACC-17), FEAT-ACC-06
- **Requirement IDs:** FR-41, NFR-26

## Problem & intent
People should be able to opt in to marketing email and out again, and an opt-out must hold for every future send. Romp sends no newsletters yet (campaigns are post-launch), so this sprint builds the consent record and the unsubscribe link, not a campaign tool.

## Requirements (quoted from the BRD)
> **FR-41:** The system shall support newsletter subscribe/unsubscribe, honoured on all future sends.

FEAT-ACC-06: "Newsletter subscribe/unsubscribe — Marketing-consent toggle." (MVP)

## User flow
1. **Subscribe** from the registration checkbox, an Account preference toggle, or the footer form (the footer newsletter was deferred in site-chrome and is added here `(new)`).
2. A confirmation email is sent `(new, Q-NLS-1)`.
3. **Unsubscribe** from the Account toggle or the link in any email (no sign-in needed; one click, then a confirmation page).
4. A later send reads the consent list; unsubscribed addresses are never included.

## Acceptance criteria

| ID | Given / When / Then | Source |
|---|---|---|
| NLS-1 | Given a visitor or customer, when they subscribe with an email, then a subscription is recorded once per email (case-insensitive); repeating it changes nothing. | FR-41 |
| NLS-2 | Given a subscribed customer, when they toggle off in their account, then the preference persists. | TC-ACC-17 |
| NLS-3 | Given an unsubscribe link, when opened (signed in or not), then the address is unsubscribed; the link cannot unsubscribe anyone else and does not reveal whether an address exists. | TC-ACC-17, FR-41 |
| NLS-4 | Given an unsubscribed address, when a later campaign list is produced, then it is excluded, and a later registration with the same email does not silently re-subscribe it. | FR-41 (new) |
| NLS-5 | Given the consent record, then it stores when and where consent was given or withdrawn (source: registration, account, footer, link). | (new) |
| NLS-6 | Given the public subscribe endpoint, then it is rate limited and its response is the same for known and unknown addresses. | NFR-15 (new) |
| NLS-7 | Given the module's contract, then another module (a future campaign sender) can ask "who is subscribed" without reading customer tables. | ADR 0002 |

## Non-functional constraints
Email addresses are personal data and are never sold or shared (NFR-26). Transactional mail (reset, order) ignores newsletter consent and must not carry marketing.

## Out of scope
Composing or sending campaigns, segments, WhatsApp marketing, an admin export (see Q-MOD-1).

## Open questions
- [x] **Q-NLS-1 Double opt-in (owner, 2026-11-04: single opt-in).** Default: single opt-in with a welcome/confirmation email carrying the unsubscribe link. Choose double opt-in (confirm link first) if you want stricter consent.
- [x] **Q-NLS-2 Footer form (owner, 2026-11-04: yes, added).** Add the footer subscribe form this sprint (it was deferred in site-chrome)?


## As built (2026-11-04)

What was built matches the criteria above, with these differences from the plan and the draft:

- **No customer id on the record.** `NWSL_SUBS` has no `CUST_ID`. Consent belongs to an address, and the account toggle finds the record by the customer's own email, so the column had no reader and would have made the table look customer-owned to the ownership scanner. Columns: `EML` (stored lower case, unique, a check constraint keeps it so), `STS_ID`, `CHNG_DTE`, `WLCM_SENT_DTE` and the audit columns. `NWSL_HIST` is append-only with the action, the source and `OCCR_DTE`. Both entities are `NotAudited` (the history is the audit record, and the address is not copied into the audit trail).
- **One place changes consent.** `NewsletterConsent` takes a transaction-scoped advisory lock on the address (`pg_advisory_xact_lock`), so simultaneous requests for one address run one after another and leave one record and one welcome (the unique index is the backstop). It replaces the plan's `INSERT ... ON CONFLICT`.
- **The welcome event is raised by the record.** `NewsletterSubscription.QueueWelcome()` raises `CustomerEmailRequestedEvent` (v1, kind `NewsletterWelcome`, the record id, no address) after the first save gives the record an id. There is no separate `ICustomerEmailQueue`; Notify Me raises the same event from its own record. The event lives in `Domain/Abstractions` (shared code, not a feature). `CustomerEmailConsumer` runs it behind the inbox guard, and an unknown kind throws so it retries and dead-letters instead of being skipped.
- **Welcome rules.** Sent at send time only if the record is still subscribed and has no `WLCM_SENT_DTE`. A send failure throws, the transaction rolls back and the dispatcher retries. A new subscription, and a re-subscribe after an unsubscribe, each get one welcome; a repeat subscribe gets none. The mail carries `List-Unsubscribe` (the one-click API link through the storefront pass-through) and `List-Unsubscribe-Post`, a visible unsubscribe link to the storefront and a plain-text twin. Neither link nor header holds the address.
- **Unsubscribe link.** The token is `base64url(id).base64url(HMAC-SHA256)`, at most 200 characters, compared in constant time. `GET` and `POST /api/newsletter/unsubscribe?token=` answer 200 for any validly signed token (even for a record that no longer exists) and 400 for anything else. Neither route checks `Origin` (a mail client sends none); the signature is the authority. On the storefront the link is `/newsletter/unsubscribe?token=`: the SSR server calls the API and redirects (303) to `/newsletter/unsubscribed?status=ok|invalid|error`, so it works without JavaScript.
- **Footer form.** A real `<form method="post" action="/newsletter/subscribe">`. With JavaScript the store handles it and announces the answer in a polite live region; without it the SSR server posts to the API and redirects to `/newsletter/subscribed?status=...`. The result pages are `noindex` and `no-store`. The footer text promises only what is built: news and offers, unsubscribe any time, never shared.
- **Public subscribe.** `POST /api/newsletter/subscribe` (Public, `strict` rate limit, 2 kB body, `Origin` check). The answer is `{ subscribed: true }` for a new, a repeated and a previously unsubscribed address; only an unreadable address is a 400. The rate limit is proved by endpoint metadata (`NewsletterEndpointsTests`), not by a 429 call, because the test host runs with a very high limit.
- **Account.** `GET` and `PUT /api/customers/me/newsletter` act on the signed-in customer's own email only, with source `Account`. The page is `/account/preferences`, with a labelled checkbox and a live region. Like the address and wishlist routes it does not use the `X-Romp-Csrf` header (a JSON `PUT` cannot be sent cross-site by a form, and the cookie is `SameSite=Lax`).
- **Registration checkbox (NL8).** `RegisterCommand.SubscribeNewsletter` (default false) is sent by an unticked-by-default checkbox on the register form. Only a ticked box calls `INewsletterOptIn` (a port in `Application/Abstractions`, so Accounts does not depend on Newsletter), which subscribes with source `Registration`. An unticked box never touches an existing record, so an unsubscribed address stays unsubscribed (NLS-4).
- **Audience (NLS-7).** `Customers.Contracts.INewsletterAudience.ListSubscribedAsync(afterId, take)`: keyset by record id, subscribed only, `take` clamped to 1..500, one query.
- **Configuration.** `Customers:LinkSigningKey` is at least 16 characters. The development key works only in Development and Testing; anywhere else the host refuses to start unless the key is a real secret of at least 32 characters. The docker stack and the e2e run use the development key.
- **Shared web filters moved.** `CustomerCsrfFilter` and `CustomerOriginCheckFilter` moved out of `Infrastructure/Accounts` to `Infrastructure`, so features other than Accounts can use them without breaking the feature-dependency rule.
- **Not done:** no campaign sending, segments or admin export (out of scope). `robots.txt` does not exist yet (the seo feature is deferred), so the new pages are protected only by `noindex` and `no-store`. Double opt-in would be a `PEND` status row plus a confirm link, with no schema break.
- **Side effects.** The storefront initial bundle grew from about 514 kB to 521 kB (the 500 kB budget was already exceeded; warning only). The e2e helper that registers a customer now picks the terms checkbox by name, because the register form has a second checkbox.
