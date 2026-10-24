# SCRUM-xxx: Mega-menu and mobile menu to the new design references

- **Status:** Approved, deferred by the owner (2026-10-25): spec and plan are approved, no code is started; resume from tasks.md
- **Jira:** SCRUM-xxx (to be created under SCRUM-203)
- **BRD sections:** §4.1 FEAT-CAT-01
- **Requirement IDs:** FR-01 (presentation), NFR-17, NFR-19, SEO-01
- **Design source:** `docs/design/navigation-menu/` (`menu-desktop.html`, `menu-mobile.html`). Amends [site-chrome](../site-chrome/spec.md) (SC-3a, SC-5, SC-6, SC-7) and [chrome-redesign](../chrome-redesign/spec.md); it does not replace them.
- **Branch:** `menu-redesign`, cut from `main`.

## Problem & intent
The shipped menus (SCRUM-205) show the data tree plainly. The new references add a clearer structure: labelled columns, a "Shop all" link per entry, an editorial column and photo tiles on desktop, and an age-chip drawer on phones. The aim is to match the references' structure and look **while keeping the menu driven by settings**: nothing the menu shows is written in a template.

## Owner decisions that still stand
Header entries Girls, Boys, New in, Collections, Sale (no Baby); ages one per year, 1Y to 8Y (O-1, O-3); no Account or Bag until those features exist (O-4); menu entries, ages and tiles come from settings (reference README). The references show other sample entries and age bands; these are placeholders and are **not** adopted.

## What changes

### Desktop panel (64rem and up)
| Part | Shipped | Reference | Change |
|---|---|---|---|
| Column headings | "Shop by age" only | Every column has an eyebrow heading ("Shop by category", "Shop by age", "The edit") | Each column gets a heading from settings; the age column keeps its heading |
| Shop all | none | Last link in the first column, ruled off, "Shop all girls →" | Add "Shop all {label} →" as the entry's own link, from the entry label and target |
| Editorial column | none | "The edit": New arrivals, Essentials, Occasion, Sale | Optional column of plain links, from settings |
| Photo tiles | none | Two linked tiles on the right | Optional tiles (image, label, link), from settings; hidden when none are configured |
| Page behind | not dimmed | 40% dim | Dim scrim under the panel; a click closes it (SC-6 already closes on outside click) |
| Link type | 22px display font, `translateX` on hover | 15px, 8px vertical padding, underline on hover | Restyle |
| Open entry | underline | underline stays on the open item | Already so; keep |

### Mobile drawer (under 64rem)
| Part | Shipped | Reference | Change |
|---|---|---|---|
| Row | accordion from the tree | 56px rows, 18px semibold, chevron | Restyle to 56px rows |
| Expanded entry | tree of links | "Shop all {entry}", a "Category" label and links, an "Age" label and **chips** | Add Shop all, group labels and age chips (44px, pill) |
| Bottom bar | none | Account, Search, Size guide | Add **Search and Size guide** only (links from settings); Account waits for accounts (O-4) |
| Top bar | "Menu" label and close | Close, wordmark, bag | Close and wordmark; Bag waits |

## Settings contract additions (amends the SiteSettings contract of SCRUM-206)
All optional, so existing data keeps working and a missing value hides the part.
- `NavItem.showShopAll` (default true for menu entries): renders "Shop all {label} →".
- `NavItem.columns`: optional headings for the menu's member columns (`categoryHeading`, `ageHeading`).
- `NavItem.edit`: `{ heading, links[] }`, the editorial column.
- `NavItem.tiles`: up to two `{ label, imageId, link }` (image from the media library, with alt text).
- `SiteSettings.mobileBar`: the bottom-bar links (launch: Search, Size guide).

The admin editor (storefront-settings) gains matching fields. **This is part of the work, not a follow-up** (O-6).

## Acceptance criteria
| ID | Given / When / Then | Source |
|---|---|---|
| MR-1 | Given an open menu entry, then every column has a visible heading from settings, and the panel markup (headings and links) is in the server HTML | reference, SEO-01 |
| MR-2 | Given a menu entry, then its panel ends the category column with "Shop all {label} →" linking to the entry's own target; given `showShopAll` is false, it is absent | reference |
| MR-3 | Given `edit` is configured, then an "edit" column of plain links shows; given it is not, no empty column shows | reference |
| MR-4 | Given tiles are configured, then each is an `<a>` containing an image with non-empty alt and a visible label; given none, no tile area shows | reference, SEO-07 |
| MR-5 | Given the panel is open, then a scrim dims the page behind it; clicking the scrim, Esc or a link closes it, and focus returns to the trigger | SC-6 |
| MR-6 | Given the phone drawer, then rows are at least 56px, an expanded entry shows Shop all, a "Category" label with its links, an "Age" label with chips, and every chip and link is at least 44px | reference, NFR-19 |
| MR-7 | Given the drawer bottom bar, then it shows only the configured links (launch: Search, Size guide) and no Account or Bag | O-4 |
| MR-8 | Given the settings gain, lose or reorder a heading, edit link, tile or bar link, then the menus follow with no code change; the validation test covers duplicate keys and links that do not resolve | SC-3, SC-4 |
| MR-9 | Given every menu control, then contrast is 4.5:1 for text and 3:1 for borders, focus is visible, `prefers-reduced-motion` removes the scrim fade and hover motion, and axe reports no violations open or closed at 390, 768 and 1280 px | SC-16..19 |
| MR-10 | Given the admin app, then an editor can set headings, the edit column, tiles and the bar links, and invalid values are refused with a message | O-6 |
| MR-11 | Given the existing site-chrome criteria SC-1..SC-19, then they still pass | site-chrome |

## Non-functional constraints
NFR-17, NFR-19 WCAG 2.2 AA, SEO-01 (all menu content in the server HTML). Tile images use `rp-image` with explicit dimensions (no layout shift) and load lazily. No `innerHTML`.

## Out of scope
Account and Bag controls; the sample categories, age bands and tile photos in the references; search overlay changes; the content of the Boys, New in, Collections and Sale entries (set by the owner in settings, not designed here).

## Open questions
- [x] **Q-MR-1 Age chips.** The reference shows age *bands* ("2–3 Y"); the owner decided one chip per year (1Y to 8Y). Keep per-year chips (recommended; eight chips wrap onto two lines) or group them? **Resolved: recommendation adopted.**
- [x] **Q-MR-2 Age query parameter.** Menu age links use `?age=<code>`; SEO-02 discourages query strings on indexable pages (the references' README raises the same). Keep `?age=` for now (recommended, as built) or move to path URLs such as `/girls/age-4`? Path URLs would need the listings spec to change. **Resolved: recommendation adopted.**
- [x] **Q-MR-3 Tiles need images.** Tiles use the media library. Is there a source of editorial photos, or should tiles stay hidden at launch (recommended, since the launch set has no photos)? **Resolved: recommendation adopted.**
- [x] **Q-MR-4 "The edit" content.** The references list New arrivals, Essentials, Occasion, Sale. Which links does the owner want at launch (or none)? **Resolved: recommendation adopted.**
- [x] **Q-MR-5 Admin work.** The settings and admin additions touch the backend contract. Include them in this feature (recommended, per O-6) or split into a second feature after the storefront part? **Resolved: recommendation adopted.**
- [ ] **Q-MR-6 Jira key.**
