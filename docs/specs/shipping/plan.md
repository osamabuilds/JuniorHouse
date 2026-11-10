# Shipping: technical plan (Sprint 15)

- **Specs:** [courier-port](courier-port/spec.md), [bookings](bookings/spec.md), [labels](labels/spec.md), [manual-booking-queue](manual-booking-queue/spec.md), [manifests](manifests/spec.md), [tracking](tracking/spec.md), [orders/order-history](../orders/order-history/spec.md)
- **Module:** new `Shipping` (schema `SHPG`), plus additions to `Orders.Contracts`, `Orders`, `Notifications`, Identity permissions and the admin app. ADR: [0031](../../adr/0031-shipping-module-courier-port-and-tracking.md).
- **Status:** Draft, applied by delegation while the owner is away (2026-11-09).

## Domain model (`Shipping.Domain`)
- **`Shipment`** (aggregate): one per order (unique on order id). Status: PendingBooking → Booked → PickedUp → InTransit → OutForDelivery → Delivered, or FailedAttempt, ReturnedToOrigin, Cancelled. Holds order id and number, courier, waybill (unique when set), COD amount, weight in grams, booking source (courier or manual), attempts, last error code, next attempt time, `NeedsStaff`, the manifest it is on, and `Events`.
- **`ShipmentEvent`**: courier status history (status, courier event id unique per shipment, occurred time, source push or poll).
- **`Manifest`** (aggregate): courier and business day (unique together), status Open → Matched or Mismatch (then Closed), counted parcels, lines.
- **`ManifestLine`**: shipment, handed-over flag.
- **Rules:** a shipment never moves to an earlier status (a status has a rank; failed attempt may be followed by out for delivery); a waybill is set once; a label needs a waybill; a manifest line belongs to one manifest; a closed manifest is frozen.

## Cross-module contracts
| Contract | Owner | Purpose |
|---|---|---|
| `IOrderShippingQueries.FindAsync(orderId)` | Orders.Contracts (new) | Recipient name, full address, city, phone, COD amount, order number, status code: read at booking and label time, never in an event |
| `IShipmentTrackingQueries.FindByOrderAsync(orderId)` / `FindByWaybillAsync(waybill)` | Shipping.Contracts (new) | Courier name, waybill, status timeline: statuses and times only (OHS-5, TRK-9) |
| `OrderPackedEvent` (existing) | Fulfilment | Starts a booking |
| `ShipmentStatusChangedEvent`, `ShipmentBookedEvent` | Shipping | Orders (Dispatched, Delivered), Notifications (shipped, delivery attempt failed) |
| `OrderCancelledEvent` (existing) | Orders | Cancels an unbooked or unpicked shipment |

## The port (`Shipping.Application`)
`ICourierGateway` (one per courier code, found by `ICourierRegistry`): `BookAsync(BookingRequest)` returns `Booked(waybill, labelUrl?)`, `Unreachable` or `Rejected(reason)`; `StatusAsync(waybill)`; `Parse(headers, body)` returns a verified `CourierPush` or invalid. The fake courier (`FakeCourierGateway`, Development and Testing only, ADR 0031) books in memory, has an outage switch, signs and parses pushes with the HMAC secret, and answers status from a script.

## Application layer (MediatR)
| Command / query | Covers |
|---|---|
| `CreateShipmentCommand` (from `OrderPackedEvent`), `BookShipmentCommand` (worker and staff) | BKG-1 to 9, MBQ-2, 3 |
| `EnterManualWaybillCommand`, `ListManualQueueQuery` | MBQ-1, 4 to 8 |
| `GetLabelQuery` | LBL-1 to 7 |
| `GenerateManifestCommand`, `CountManifestCommand`, `MarkHandedOverCommand`, `ListManifestsQuery`, `GetManifestQuery` | MNF-1 to 10 |
| `ApplyCourierPushCommand`, `PollShipmentsCommand`, `GetShipmentQuery` | TRK-1 to 8, 13 |
| `TrackByWaybillQuery` (public) | TRK-9, 10 |
| `CancelShipmentCommand` (from `OrderCancelledEvent`) | BKG-10 |

## API
| Method | Route | Permission |
|---|---|---|
| GET | `/api/shipping/shipments`, `/shipments/{id}` | `shipping.view` |
| GET | `/api/shipping/queue` | `shipping.view` |
| POST | `/api/shipping/shipments/{id}/book`, `/manual-waybill` | `shipping.ops` |
| GET | `/api/shipping/shipments/{id}/label` | `shipping.view` |
| GET, POST | `/api/shipping/manifests`, `/manifests/{id}`, `/manifests/generate`, `/manifests/{id}/count`, `/manifests/{id}/handover` | `shipping.view`, `shipping.ops` |
| POST | `/api/shipping/webhooks/{courier}` | anonymous: signature is the proof |
| GET | `/api/shipping/track` | anonymous: rate limited, CAPTCHA after failures, generic answers |

The existing permission `shipping.manage` (delivery fee rules, S11) is unrelated: the new codes are **`shipping.view`** and **`shipping.ops`** (ADR 0031).

## Data (`SHPG`, per [docs/db/naming.md](../../db/naming.md))
`SHPG_MAIN` (shipment), `SHPG_EVNT`, `MNFS_MAIN`, `MNFS_LINE`, lookups `SHPG_STS_LKP`, `CRER_LKP` (courier), `BKNG_SRC_LKP`, `MNFS_STS_LKP`; unique waybill (filtered), unique order id, unique `(SHPG_ID, CRER_EVNT_ID)`, unique `(CRER_ID, MNFS_DTE)`; indexes on every FK, on `(SHPG_STS_ID, NXT_ATMP_DTE)` for the worker and on status for the queue. `xmin` concurrency on both aggregates.

## Workers
Booking retry (every minute: due pending shipments), polling (courier without webhooks, in-transit shipments), end-of-day manifest. Each is a hosted service wrapping a command, so tests drive the command, not the timer.

## NFR and security
- Courier calls have a timeout and never throw (CPT-2). Webhooks: body size limit, signature checked over the raw body before parsing, constant-time compare, generic answers (TRK-2, SEC-07).
- Public tracking: rate limit (strict policy), CAPTCHA after failures like order look-up (SEC-06), generic mismatch, status timeline only (TRK-9).
- Personal data (address, phone) is read at booking and label time and is not stored in Shipping beyond what the courier call needs: Shipping stores the order id, number, waybill, COD amount and weight only. The label reads Orders again.
- Audit (NFR-SC-05): every staff action records the actor.

## Frontend (admin app, `features/shipping/...`)
`bookings` (shipment list and detail), `manual-booking-queue`, `labels` (label page), `manifests`, `tracking` (timeline component used on the shipment page); customer storefront timeline in the order screens (`orders/order-history`).

## Risks and alternatives
- Real courier formats are unknown until accounts exist: the port isolates it. The fake courier is the only adapter built.
- Alternative: Orders owns the shipment. Rejected: it mixes courier mechanics into Orders.

## Security & coupling review
| Area | How this feature meets it |
|---|---|
| Authorization | Permission per staff route on every request; the matrix is updated and tested; webhook and public tracking are on the explicit anonymous list |
| Input validation | Waybills, counts and courier codes are length and pattern checked; bodies are size limited |
| Ownership (SEC-05) | My Orders tracking is read through the caller's own order only |
| Idempotency | Events behind the inbox; bookings, pushes and manifests are idempotent |
| Data protection | Ids and codes in events; address and phone read at use; public tracking shows statuses only |
| Coupling | Contracts only; `Shipping` added to `ModuleNames` |
| Secrets | Courier webhook secrets come from configuration, never committed |

---

# COD reconciliation (Sprint 16)

- **Spec:** [cod-reconciliation](cod-reconciliation/spec.md) · **ADR:** [0032](../../adr/0032-cod-reconciliation-in-shipping.md) · **Status:** applied by delegation (2026-11-10).

## Domain model (`Shipping.Domain`)
- **`CodRemittance`** (aggregate, `RMTN_MAIN`): courier, reference (unique with courier), remitted date, stated total, recorded by, lines. Immutable once recorded.
- **`CodRemittanceLine`** (`RMTN_LINE`): waybill as stated, amount, nullable shipment id, outcome (lookup), resolution note, resolved by and at.
- **`Shipment`** gains `CodStatusId` (lookup `COD_STS_LKP`: NotCod, Awaiting, Settled, Short, Over, Resolved), `CodOverdueAt` (null or when flagged) and keeps `DeliveredAt` and `CashToCollect` from S15. Existing rows are back-filled by the migration (cash 0 = NotCod, else Awaiting).
- **`CodJudge`** (pure): `(isCod, delivered, alreadySettled, expected, remitted)` to an outcome.
- **Rules:** a line is judged once; only Matched changes a parcel to Settled; Short and Over set the parcel to Short or Over unless it is already settled; a flagged line is resolved once with a 1 to 200 character note; a resolved Short or Over parcel becomes Resolved.

## Application layer
| Command / query | Covers |
|---|---|
| `RecordCodRemittanceCommand` | COD-2 to 10, 16 |
| `ResolveCodLineCommand` | COD-12 |
| `FlagOverdueCodCommand` (daily worker) | COD-11 |
| `GetCodSummaryQuery`, `ListCodAwaitingQuery`, `ListCodExceptionsQuery`, `ListCodRemittancesQuery`, `GetCodRemittanceQuery` | COD-13, 14 |

## API
| Method | Route | Permission |
|---|---|---|
| GET | `/api/shipping/cod/summary`, `/awaiting`, `/exceptions`, `/remittances`, `/remittances/{id}` | `shipping.view` |
| POST | `/api/shipping/cod/remittances`, `/api/shipping/cod/lines/{id}/resolve` | `shipping.ops` |

Body limit 64 KB, at most 500 lines.

## Data (`SHPG`)
`RMTN_MAIN`, `RMTN_LINE`, lookups `COD_STS_LKP`, `RMTN_OTCM_LKP`; new columns `COD_STS_ID`, `COD_OVRD_DTE` on `SHPG_MAIN`; unique `(CRER_ID, REF_NO)` and unique `(RMTN_ID, WYBL_NO)` where unique per statement is not required (duplicates are lines too, so no unique); indexes on every FK, on `(COD_STS_ID, DLVR_DTE)` for the awaiting list and on `(RMTN_OTCM_ID, RSLV_DTE)` for the exceptions list. Growth: statements are small and weekly; no partitioning needed.

## Worker
`CodOverdueWorker` (hosted, hourly check, runs the command once a day by its own guard) wraps `FlagOverdueCodCommand`; tests drive the command.

## Frontend (admin app, `features/shipping/cod-reconciliation`, route `/dispatch/cod`)
`cod-page` (summary, awaiting, exceptions, statements), `remittance-form-page` (paste lines `waybill,amount`), `remittance-detail-page` (lines, resolve). NgRx store, API service, models with the statement-text parser.

## Security & coupling review
| Area | How this feature meets it |
|---|---|
| Authorization | `shipping.view` / `shipping.ops` on every route, actor from the session |
| Input validation | length, count, amount and waybill pattern; total must equal sum of lines; body size limit |
| Idempotency | unique `(courier, reference)` plus a check before saving; a race is answered from the database |
| Data protection | order number and waybill only, no customer data |
| Coupling | Shipping data only, no other module touched |
