# Labelling: module design

- **Module README:** [README.md](README.md) (decisions D1-D12, BRD text, scope)
- **Features:** [label-work](label-work/plan.md) · [variants](variants/plan.md) · [print-jobs](print-jobs/plan.md) · [tags](tags/plan.md)
- **ADRs:** [0009 GTIN](../../adr/0009-gtin-allocation-and-numbering.md) · [0010 print queue](../../adr/0010-label-printing-queue-and-printer-port.md) · [0011 security and coupling](../../adr/0011-security-and-coupling-baseline.md) · numbering reuses [0006](../../adr/0006-business-number-generation.md)

This file holds what is shared by the four features. Each feature plan holds its own domain model, use cases, endpoints and security review.

## Module layout

`Romp.Modules.Labelling.{Domain,Application,Infrastructure,Contracts}`, tests `Romp.Modules.Labelling.Tests`. `LabellingModule : IModule`, DbContext `SkuDbContext` (schema `SKU`), outbox `AddOutboxModule("SKU", …)`, inbox `SKU.INBX`. Add `Labelling` to `ModuleNames` in `ModuleBoundaryTests`, to the `modules` array and MediatR assembly list in `Program.cs`, and to the tests project list.

| Feature (same name in every tree) | API folder | Web feature |
|---|---|---|
| label-work | `LabelWork/` | `labelling/label-work` |
| variants | `Variants/` | `labelling/variants` |
| print-jobs | `PrintJobs/` | `labelling/print-jobs` |
| tags | `Tags/` | hosted by `labelling/print-jobs` (tag preview) |

Domain folders hold: `Variants/Gtins` (GTIN value object, check digit, prefix rules), `Tags/Ean13` (encoder), everything else per feature plan.

## How the features depend on each other (loose coupling inside the module)

```
 QC event ──► label-work ──(AssignGtinsForLines port)──► variants
                  │                                          ▲
                  └──(QueuePrintJob)──► print-jobs ──(ITagDocumentBuilder port)──► tags
                                            │
                                            └──(ILabelPrinter port)──► ZPL / PDF-preview implementations
```

- Features talk to each other through small interfaces declared by the **caller** in its own `Application/<Feature>/` folder and implemented by the callee (`IVariantAssigner` implemented in `Variants`, `ITagDocumentBuilder` implemented in `Tags`). No feature references another feature's entities or handlers.
- Direction is one way: `label-work` → `variants`, `print-jobs` → `variants` and `tags`. `variants` and `tags` know nothing about the others (an architecture test checks it).

## Cross-module reads (all additive, all in `*.Contracts`)

| Need | Source | Change |
|---|---|---|
| Style id for a PO | `Vendor.Contracts.IPurchaseOrderQueries.GetTermsAtAsync(poId, today)` | none (`PoInForceTerms.StyleId` exists; one style per PO, D11) |
| Style code, name, target price, price-changed time, active flag | `Catalog.Contracts.IStyleQueries` | **new** `GetLabelInfoAsync(IReadOnlyCollection<long> styleIds)` → `StyleLabelInfo(Id, Code, Name, TargetRetailPrice, TargetPriceChangedAt, IsActive)`; one call per page (no N+1) |
| Colour and size codes and names | `Reference.Contracts` | **new** `IApparelCodeQueries.GetColourCodesAsync(ids)` / `GetSizeCodesAsync(ids)` → `id → (Code, Name)` |
| Accepted batches | event `QcBatchAcceptedEvent` (`Quality.Contracts`) | none |

The module reads no other module's tables. Consumed events pass through `LabelWork/QcBatchAcceptedMapper` (anti-corruption layer): it is the only file that knows the shape of QC's event.

## Events

| Event | Published by | Consumed by | Via outbox? |
|---|---|---|---|
| `QcBatchAcceptedEvent` (existing v1) | QC | label-work | yes (existing) |
| `VariantGtinAssignedEvent` v1 | variants | S6/S7 later | yes |
| `GtinVoidedEvent` v1 | variants | S6/S7 later | yes |
| `TagJobPrintedEvent` v1 | print-jobs | none yet | yes |

Payloads carry no notes, staff names or personal data. Records live in `Labelling.Contracts` with the same envelope as `QualityEvent`.

## Data (schema `SKU`, all names per `docs/db/naming.md`)

Tables are owned by one feature each; the owner is in the right-hand column. Full column lists are in the feature plans.

| Table | Owner |
|---|---|
| `LBL_WORK_ITEM`, `LBL_WORK_STS_LKP`, `LBL_OUTC_LKP` | label-work |
| `VRNT_MAIN`, `VRNT_GTIN`, `GTIN_NO_SEQ`, `GTIN_STS_LKP`, `VOID_RSN_LKP` | variants |
| `PRT_JOB`, `PRT_JOB_ATMP`, `PRTR_MAIN`, `JOB_NO_SEQ`, `PRT_JOB_STS_LKP`, `PRT_JOB_KIND_LKP`, `RPRT_RSN_LKP`, `PRT_FMT_LKP` | print-jobs |
| `OUTB_MSG`, `INBX` | module |

No foreign key crosses a feature's ownership except `PRT_JOB → VRNT_GTIN` and `LBL_WORK_ITEM → VRNT_MAIN` (ids only, resolved through the port; the FK is declared for integrity, the code does not navigate it).

New glossary words, added to `docs/db/naming.md` in the same PR: company `CMPY`, prefix `PFX`, print `PRT`, printer `PRTR`, job `JOB`, reprint `RPRT`, void `VOID`, retry `RTRY`, tag `TAG`, queue `QUE`, snapshot `SNAP`, work `WORK`, resolve `RSLV`, parked `PARK`, capacity `CAP`. (`PRNT` is "parent", so print is `PRT`.)

Catalog change (additive migration): `CTLG.STYL_MAIN.TGT_PRIC_CHG_DTE timestamptz`, set only when the target retail price changes; backfilled with `INSR_DTE`.

## Cross-cutting prerequisite group (ADR 0011, `Romp.Api` host)

Done first in the sprint, reviewed on its own, with tests. It is not Labelling code but Labelling cannot ship securely without it.

1. `MapSecuredGroup(policy)` helper and `Policies` constants; existing modules' endpoint groups switch to it (mechanical).
2. Development-only allow-all handler for `Policies.Staff` and `Policies.Admin`, registered only in `Development` and `Testing`.
3. **Deployment gate:** startup fails outside `Development`/`Testing` unless a real authentication scheme is registered.
4. Rate limiter (default fixed window plus a strict policy), explicit Kestrel body-size limit, security headers middleware.
5. `/api/admin/outbox` moved into the `Admin` policy group.
6. Architecture test: every mapped endpoint belongs to a group with a policy.

## Module-wide security & coupling review

| Area | How the module meets it |
|---|---|
| Authorization | all endpoints in `MapSecuredGroup(Policies.Staff)`; void, reprint and printer administration are `Policies.Admin`-ready (declared now, enforced with S8) |
| Input | FluentValidation on every command (lengths, ranges, charset); request records are separate from entities |
| Output encoding | ZPL text escaped with hex escapes; PDF text drawn as text, never interpreted; SQL parameterised and identifiers quoted |
| Outbound calls | the only outbound connection is to a printer address read from `PRTR_MAIN`, which no endpoint can change; host:port allow-list and timeouts |
| Resource limits | max tags per job, max queued jobs per printer, page size 1..100, rate limit on preview and queue endpoints |
| Concurrency | unique and partial unique indexes, conditional updates, `FOR UPDATE SKIP LOCKED`, concurrency tokens |
| Idempotency | inbox on `QcBatchAcceptedEvent`; unique key per work item |
| Audit | acting person + system actor + time + reason on every assignment, print, reprint, void, in append-only rows |
| Data | no personal data in this module; money `decimal` |
| Secrets/config | test GTIN prefix rejected in Production; printer addresses are configuration, not secrets |
| Errors | problem details only |
| Dependencies | one new package: PDFsharp (MIT), pinned in `Directory.Packages.props` |
| Coupling | contracts and events only; anti-corruption mapper; ports with fakes; one-way dependencies between features |

## Risks

- GTIN capacity falls as the company prefix grows longer (7 digits = 100,000, 8 = 10,000): ADR 0009.
- Labelling integration tests cannot load locally built DLLs on this machine (Application Control); run them in the Linux SDK container or CI and say so.
