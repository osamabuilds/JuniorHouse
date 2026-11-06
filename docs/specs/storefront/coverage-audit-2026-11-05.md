# Storefront acceptance-criteria coverage audit (2026-11-05)

Method: every `| ID |` row in each implemented storefront, customers and orders spec was searched for in `src/web/projects`, `src/web/e2e` and `src/api/tests`. An ID with no hit is a gap *candidate*; each was then checked by hand. Features that are draft, parked or deliberately deferred are listed separately and are not counted as gaps.

## Result for the implemented storefront specs

Everything not listed here has at least one test that names its ID.

| ID | Finding | Action |
|---|---|---|
| CR-1, CR-2 | No test | **Fixed**: e2e `CR-1 CR-2 header layout` (phone, tablet, desktop). Also found that the CR-2 wording did not match what was built (search icon sits beside the menu button, left of the centred wordmark); spec text corrected, owner to confirm |
| CR-4 | Covered by `SC-11 O-7` footer test, not tagged | **Fixed**: tagged |
| CR-6 | Covered by the SC-16/SC-19 e2e (axe, 44px, no scroll), not tagged | **Fixed**: tagged |
| SC-20, NFR-01 budgets | **No Lighthouse CI exists** (no config in the repo or workflows), although CLAUDE.md says budgets are enforced in CI | **Open gap**: needs a decision on how to run it (see below) |
| SC-15 | Size-guide table is rendered by the page block table component; no test asserts "every configured age, `<th scope>`" for the size guide | **Fixed**: container test `SC-15 a size guide table lists every configured age` (table, caption, `<th scope="col">`, one row per enabled age) |
| PR-14 | Content-page facts come from settings; draft never served is covered by ST-8 but no test asserts "no fact typed into a template" | **Fixed**: two container tests change the WhatsApp, email, hours, exchange and delivery facts and see the pages change; with no facts the pages show nothing |
| ST-5, ST-6 | No API test for "enabled entry with no live products returns 200 empty + noindex" or "sold items of the current collection shown as sold-out cards" | **ST-5 fixed** (`StorefrontEntryEmptyStateTests`: an enabled collection with no live products, and an audience with everything out of stock, are found and empty; `noindex` is the existing browser test LST-5). **ST-6 stays and wins over LST-15** (owner, 2026-11-05; Q-ST-8). Not built yet: needs Q-ST-9 answered, then plan, tasks and tests |
| DSF-4 | Contrast per surface is only covered indirectly by axe on `/_foundations` | **Fixed**: `design-tokens-contrast.spec.ts` checks contrast per surface on `tokens.json` and that it agrees with the stylesheet. It found `line-dark` still the old brown in `tokens.json`; aligned to the stylesheet's `#3A3A3A` |
| SRC-10 | Search rate limit deferred with the `seo` feature by the owner | Not a gap |
| PDP-14 | Belongs to `seo`, deferred by the owner | Not a gap |
| CR-7, DSF-16, PR-17, SC-21 | Diff-scope rules ("touch only the storefront"), not testable by an automated test | Verified by PR review; recorded as **Review** |
| PR-14 | see above | |

## Not counted (not implemented or not in scope)

- `seo` (SEOX-1..20): deferred by the owner.
- `product-recommendations` (REC-1..14) and `order-line-images` (OLI-1..9): draft specs, not approved.
- `child-profiles` (CHD-1..12): parked.
- `payments` (PAY-*), `order-queue` (OQ-3..6, 10), `orders` (ORD-2, 13, 14), `checkout` (CHK-5, 7), `cod-confirmation` (COD-7, 11, 17), `order-lookup` (OLK-8, 10), `accounts` (ACT-15, 19..22, 24, 26, 30, 32): the grep found no tag. These are outside the storefront scope of this request and have **not** been checked by hand; a follow-up pass is needed before claiming 100% there. Several are probably covered under another ID or in .NET test names.

## Honest status

100% is **not** reached. Of the storefront gaps, CR-1, CR-2, CR-4 and CR-6 are closed now. SC-15, PR-14, ST-5 and DSF-4 need new tests, and SC-20 needs Lighthouse CI (a new CI job, which is a decision, not just a test). Four criteria are review-only by nature.
