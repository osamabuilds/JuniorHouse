# Admin redesign (v9)

A redesign of the staff back office, drawn in the storefront's visual language: Manrope, ink on white, one marigold primary action per screen. **Design only.** Nothing in `src/` has changed. The cards are `AdminShell`, `AdminDashboard`, `AdminList`, `AdminDetail`, `AdminProductEditor`, `AdminStation`, `AdminQuality`, `AdminFoundations`; the CSS is the `adm-*` block at the end of `components/bundle.css` with a media-query twin in `handoff/admin/_adm.scss`.

> **Needs the owner's decision first.** `CLAUDE.md` currently says the admin "keeps its current look and is out of scope" for the design system. This redesign was requested directly, so it is drawn here, but that line must be changed (and a spec amendment written) before anyone builds it. The older v8 admin skin (`.ad`, `handoff/admin/_admin.scss`) is superseded by this one and can be deleted when the new one ships.

## What was wrong (from the current app)

| Finding | Where | Effect |
|---|---|---|
| 20 links in one wrapping header row, in no particular order | `app.html` | Hard to find anything; wraps to 3 rows on laptops; unusable on phones |
| Everyone lands on Reference data | `app.routes.ts` | A picker sees the least relevant page first |
| Sign out, Sign out everywhere and Security sit in the header as three loose items | `app.html` | Easy to hit "Sign out everywhere" by mistake |
| Footer reads "Sprint 7 (Publish)" | `app.html` | Stale, internal wording shown to staff |
| Filters as a form card with checkboxes and a Search button | orders page | Active filters are not visible; takes half a screen |
| Up to 7 buttons in a detail header, two of them primary | purchase order detail | No clear next step |
| "Loading…" text, plain error paragraphs | most pages | No skeletons, empty and error states differ per page |
| Station screens use the same page chrome and small controls as a desk screen | pick and pack | Hard to use with a scanner and gloves |

## Information architecture

Same routes and permissions; only grouping and labels change. A group shows only if the role can open at least one link in it; a collapsed group shows the sum of its counts.

| Group | Links (route) |
|---|---|
| Today | `/` (new home, see Open questions) |
| Sell | Orders `/orders`, Discount codes `/discounts`, Delivery fees `/shipping`, Notify-me demand `/customers/notify-me` |
| Fulfil | Pick `/fulfilment/pick`, Pack `/fulfilment/pack`, Pick exceptions `/fulfilment/exceptions` |
| Warehouse | Putaway, Stock, Stock alerts, Bins, Adjustments, Cycle counts (`/warehouse/*`) |
| Make | Vendors, Purchase orders, Production, Receiving & QC, Labelling |
| Catalog | Products and Size charts `/catalog/*`, Styles, Reference data |
| Storefront | Storefront settings `/storefront` (its own tabs) |
| Admin | Staff, Roles, Audit trail, Notification log, Security (in the user menu) |

## Principles

1. **One clear next step.** One marigold primary per screen; everything else is secondary, ghost or in a More menu. Destructive actions confirm in a dialog.
2. **Say where I am.** Breadcrumb, a title, one sentence, a status badge. A stepper shows progress through a status flow.
3. **Show what is filtered.** Saved views as tabs with counts, active filters as removable chips.
4. **Status is a word plus a dot.** Never colour alone; the text is always ink.
5. **Responsive by structure.** Drawer navigation below 64rem; tables become cards below 40rem; no sideways page scroll; 44px touch targets and 16px inputs on touch.
6. **Honest states.** Skeletons while loading, an empty state that says why, an error banner that stays and says what to do.
7. **Stations are different.** Scanner-first, big results, a bottom dock, no chrome.

## Accessibility (WCAG 2.2 AA)

Landmarks (`header`, `nav`, `main`, `aside`) and one `<h1>` per page; `aria-current="page"` on navigation, saved views and tabs; disclosure groups use `aria-expanded`; the drawer is a modal dialog with focus trap, Esc and focus return; errors use `role="alert"`, successes `role="status"`; every field has a visible label, errors are linked with `aria-describedby`; focus ring 2px with 2px offset; tables use `<th scope>`; the progress bar has `role="progressbar"`. Text contrast: muted text `#5f5f5f` on white is 6.3:1.

## Moving the current app over

The current templates use class names such as `.button`, `.badge`, `.card`, `.table`, `.banner`, `.form-field`. Migration is a rename plus the shell:

| Today | New |
|---|---|
| `.button`, `.button--primary`, `--small`, `--danger` | `.adm-btn`, `--primary`, `--sm`, `--danger` |
| `.badge` | `.adm-badge` with `--ok`, `--warn`, `--bad`, `--info`, `--draft` |
| `.card` | `.adm-card` (`__h`, `__b`, `__f`) |
| `.banner--error/--success/--info` | `.adm-banner--bad/--ok/--info/--warn` |
| `table.table` | `.adm-table` inside `.adm-tablewrap`; add `data-label` to cells |
| `.form-field`, `.form-grid` | `.adm-field`, `.adm-cols--2/3` |
| `.page-intro` | the `<p>` in `.adm-head` |

Shell work: replace `app.html` with the new header, sidebar and drawer (group open state in `localStorage`, wrapped in try/catch); keep `hasPermission` on every link; hide the shell on station routes (`chromeless()` already exists); remove the footer text and move outbox health to Today and the bell. Suggested order: shell and foundations, lists, details, forms, stations. Each feature's screens need their spec amended before code (CLAUDE.md workflow).

## Open questions

1. **Admin scope.** May `CLAUDE.md` be changed so the admin is in the design system? (Required to build any of this.)
2. **Today page.** A new page not in any spec. Which counts and queue items does each role get?
3. **Go to box.** Page names only (proposed), or search of records (orders, POs, SKUs) too? Records would need an API.
4. **Bell.** What does it list: stock alerts only, or also exceptions and failed notifications?
5. **Group names and the Warehouse split.** Confirm labels with the people who use them daily.
6. **Pack station and print flow.** Only the pick station is drawn; pack follows the same pattern but has not been designed.
7. **Dark mode.** Out of scope; admin stays light.
