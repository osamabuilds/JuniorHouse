# Notify Me (back in stock)

- **Status:** Draft (2026-10-25)
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
- [ ] **Q-NTM-1 SLA.** "A defined SLA" is not defined. Default: within 15 minutes of the stock event. The BRD example says "same business day"; which do you want?
- [ ] **Q-NTM-2 Retention.** Default: an open request expires after 90 days; a closed one is deleted after 30 days.
- [ ] **Q-NTM-3 Availability at send time.** If the variant sells out again before sending, send anyway (default) or skip and keep the request open?
- [ ] **Q-NTM-4 Which module owns the sold-out check.** Default: the Notify Me endpoint checks the variant against the Storefront read model through its contract, because that is what the shopper saw.
- [ ] **Q-NTM-5 Variant level.** Request per variant (size and colour), as FR-11 says. The sold-out size picker may be per size across colours; confirm per variant.
