# Variants: technical plan

- **Spec:** [spec.md](spec.md) · **Module design:** [../plan.md](../plan.md) · **ADR:** [0009](../../../adr/0009-gtin-allocation-and-numbering.md)

## Domain model
- `Variant` (aggregate root): `StyleId`, `ColourId`, `SizeId`, `SkuCode`; owns `VariantGtin` rows. Invariants: unique (style, colour, size); at most one Active GTIN; `AssignGtin(itemRef)` only when none is Active; `Void(reasonCode, person)` marks the Active GTIN Retired and never reissues it.
- `Gtin` (value object, `Variants/Gtins`): 13 digits, valid GS1 mod-10 check digit; built from `CompanyPrefix` + `ItemReference`; parsing validates length, digits and check digit (VAR-14).
- `CompanyPrefix` (value object): 7-9 digits; test prefix recognised by leading `02`; `Capacity = 10^(12 − length)`.

## Application layer
| Use case | Covers |
|---|---|
| `AssignGtinsForLinesCommand` (implements port `IVariantAssigner`, runs inside the caller's transaction) | VAR-1..7, VAR-9, VAR-10 |
| `VoidGtinCommand` (variantId, reason code, acting person) | VAR-8, VAR-10 |
| `SearchVariantsQuery` (paged), `GetVariantQuery` | VAR-13 |
| `GetGtinCapacityQuery` | VAR-12 |
| `IGtinSequence` port: atomic next item reference (ADR 0006 upsert) | VAR-4, VAR-6 |
| `LabellingOptionsValidator` (`ValidateOnStart`) | VAR-11 |

## API endpoints
| Method | Route | Policy |
|---|---|---|
| GET | `/api/labelling/variants` | Staff |
| GET | `/api/labelling/variants/{id}` | Staff |
| POST | `/api/labelling/variants/{id}/void` | Admin-ready (Staff until S8) |
| GET | `/api/labelling/capacity` | Staff |

Assignment has no public endpoint: only the in-process port, called by `label-work`.

## Events
Publishes `VariantGtinAssignedEvent` v1 and `GtinVoidedEvent` v1 (outbox, same transaction). Consumes none.

## Data
`SKU.VRNT_MAIN` (unique `STYL_ID`,`CLR_ID`,`SIZE_ID`; unique `SKU_CODE`), `SKU.VRNT_GTIN` (unique `GTIN`; unique `PFX`,`ITEM_REF`; **partial unique** on `VRNT_ID` where status is Active; FK indexes; `RETD_*` columns), `SKU.GTIN_NO_SEQ`, lookups `GTIN_STS_LKP`, `VOID_RSN_LKP`. Audit columns and concurrency token. Raw SQL for the counter quotes every identifier.

## Frontend
Web feature `labelling/variants` (container `SkuRegisterPageContainer`, route `/labelling/register`): paged table, search box with a label, Void dialog (reason select, acting person, confirm), capacity banner.

## Security & coupling review
| Area | How this feature meets it |
|---|---|
| Authorization | endpoints in `MapSecuredGroup`; void declared `Admin`-ready; assignment not reachable over HTTP at all |
| Input validation | void: reason from the lookup, acting person 1..100 chars without control characters; search text ≤ 50 chars, parameterised; GTIN parsing strict (VAR-14) |
| Output encoding | SKU and GTIN are digits/uppercase letters/hyphens only, validated on creation; SQL parameterised, identifiers quoted |
| Outbound calls | none |
| Resource limits | page size 1..100; rate limit on void; a message can request at most 500 assignments |
| Concurrency | counter upsert row lock; unique and partial unique indexes make duplicates impossible even if code is wrong; void is a conditional update on status |
| Idempotency | assignment of an already-active variant is a no-op that returns the existing GTIN |
| Audit | void keeps reason, person, time forever; GTIN rows are never deleted or reused |
| Data protection | events carry ids, SKU and GTIN only |
| Secrets/config | test prefix refused in Production at startup; prefix length validated |
| Error responses | problem details; capacity exhaustion is a domain error with a fixed message |
| New dependencies | none |
| Coupling | knows nothing about label-work, print-jobs or QC; style/colour/size codes come through `ILabelStyleLookup` and `Reference.Contracts`; events are versioned and additive |
| Abuse cases | (1) two operators void and re-assign the same variant at once: the partial unique index and conditional update leave exactly one Active GTIN; (2) a caller feeds a forged GTIN string to reprint or void: parsing and lookup reject it; (3) someone sets the test prefix in production: startup refuses |

## Tests
Unit: check-digit vectors from GS1 examples, prefix and capacity rules, void state machine. Integration (Testcontainers): 50 parallel assignments produce 50 distinct GTINs; void then assign never reuses; rollback leaves the counter unchanged; partial unique index rejects a second Active row.
