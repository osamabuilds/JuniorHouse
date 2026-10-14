# Romp constitution: one hierarchy, everywhere

The same two names, **module** then **feature**, identify a piece of functionality in every part of the repository: its spec, its API code, its tests, its web screens and its end-to-end tests. If you know the module and the feature, you know the path in every tree. If a path breaks this rule, it is a bug.

Detailed code-level conventions live in [`CONVENTIONS.md`](CONVENTIONS.md) and database naming in [`db/naming.md`](db/naming.md). When they conflict with this document, this document wins for *where things live*; they win for *how code inside is written*.

## 1. The hierarchy

```
<module>/<feature>
```

- A **module** is a business area with its own database schema, one backend module and one web area (ADR 0002). Module names are single lowercase words.
- A **feature** is one piece of functionality a user or another module can name ("purchase orders", "inspections", "print jobs"). Feature names are plural nouns or noun phrases.
- **Nothing is named after a ticket, sprint or date.** Jira keys live inside the spec header (`Jira:`), commits and branches, never in a directory or file name.

### Modules

| Module | Business area | Schema | Backend project family |
|---|---|---|---|
| `reference` | Shared lookup data (sizes, colours, fabrics, …) | `REF` | `Romp.Modules.Reference.*` |
| `catalog` | Styles and (later) products | `CTLG` | `Romp.Modules.Catalog.*` |
| `vendor` | Vendors, purchase orders and amendments | `VNDR` | `Romp.Modules.Vendor.*` |
| `production` | Production runs, milestones, delivery notes | `PROD` | `Romp.Modules.Production.*` |
| `quality` | Goods receipt and quality control | `QC` | `Romp.Modules.Quality.*` |
| `labelling` | SKUs, GTIN barcodes and tag printing | `SKU` | `Romp.Modules.Labelling.*` |

A new module needs an ADR, a row here, a row in `docs/db/naming.md` §4, an entry in `ModuleNames` (architecture tests) and a row in [`specs/README.md`](specs/README.md).

### Where a feature lives

| Tree | Path for module `vendor`, feature `purchase-orders` | Case |
|---|---|---|
| Spec | `docs/specs/vendor/purchase-orders/` | kebab-case |
| API domain / application / infrastructure | `src/api/src/Modules/Vendor/Romp.Modules.Vendor.{Domain,Application,Infrastructure}/PurchaseOrders/` | PascalCase, namespace = path |
| API contracts (public, cross-module) | `Romp.Modules.Vendor.Contracts` (flat, by purpose; not per feature) | |
| API tests | `src/api/tests/Romp.Modules.Vendor.Tests/PurchaseOrders/` | PascalCase |
| Web | `src/web/projects/admin/src/app/features/vendor/purchase-orders/` | kebab-case |
| Web unit tests | beside the file they test, in that web folder | |
| End-to-end | `src/web/e2e/vendor/purchase-orders.e2e.spec.ts`, or `purchase-orders.<topic>.e2e.spec.ts` for extra files of the same feature | kebab-case |

`PurchaseOrders` and `purchase-orders` are the same name in two casings; the architecture tests convert between them.

Cross-module user journeys (a flow that crosses several modules) live in `src/web/e2e/journeys/` and are named for the journey (`receive-to-accept.e2e.spec.ts`). Journeys are the only e2e files allowed outside a module folder.

## 2. Specs

```
docs/specs/
  README.md                       index of modules and features
  _template/                      copy this to start a feature
  <module>/
    README.md                     what the module is for, feature table, module-wide decisions
    <feature>/
      README.md                   feature card: purpose, status, where the code and tests are
      spec.md                     WHAT and WHY: requirements, flow, acceptance criteria, decisions
      plan.md                     HOW (tech): domain, endpoints, events, data, NFR design
      tasks.md                    ordered, test-named steps
      traceability.md             requirement → test mapping (optional)
    plan.md                       optional module-wide design shared by the features (layout, contracts, data overview)
    _sprint-specs/<capability>/   legacy: specs written per sprint that cover several features
```

Rules:

1. **One spec per feature.** A sprint that delivers several features of one module writes a spec in each feature folder. Decisions shared by several features go in the module `README.md` under "Module decisions" and are linked, not copied.
2. **Every feature folder has a `README.md` card**, even when its spec still lives in a legacy `_sprint-specs` folder. The card links to that spec and names the sections that apply.
3. **`_sprint-specs/` is closed.** Nothing new is added to it. Features are moved out of it into their own folders when they are next touched (the spec must be updated in the same change anyway, workflow step 5).
4. A feature's spec, plan and tasks are written in the order given in `CLAUDE.md` (spec, plan, tasks; stop for approval at each).
5. Requirement IDs (`FR-…`, `NFR-…`, …) appear unchanged and are the only cross-reference between specs and tests.

## 3. Backend

- Feature folders exist in **every layer the feature touches**, with the same name: `Domain/PurchaseOrders`, `Application/PurchaseOrders`, `Infrastructure/PurchaseOrders`, `Tests/PurchaseOrders`. Namespaces equal the path.
- A feature folder never holds another feature's files. Two features that need the same code put it in `Abstractions/` (ports) or the module's `Contracts` (public).
- Module-internal rules (no module references another's internals) are in ADR 0002 and enforced by `Romp.ArchitectureTests`.

## 4. Web

```
features/<module>/<feature>/
  containers/ components/ forms/ models/ services/ store/ utils/
  <feature>.constants.ts   <feature>.routes.ts   <feature>.providers.ts   index.ts
```

- A web feature folder **must match a backend feature** of the same module. The top-level files (`<feature>.routes.ts`, `.constants.ts`, `.providers.ts`) follow the folder name. Files inside `store/`, `utils/` and component folders may keep a historic prefix (for example `reference-data.*` inside `lookups`) until they are next touched; new files use the feature name.
- A backend feature may not need its own screens. Then its card says which web feature **hosts** it (for example, "Revisions" is hosted by `vendor/purchase-orders`). A web feature that hosts others lists them in its card. When a hosted feature grows its own pages, it gets its own folder.
- Imports go through the feature barrel: `@features/<module>/<feature>`. Features may import `core`, `shared` and another feature's barrel; never the reverse of that direction (see `CONVENTIONS.md`).
- Browser URLs are an interface and do not have to equal folder names; they are defined in the feature's `<feature>.routes.ts`.

## 5. Enforcement

`Romp.ArchitectureTests/StructureSyncTests` fails the build when:

1. a backend feature folder has no spec folder (with a `README.md`) under `docs/specs/<module>/`;
2. a spec feature folder has no backend feature of that name (a card whose status is `**Status:** Planned` is excluded until the feature is built);
3. a web feature folder has no matching backend feature in the same module;
4. an e2e spec sits outside `e2e/<module>/` or `e2e/journeys/`, or its file name does not start with a feature of that module;
5. a spec, web or e2e file or folder name contains a Jira key (`SCRUM-`) or `sprint`, except under `_sprint-specs/`;
6. an API test folder (`Romp.Modules.<Module>.Tests/<Feature>`) has no API feature of that name.

The test reads directories, not code, so it runs in seconds and needs no database.

## 6. Architecture principles

Every feature plan is reviewed against these. The detail and the security checklist are in [ADR 0011](adr/0011-security-and-coupling-baseline.md); module boundaries are in [ADR 0002](adr/0002-modular-monolith.md).

**Loose coupling**
1. Modules talk only through `*.Contracts` (query interfaces returning DTOs) and versioned integration events. Never through another module's tables, entities or internal types.
2. Events are consumed through a mapper in the consumer's own `Application` project, so a contract change touches one file.
3. Dependencies point inward: `Domain` depends on nothing but building blocks; `Application` depends on `Domain` and its own ports (`Abstractions/`); `Infrastructure` implements the ports. Hardware, files, clocks and other systems are always behind a port with a fake.
4. A feature is replaceable: its public surface is its contracts and endpoints, nothing else. A feature folder never imports another feature's internals; shared code goes in `Abstractions/` or a building block.

**Understandable**
5. One concept per file; names say what the thing does; a reader can go from a spec card to the code by path alone (section 1).
6. Business rules live in the Domain layer and are tested there. Endpoints only translate HTTP to a command or query.

**Secure by construction**
7. Every endpoint declares an authorization policy; the host will not start in production without real authentication (the deployment gate, ADR 0011).
8. Every plan contains a "Security & coupling review" table (template). A plan without one is not approved.
9. Rules that can be enforced by a database constraint, a type or a test are enforced there, not by convention.

## 7. Changing this document

Change the rules first, in this file, in a PR of its own. Then move code and specs to match. An exception is written here, with a reason and an end date, never in someone's head.
