# Romp: third-party integrations checklist

Everything here is built **behind a port with a fake implementation first**, so development and tests never wait on an external account. Each item below is switched on later by configuration (credentials and an enabled flag), then verified live with the steps in "Live check". Work through them one at a time and tick each box.

Status key: ☐ not started · ◐ account requested · ☑ live and verified

## How to read an entry
- **Needed by:** the sprint where the real integration is first required (the fake works before that).
- **I build meanwhile:** what exists with the fake, so you can try the flow locally.
- **I need from you:** accounts, credentials, decisions. Credentials go in secrets or configuration, never in git.
- **Live check:** how we confirm it works for real.

---

## 1. Hosting, domain and edge (S8 staging, S19 production)

| | |
|---|---|
| Status | ☐ |
| Needed by | S8 staging deploy (already waiting), S19 production |
| I build meanwhile | Staging topology and GitHub Actions deploy pipeline are written (ADR 0017); everything runs on docker compose locally. |
| I need from you | Domain name; VPS provider and plan (staging, later production with a Postgres primary + replica); Cloudflare account on a plan with DDoS protection (SEC-20); SSH access or a deploy key; GitHub Actions secrets. |
| Live check | Staging URL loads over HTTPS, deploy pipeline goes green, `/health/ready` passes behind Cloudflare. |

## 2. Card payment gateway (S12b fake, live before launch)

| | |
|---|---|
| Status | ☐ |
| Needed by | Live card payments before launch; fake covers S12b to S18. |
| I build meanwhile | `IPaymentGateway` port, a fake with configurable success, decline, timeout and signed callbacks, webhook signature check, reconciliation, circuit breaker, refund call (needed by S17). Online methods stay off until `Orders:EnabledOnlineMethods` is set. |
| I need from you | Which gateway (decision). Merchant account approval; sandbox and live API keys; webhook secret; the gateway's webhook signature scheme and sandbox test cards; supported methods (cards, wallets). |
| Live check | Sandbox payment succeeds and decline retries without a double charge; a forged webhook is rejected; a refund goes through. |

## 3. WhatsApp Business API (notifications built, live check pending)

| | |
|---|---|
| Status | ☐ |
| Needed by | Before launch (COD confirmation and order updates), S17 for the support inbox. |
| I build meanwhile | WhatsApp channel behind its port with a fake; inbound webhook; delivery log, retry, fallback to email (S13, done). |
| I need from you | WhatsApp Business account approval; business phone number; provider choice (Meta Cloud API or a BSP such as Twilio); access token; webhook verify token and app secret; **approved message templates** (order confirmation, COD confirm request, shipped, delayed, return updates). Template approval takes days, so submit early. |
| Live check | A real COD confirmation request reaches a test phone and the reply updates the order; a failed send falls back to email. |

## 4. Email sending (S13 built with Mailpit locally)

| | |
|---|---|
| Status | ☐ |
| Needed by | Staging and production. |
| I build meanwhile | `IEmailSender` port (ADR 0022); Mailpit catches mail locally. |
| I need from you | Provider and credentials (SMTP or an API such as SendGrid or SES); sending domain with SPF, DKIM and DMARC records; from-address and reply-to. |
| Live check | Order confirmation lands in a real inbox (not spam) and passes DKIM. |

## 5. CAPTCHA (guest order look-up, SEC-02 and SEC-06)

| | |
|---|---|
| Status | ☐ |
| Needed by | Staging. |
| I build meanwhile | Throttle and a "CAPTCHA required" state (HTTP 428) exist; a fake verifier accepts a test token. |
| I need from you | Provider choice (for example Cloudflare Turnstile, free) and site and secret keys. |
| Live check | After repeated wrong lookups the real challenge appears and a solved one lets the lookup proceed. |

## 6. Couriers (S15 build, S16 to S17 returns)

| | |
|---|---|
| Status | ☐ primary · ☐ secondary |
| Needed by | S15 for real bookings; fake courier works before that. |
| I build meanwhile | `ICourierGateway` port, a fake courier (booking, waybill, label, tracking webhooks, outages on demand), fallback to a secondary, manual-booking queue, manifest, return pickup booking. |
| I need from you | Which primary and secondary courier (decision). For each: merchant account, API credentials, base URL, pickup address and account/shipper codes, city and area code lists, the webhook or polling method for status, label format (their PDF or our own), sandbox access, rate card, COD fee and remittance cycle. |
| Live check | A test parcel books, returns a waybill, prints a label, tracks to delivered; unplugging the primary books on the secondary. |

## 7. GS1 Pakistan company prefix (barcodes)

| | |
|---|---|
| Status | ☐ |
| Needed by | Before real stock is labelled (S5 is built with a test prefix). |
| I build meanwhile | GTIN-13 generation with a configurable prefix; capacity caveat is in the S5 notes. |
| I need from you | The registered company prefix and the capacity it covers. |
| Live check | A generated GTIN passes a check-digit validator and scans on the real scanner. No migration is needed if the prefix is only configuration; confirm that when it arrives. |

## 8. Label printer and barcode scanner (S5 labels, S14 warehouse)

| | |
|---|---|
| Status | ☐ printer · ☐ scanner |
| Needed by | Real printing (S5 queue exists with a fake printer); scanning in S14. |
| I build meanwhile | Print queue with retry behind a printer port, fake printer that stores the output; scan input handled as keyboard text so any scanner works. |
| I need from you | Printer model and language (ZPL, TSPL), connection (USB, network, via a print service), label sizes; scanner model and whether it is a USB keyboard-wedge type or you want phone-camera scanning. |
| Live check | A tag prints at the right size and its barcode scans back to the same GTIN. |

## 9. Search engines and Merchant Center (S18, S19)

| | |
|---|---|
| Status | ☐ Search Console · ☐ Bing · ☐ Merchant Center |
| Needed by | Search Console and Bing from first production deploy (SEO-11); Merchant Center when the feed ships. |
| I build meanwhile | Sitemap, robots.txt, structured data (done); the feed generator and its tests (S18). |
| I need from you | Verified ownership of the domain for each tool (a DNS record or meta tag); a Merchant Center account and business details; decision whether Merchant Center is in the MVP. |
| Live check | Sitemap accepted in both consoles; feed has no disapproved items. |

## 10. Backups, object storage and monitoring (S19; some in S8)

| | |
|---|---|
| Status | ☐ |
| Needed by | S19, and staging if you want its uploads kept. |
| I build meanwhile | File storage and logging behind their ports, local volume locally; the restore drill script is written in S19. |
| I need from you | Off-server backup destination (S3-compatible bucket or similar) and credentials; where alerts should go (email address, WhatsApp, Slack) for security and error alerts (SEC-26). |
| Live check | A backup restores into a clean database inside the 4-hour RTO; a test alert reaches you. |

## 11. Refund channel for COD orders (S17, a business decision, no vendor yet)

| | |
|---|---|
| Status | ☐ |
| Needed by | S17. |
| I build meanwhile | Refund recorded as an instruction with a manual "mark paid" step. |
| I need from you | How COD refunds are paid (bank transfer, store credit, a mobile wallet) and, for any external wallet or bank API, its account and credentials. |
| Live check | A test refund is recorded, paid and confirmed to the customer. |

---

## Suggested order for you
1. **Start now (long lead times):** WhatsApp template approval (3), payment gateway merchant account (2), courier accounts (6), GS1 prefix (7), domain and VPS (1).
2. **Quick wins:** email provider and domain records (4), CAPTCHA keys (5).
3. **Later:** printer and scanner (8), search consoles (9), backups and monitoring (10), refund channel (11).

When you finish one, tell me and I'll switch it on in staging and walk through its live check with you.
