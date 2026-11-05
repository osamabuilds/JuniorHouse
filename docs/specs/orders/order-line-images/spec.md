# Order line pictures

- **Status:** Draft (awaiting owner approval)
- **Jira:** SCRUM-217 follow-up (new key to be created on approval; owner request 2026-11-05)
- **BRD sections:** none. This is an owner request, not a BRD requirement, so it carries no `FR-`/`NFR-` ID. It extends the order-confirmation email and the order line snapshot ([orders](../orders/spec.md), [order-confirmation](../../notifications/order-confirmation/spec.md)).
- **Requirement IDs:** none from the BRD. NFR-19 (alt text) and SEC-25 (no personal data in logs) still apply. Criterion IDs are prefixed `OLI-`.

## Problem & intent
A customer reading the order email should recognise what they bought at a glance. Today the order summary lists text only (quantity, name, colour, size). The owner wants the **exact product picture the shopper saw on the website for that line** beside each line item, **saved when the order is placed**, so the email (and any later order page) shows what was bought even if the catalogue photos change afterwards.

## Owner decisions (2026-11-05)
1. **Which picture:** the exact picture the storefront showed for that line in the bag (the picture for the line's colour; the same file the cart uses).
2. **When it is fixed:** saved on the order line **at order time**, not looked up at send time.
3. **Format** (recommendation accepted): pictures appear in the **HTML email only**, as the **JPEG** version (many email clients cannot show WebP), about 80 px wide, with the product name as alt text. The plain-text email and WhatsApp stay as they are.

## User flow
1. A shopper adds a product in a colour and size to the bag; the bag shows its picture (existing behaviour).
2. The shopper places the order. The server prices the cart as today and, for each line, also copies the picture's addresses and alt text onto the order line `(new)`.
3. The confirmation email is sent from the outbox as today. Each line in the order summary shows its saved picture beside the text.
4. A line with no finished picture at order time is shown as text only, with no broken image `(new)`.

## Acceptance criteria

| ID | Given / When / Then | Source |
|---|---|---|
| OLI-1 | Given a cart line whose variant has a picture, when the order is placed, then the order line stores that picture's WebP address, JPEG address and alt text, copied from what the bag showed | owner decisions 1, 2 |
| OLI-2 | Given the picture is changed or removed in the catalogue after the order, when the confirmation email is sent or re-sent, then it still shows the picture saved at order time | owner decision 2 |
| OLI-3 | Given the HTML confirmation email, when it is rendered, then each line shows its JPEG as an `<img>` with an absolute `src` (public site address + saved path), `width`/`height` and alt text; the text version and the WhatsApp message carry no picture | owner decision 3, NFR-19 |
| OLI-4 | Given a line with no saved picture, when the email is rendered, then the line is text only and has no `<img>` | (new) |
| OLI-5 | Given a product name or alt text containing markup, when the email is rendered, then it is HTML-encoded and cannot break out of the attribute | CHN-9, OCF-7 (existing rule) |
| OLI-6 | Given the public site address is not configured, when the email is rendered, then no picture is shown (a relative path is broken in a mail client) | (new) |
| OLI-7 | Given an order placed before this change, when its email is rendered, then it renders text only without error | (new) |
| OLI-8 | Given the picture lookup fails or is slow, when the order is placed, then the order is still placed, with no picture on that line (pictures never block an order) | NFR-08, NFR-FT-08 (new) |
| OLI-9 | Given an order placed in the running stack for products that have test pictures, when the confirmation email arrives in Mailpit, then each line shows its own picture and the picture address returns an image | owner testing request |

## Non-functional constraints
- NFR-19: every image has alt text; when the catalogue alt text is empty, use the product name with the colour.
- SEC-25: picture addresses are not personal data; nothing new about the customer is stored or logged.
- A picture must never delay or fail placement (OLI-8).

## Out of scope
- Pictures in the plain-text email, WhatsApp and SMS.
- Pictures on the storefront order-placed page and the track-order page (the data will be there; showing it is a later request).
- Back-filling pictures onto orders placed before this change.

## Open questions
- [ ] **Q-OLI-1 Test pictures.** The owner asked for test pictures downloaded from the web for the relevant collections. **Recommendation:** download a few freely licensed kidswear photos (Unsplash or Pexels licences allow this) into the local demo data only, so each demo product shows a real picture, and record each source and licence next to the seed. They are never used in a live catalogue. Needs the owner's yes that this is demo data only.
- [ ] **Q-OLI-2 Which emails?** The `{{Lines}}` block is shared by the confirmation, cancellation and COD-confirmation emails. **Recommendation:** show pictures in all three (one block, consistent). Alternative: confirmation only.
- [ ] **Q-OLI-3 Picture size.** **Recommendation:** the bag's 320 px thumbnail, displayed at 80 px (sharp on high-density screens). Larger files make the email heavier.

## As built
(filled in when implemented)
