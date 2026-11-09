# Warehouse app

- **Status:** Draft, applied by delegation (owner away, 2026-11-09)
- **Jira:** to be created
- **BRD sections:** §5.15, §5.16, §9
- **Requirement IDs:** NFR-SC-01, NFR-SC-07, FR-SC-10

## Problem & intent
Pickers and packers work standing up, with a scanner in one hand, on cheap devices and a weak network. The screens must be fast, large and forgiving.

## Requirements (quoted from the BRD)
> **NFR-SC-07:** The warehouse/pack-station system must remain usable on a low-spec local network (a solo-founder or small-team operation, not an enterprise LAN) — this favours a lightweight web app or PWA over a heavy native install for pick/pack scanning.

> **NFR-SC-01:** Barcode scan-to-confirm at pick/pack must resolve in under 300ms so it never becomes the bottleneck step at a pack station.

## Acceptance criteria

| ID | Given / When / Then | Source |
|---|---|---|
| APP-1 | Given a signed-out visitor, when a warehouse route is opened, then they are sent to sign in; the API refuses every call without the matching permission | SEC-08 |
| APP-2 | Given a signed-in staff member without the pick or pack permission, when they open the screen, then they see that they have no access and the API refuses the call (new) | SEC-08 |
| APP-3 | Given the pick or pack screen, when it opens, then the scan box has focus and stays focused after each scan, so a keyboard-wedge scanner works with no clicking | NFR-SC-07 |
| APP-4 | Given a scan result, then the screen shows a large success or refusal in words, in a colour and an icon (not colour alone), and the refusal sound or vibration is optional (new) | NFR-19 |
| APP-5 | Given a phone or tablet, then every control is at least 44 px and the layout works from 360 px wide | NFR-17 |
| APP-6 | Given a weak network, then each scan sends a small request and shows the result in under a second on a normal connection; a failed request is retried once and then shown clearly, never silently dropped (new) | NFR-SC-07 |
| APP-7 | Given a browser that supports it, then the app can be installed to the home screen (web manifest and icons) (new) | NFR-SC-07 |
| APP-8 | Given the queue, then it is paged and lists orders oldest first with their age, and refreshes without a full reload (new) | CONVENTIONS |
| APP-9 | Given the screens, then they follow the admin app's existing look (the design system is for the storefront only), have one heading, labelled controls and a visible focus | NFR-19 |
| APP-10 | Given the admin app, then it still sends `noindex, nofollow` | SEO-05 |

## Out of scope
Offline picking (the device must reach the server), camera scanning, voice picking, a native app.

## Open questions
- [ ] **Q-APP-1** Is an installable web app enough for the scanners the business will buy? Recommendation: yes for USB or Bluetooth keyboard-wedge scanners and for phones; confirm the model when bought.
