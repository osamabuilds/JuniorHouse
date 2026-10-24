# SCRUM-xxx: Storefront header and footer to the new design references

- **Status:** Implemented (2026-10-24); Playwright axe and visual check pending (CI)
- **Jira:** SCRUM-xxx (to be created under SCRUM-203)
- **BRD sections:** §4.1 FEAT-CAT-01, §14
- **Requirement IDs:** FR-01 (presentation), NFR-17, NFR-19, SEO-01
- **Design source:** `docs/design/shared/` (header, footer), `docs/design/navigation-menu/`. Amends [site-chrome](../site-chrome/spec.md); it does not replace it.
- **Branch:** `SCRUM-xxx-chrome-redesign`, cut from `main`.

## Problem & intent
The owner added new design references (2026-10-24) and asked that the **shipped** screens follow them. Only the header and footer are in scope. Home, category, product, the menus and every other screen stay as built. The references for screens not built yet (account, cart, checkout, search, wishlist and so on) are used when those screens are built.

## What changes
| Part | Shipped (site-chrome) | New reference | Change |
|---|---|---|---|
| Header layout | Menu and search left, centred wordmark with marigold dot, search field right | Wordmark left, primary nav centre, tool icons right | Re-lay out at 64rem and up; phone layout keeps the menu button, wordmark and search icon |
| Header tools | Search field | Search, Account and Bag icon buttons | Search stays. Account and Bag are **not added yet**: they point at pages that do not exist (O-4) |
| Footer surface | Dark band, giant decorative wordmark | White, hairline top border, `romp kids` heading, then link columns, then a legal row | Restyle to light; drop the giant wordmark |
| Footer content | Tagline, WhatsApp link, settings-driven columns | Same, plus a newsletter form | Columns, tagline and WhatsApp stay settings-driven. **Newsletter not added** (O-5) |
| Announcement bar | Settings-driven, ships disabled | Black bar, uppercase 12px | Style only; stays settings-driven |

## Acceptance criteria
| ID | Given / When / Then | Source |
|---|---|---|
| CR-1 | Given a viewport of 64rem or more, then the header shows the wordmark at the left, the primary nav in the middle and the search control at the right, all on one row with the hairline bottom border | design `shared/header.html` |
| CR-2 | Given a viewport under 64rem, then the header is unchanged: menu button, wordmark, search icon | site-chrome SC-7 |
| CR-3 | Given the footer, then it is on the light surface with a hairline top border, a `romp kids` heading (brand name from configuration), the settings-driven columns and a legal row; the giant decorative wordmark is gone | design `shared/footer.html` |
| CR-4 | Given the footer, then the tagline and the WhatsApp link are still read from the brand configuration, and the columns from the settings (SC-3, SC-11, SC-14 still hold) | site-chrome |
| CR-5 | Given the chrome, then there is no Account icon, Bag icon or newsletter form until their features exist | O-4, O-5 |
| CR-6 | Given the chrome after the change, then every existing site-chrome criterion SC-1..SC-19 still passes: landmarks, 44px targets, focus ring, contrast 4.5:1 and 3:1, no horizontal scroll at 390, 768 and 1280 px, axe clean | site-chrome |
| CR-7 | Given the diff, then it touches only the storefront header and footer components, their styles and tests (no tokens, no admin project, no other pages) | owner instruction |

## Out of scope
Home, category, product and info pages; the mega-menu and mobile menu (they already follow the navigation reference closely enough that no change is proposed); Account and Bag icons; newsletter; any change to colours or fonts in the token file.

## Open questions
- [x] **Q-CR-1 Marigold dot.** The reference wordmark is plain text `romp kids`; the shipped wordmark has a marigold dot. Keep the dot (recommended, it is the shipped brand mark) or drop it to match the reference? **Resolved: keep the dot.**
- [x] **Q-CR-2 Footer wordmark.** The reference footer uses the same `romp kids` wording; the shipped brand name is configured as one word. Show the configured brand name (recommended), or the literal `romp kids`? **Resolved: configured brand name.**
- [x] **Q-CR-3 Footer on dark.** The shipped dark footer was a deliberate site-chrome choice. Confirm the move to a light footer. **Resolved: light footer.**
- [ ] **Q-CR-4 Jira key.** Which SCRUM key should this carry?
