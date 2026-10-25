# Feature specs

Layout and rules: [`docs/CONSTITUTION.md`](../CONSTITUTION.md). In short, `docs/specs/<module>/<feature>/` mirrors the code path `Modules/<Module>/…/<Feature>/` and the web path `features/<module>/<feature>/`. Ticket numbers live in a spec's header (`Jira:`), never in a folder name.

Each feature folder has a `README.md` card (what it is, status, where the code is). Features specified from Sprint 5 onward (labelling) hold `spec.md`, `plan.md` and `tasks.md` in their own folder; a module may also have a module-wide `plan.md`. Earlier work was specified per sprint and lives in the module's `_sprint-specs/` folder (closed; features move out when next touched). Start a new feature from [`_template/`](_template/).

| Module | What it covers | Features |
|---|---|---|
| [reference](reference/README.md) | Shared lookup data | lookups |
| [catalog](catalog/README.md) | Styles; products, media, size charts, composition and care, URLs (Sprint 7, implemented) | styles, products, product-media, size-charts, composition-care, urls |
| [vendor](vendor/README.md) | Vendors and purchase orders | vendors, purchase-orders, revisions, files, vendor-responses, vendor-view |
| [production](production/README.md) | Production tracking | production-runs, milestones, samples, deliveries |
| [quality](quality/README.md) | Goods receipt and QC | inbound-deliveries, receiving, batches, inspections, outcomes, safety-checks |
| [labelling](labelling/README.md) | SKU, GTIN and tag printing | label-work, variants, print-jobs, tags |
| [warehouse](warehouse/README.md) | Bins, putaway, stock ledger, adjustments, cycle counts, alerts (implemented) | bins, putaway, stock-ledger, adjustments, cycle-counts, stock-alerts |
| [identity](identity/README.md) | Staff accounts, sign-in, MFA, roles, audit trail (Sprint 8, specs approved) | staff-accounts, sessions, mfa, roles, audit-trail |
| [platform](platform/README.md) | Cross-cutting, not a module: staging deployment, health probes, logging policy (Sprint 8, specs approved) | deployment, observability |

| [storefront](storefront/README.md) | Public server-rendered shop: read model, navigation, listings, search, product pages, SEO baseline (Sprint 9; design-foundation, site-chrome and storefront-settings implemented) | design-foundation, site-chrome, storefront-settings, pages-redesign, published-catalog, navigation, listings, search, product-pages, seo |

Later sprints add `orders`, `fulfilment`, `returns` and so on ([sprint plan](../roadmap/sprint-plan.md)).
