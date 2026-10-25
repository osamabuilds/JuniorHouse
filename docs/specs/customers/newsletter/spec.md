# Newsletter consent

- **Status:** Draft (2026-10-25)
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
- [ ] **Q-NLS-1 Double opt-in.** Default: single opt-in with a welcome/confirmation email carrying the unsubscribe link. Choose double opt-in (confirm link first) if you want stricter consent.
- [ ] **Q-NLS-2 Footer form.** Add the footer subscribe form this sprint (it was deferred in site-chrome)?
