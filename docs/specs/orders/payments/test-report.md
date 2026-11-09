# Payments: test report (S12b, 2026-11-09)

What was run after the payment code was written, what it proved, what it found, and what it could not prove yet. Everything ran against the **pretend gateway**; nothing here proves anything about a real provider. The real adapter, once it exists, repeats the live checks in "Live check" of the [third-party checklist](../../../roadmap/third-party-integrations.md).

## The "no duplicate payment" guarantee, and how it is proved

| Layer | Rule | Proof |
|---|---|---|
| Domain | Only the first success counts; a repeat changes nothing; a second charge is flagged, never confirmed again; a wrong amount is held; money for a cancelled order is held | `PaymentDomainTests` (every transition) |
| Database | A payment row is locked (`FOR UPDATE`) before any message about it is read or applied; each attempt number and each provider event id can be stored once | `NoDuplicatePaymentTests` against PostgreSQL |
| One page per order | A second press returns the page already open; the gateway call is idempotent on `<order>-A<n>` | `PaymentFlowTests`, `NoDuplicatePaymentTests` (40 clicks at once give one attempt, one session) |
| Races | 40 identical callbacks at once; 40 different callbacks for one attempt; webhook + reconciliation + expiry at once; a staff cancel against a payment | `NoDuplicatePaymentTests`: exactly one paid event, never paid and cancelled by the system |
| Randomised | 8 random runs of 16 operations each (start, success, decline, repeat, wrong amount, reconcile, expire, time passing) | invariants checked after every step: at most one paid event, at most one open try, a second paid attempt is always flagged |
| Browser | Pressing Pay twice, reloading the return page, Back | `payments.e2e.spec.ts` against the running stack |

## Results

| Kind | What | Result |
|---|---|---|
| Unit | Payment rules, circuit breaker, fake gateway signatures and replay window, options guard, settings | `PaymentDomainTests`, `GatewayTests`: all pass |
| Integration (PostgreSQL, Testcontainers) | Whole flow, races, hygiene, customer reads, session expiry | `Romp.Modules.Orders.Tests`: **300 pass**; `Romp.Modules.Cart.Tests` (event handlers, D6): **157 pass** |
| Integration (real HTTP API) | Endpoints, attacks over HTTP, authorization matrix | `PaymentEndpointsTests`, `AuthorizationMatrixTests`, `CheckoutEndpointsTests`: pass (CI is the verification, see "Not proved") |
| Storefront unit | API service, store (polling, give-up, double press), page states, legal placeholders, safe redirect addresses | **794 pass** (108 files); production build compiles; the payment page is a separate 10 KB chunk |
| Browser (Playwright, axe WCAG 2.2 AA) | Pay, decline then retry, lost callback, second Pay, tampered link | `payments.e2e.spec.ts`: **6 pass** on the running stack |
| Architecture and naming | Module boundaries, table and column names, glossary, folder structure | **174 pass** (one run showed a stray `TestResults` folder created by my own test runner, not by the code) |
| Security | Forged, stale, tampered, replayed, oversized, malformed and injected messages; tokens (edited, borrowed, expired, wrong purpose, absurdly long); who can read whose order; nothing but a hash kept; no personal data in events | `PaymentSecurityTests` (about 45 cases) and the HTTP tests |
| Penetration test (live) | `scripts/security/payments-pentest.mjs` against the running stack | **60 of 60 checks pass**; see below |
| Performance (in process) | 100 orders paying at once; 500 forged callbacks; 300-order expiry backlog; signature check cost | 190 paid callbacks a second, p95 120 ms; forged callbacks 870 a second, p95 165 ms; backlog of 300 cleared in 15 s; one signature check about 8 microseconds |
| Load (live) | `scripts/perf/payments-load.mjs`, 500 callers at once, 5,000 requests per scenario, rate limits raised for the run | all budgets held, no error status: signed callbacks about 1,000 a second (p95 654 ms), duplicate callbacks about 1,970 a second (p95 434 ms), forged callbacks about 2,100 a second (p95 537 ms), status 4,100 a second (p95 169 ms), payment methods 4,700 a second (p95 181 ms); outbox empty afterwards |

## What testing found, and what was done

1. **The lock SQL used the wrong table name** (`PYMT_MAIN` instead of `PAYM_MAIN`). Every webhook failed in the integration tests. Fixed before anything else.
2. **A flood of forged callbacks queued thousands of audit events and starved real order events** (found by the load test: 33,000 junk log rows and 7,500 queued `AuditRecorded` events delayed real order processing by minutes). Fixed three ways: the webhook log is no longer audited (`[NotAudited]`), only a budget of rejected messages is stored a minute (default 60, the rest are only logged), and rejected records older than 30 days are purged. Verified live: a full pen test now stores 60 rows and queues no audit events. Tests: `WebhookHygieneTests`, and an HTTP test.
3. **The storefront proxy dropped the redirect after the demo payment page** because the API sent an absolute address. Fixed: a same-host absolute address is sent on as a path; anything else is dropped.
4. **The authorization matrix lacked the two recommendations routes** (left by the earlier recommendations work). Added.
5. Harness notes, not product bugs: an Apache server on this machine's IPv4 port 8080 answered some load-test connections with 404 until the scripts used one address family; the product pages no longer allow adding a size with no stock, so the older checkout browser tests (not mine) need stocking first.

## Not proved, and why

- **Local Windows policy blocks the compiled integration tests**, so they were run in a Linux container with Docker (CI on Linux remains the real verification).
- **Pre-existing failures on `main` seen in a full-solution run, not touched by this work:** the recommendations test expects at most 4 similar items but the default row size is 8 (`RecommendationEndpointsTests`); the browser checkout tests `CHK-1` and `OLK-5` wait on background events and failed on this machine's slow outbox before the flood fix and need a fresh look; a password-hashing timing test fails under CPU load.
- **No real gateway exists yet.** Signature scheme, timestamp rules, hosted-page behaviour, refunds and settlement are all the fake's. Wallet (Easypaisa, JazzCash) flows differ per provider.
- **A refund is only a method on the port.** Duplicate charges and late payments are detected and flagged for staff; paying them back is S17.
- **Database connections:** at 500 concurrent callers PostgreSQL ran out of free connections for an extra diagnostic client, so extra requests queued (none failed). The connection pool and `max_connections` need sizing in S18 (load test).
- **Slow-lane risks:** if the gateway stays unreachable for the whole payment window, orders with an open try are not cancelled (by design, to avoid cancelling a paid order) and keep their stock until the gateway answers.
