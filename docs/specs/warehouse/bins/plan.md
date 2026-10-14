# Bins: technical plan

- **Spec:** [spec.md](spec.md) · **Module design:** [../plan.md](../plan.md)

## Domain model
`Bin` (aggregate root): `Zone`, `Aisle`, `Shelf`, derived `BinCode` (`{zone}-{aisle}-{shelf}`, upper-case), `IsActive`, `LastCountedAt`. Invariants: code matches `^[A-Z0-9]{1,3}-[0-9]{1,3}-[A-Z0-9]{1,2}$` (Q-BIN-1 default); `Deactivate()` refused when the bin holds stock (checked through `IBinStockQuery`, implemented by stock-ledger); `Reactivate()` always allowed.

## Application layer
| Use case | Covers |
|---|---|
| `CreateBinCommand` (zone, aisle, shelf, acting person) | BIN-1, BIN-2, BIN-3, BIN-7 |
| `SetBinActiveCommand` (id, active, acting person) | BIN-4, BIN-7 |
| `SearchBinsQuery` (paged; filter zone; includes units on hand via one grouped query) | BIN-6 |
| `IBinReader` (published port): `FindActiveByCodeAsync`, `GetManyAsync(ids)` | BIN-5 |
| `IBinStockQuery` (port declared here, implemented by stock-ledger): units on hand per bin ids | BIN-4, BIN-6 |

Ordering note: `bins` must not depend on `stock-ledger`, but it needs on-hand per bin. The port is declared by `bins` and implemented by `stock-ledger` (the only exception to "callee declares"), so dependency stays one way: `stock-ledger` → `bins`. Also `putaway` and `adjustments` → `bins`.

## API endpoints
| Method | Route | Policy |
|---|---|---|
| GET | `/api/warehouse/bins` | Staff |
| GET | `/api/warehouse/bins/by-code/{code}` | Staff (scan lookup, light query, NFR-SC-01) |
| POST | `/api/warehouse/bins` | Staff |
| POST | `/api/warehouse/bins/{id}/active` | Staff |

## Events
None published or consumed.

## Data
`WHSE.BIN_MAIN` (`BIN_CODE` varchar(12) unique, `ZONE` varchar(3), `AISL` varchar(3), `SHLF` varchar(2), `ACT_IND`, `LAST_CNT_DTE` timestamptz null, audit columns, `xmin` token; unique `UQ_BIN_MAIN_BIN_CODE`; index on `ZONE` for the filter). Seed: none (staff create bins). `LAST_CNT_DTE` is written only by cycle-counts through a small `IBinCountMarker` port declared here.

## Frontend
Web feature `warehouse/bins` (container `BinsPageContainer`, route `/warehouse/bins`): paged table (`<th scope>`), zone filter, add-bin form with labelled inputs and inline validation, activate/deactivate button with an explanatory message when refused. NgRx store per CONVENTIONS.

## Security & coupling review
| Area | How this feature meets it |
|---|---|
| Authorization | endpoints in `MapSecuredGroup(Staff)`; roles arrive with S8 |
| Input validation | zone/aisle/shelf: upper-cased, length and charset checked server-side; acting person 1..100 chars, no control characters; page size 1..100 |
| Output encoding | codes restricted to letters, digits, hyphens; SQL parameterised, identifiers quoted |
| Outbound calls | none |
| Resource limits | paged list; rate limit (`strict`) on create |
| Concurrency | unique index on `BIN_CODE` decides concurrent creates; active toggle is a conditional update |
| Idempotency | create of an existing code is refused (BIN-2); toggling to the current state is a no-op |
| Audit | `INSR_BY/UPDT_BY` plus acting person; bins are never deleted |
| Data protection | none |
| Secrets/config | none |
| Error responses | problem details |
| New dependencies | none |
| Coupling | exposes `IBinReader`; asks for stock only through `IBinStockQuery`; no other feature's internals |
| Abuse cases | (1) two operators create `A-03-2` at once: unique index leaves one; (2) a crafted code with SQL/HTML characters is rejected by the pattern; (3) deactivating a bin while a putaway lands in it: deactivate is refused if stock is present, and putaway re-checks `IsActive` inside its transaction |

## Tests
Unit: code pattern, deactivate rule. Integration (Testcontainers): duplicate create race, deactivate refused with stock, paging order and clamp, no N+1 (statement count on a page). Architecture: bins references no other feature.
