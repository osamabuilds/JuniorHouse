# 0022 — Email sender port and customer mail through the outbox

- Status: Proposed (owner accepted the recommendation 2026-10-25)
- Date: 2026-10-25

## Context

Notify Me (restock email) and the newsletter (confirmation) must send email, and later password reset will too. No sender exists. Full notification delivery (retry policy, dead-letter, WhatsApp fallback, resend from admin, templates in data) is Sprint 13 (NFR-FT-01..08). A failed email must never roll back the business change that caused it (NFR-FT-08), and events must not carry personal data (ADR 0011).

## Decision

1. **A port, `IEmailSender`,** in a new building block project `Romp.BuildingBlocks.Email`: `SendAsync(EmailMessage, CancellationToken)`. `EmailMessage` = recipient, subject, plain-text body, optional HTML body, optional extra headers (`List-Unsubscribe`, `List-Unsubscribe-Post` for marketing). Implementations: `SmtpEmailSender` (production and local Mailpit), `FakeEmailSender` (tests, records messages).
2. **SMTP via MailKit** (MIT, new dependency, version pinned in `Directory.Packages.props` at implementation, checked to be at least two weeks old). Alternative: `System.Net.Mail.SmtpClient`, which Microsoft marks "not recommended for new development". Settings come from configuration only: `Email:Smtp:Host`, `Port`, `UseStartTls`, `User`, `FromAddress`, `FromName`; the password is a secret. The host is never taken from a request (SSRF) and options are validated at start-up; rejecting the local Mailpit address in production is parked for S8 with the other deployment checks.
3. **Sending goes through the owning module's outbox.** The business transaction writes a small event in the same transaction, `CustomerEmailRequestedEvent(Kind, RelatedId)` (ids only: **no address, no name, no body**). Its handler, in `Customers`, loads what it needs from its own tables at send time (recipient, product links), renders the template, and calls `IEmailSender`. A failure throws, so the dispatcher retries it with its existing backoff, and the business change is already committed. A handler sets `SENT_DTE` on success and the inbox guard prevents a double send on redelivery.
4. **Templates** are C# render functions in the module (`Customers/Emails/`), HTML-encoding every interpolated value; plain-text twin always sent. Two kinds this sprint: `RestockNotice`, `NewsletterWelcome`. Text lives in code now; data-driven templates arrive with S13.
5. **Marketing vs transactional.** Newsletter mail carries `List-Unsubscribe` and a signed unsubscribe link; transactional mail (restock notice) carries none and never marketing content.
6. **S13 replaces the handler, not the callers.** The module raises the same event; the Notifications module later consumes it (or the handler delegates to it).

## Alternatives considered

- **Send inline in the request/handler:** a slow or failing SMTP server would break or slow the shopper's request and could lose the change.
- **A hosted email API (SendGrid, SES) now:** a vendor and credentials decision that belongs with S13; SMTP covers Mailpit locally and any provider later.
- **Carry the address in the event:** breaks ADR 0011 and puts personal data in the outbox table.

## Consequences

- One new NuGet package and one new building block project.
- Emails are at-least-once; the inbox and `SENT_DTE` make a duplicate send unlikely but not impossible after a crash between send and commit (accepted; a restock notice is harmless to repeat).
- The dead-letter/resend experience is whatever the outbox dispatcher already provides until S13.
