# 0009 — GTIN-13 allocation, numbering and retirement

- Status: Proposed (approved with SCRUM-198's plan)
- Date: 2026-10-14

## Context

FR-SC-06 requires a unique GTIN-13 per style × colour × size and no duplicate assignment. NFR-SC-03 requires assignment to be strictly sequential and non-reusable: a voided number is retired, never recycled. The GS1 Pakistan company prefix has not been issued yet, and its length is not yet known.

A GTIN-13 is 12 payload digits plus a check digit. The payload is `company prefix (L digits) + item reference (12 − L digits)`. **A longer prefix leaves a shorter item reference, so less capacity**: L = 7 gives 100,000 item references, L = 8 gives 10,000, L = 9 gives 1,000. Because retired numbers are never reused, every void permanently consumes one.

## Decision

- **Numbering:** each company prefix owns a private counter row in `SKU.GTIN_NO_SEQ` (`PFX` PK, `SEQ` bigint). The next item reference is taken with the atomic upsert from ADR 0006 (`INSERT … ON CONFLICT DO UPDATE SET SEQ = SEQ + 1 RETURNING SEQ`) inside the same transaction as the assignment. Concurrent assignments serialise on that row. A transaction that rolls back also rolls the counter back, so there are no gaps and no duplicates.
- **Never reused:** the counter only moves forward and `SKU.VRNT_GTIN.GTIN` is globally unique, so a retired number can never be issued again, even by a bug that tried to. A voided GTIN row is kept with status `Retired`, the reason and the time.
- **One active GTIN per variant:** a partial unique index on `SKU.VRNT_GTIN (VRNT_ID) WHERE STS_ID = <Active>` enforces it in the database. A variant (`SKU.VRNT_MAIN`, unique on style, colour, size) can accumulate retired GTINs over time but only one active one.
- **Assigned lazily** (spec D9): the first time accepted quantity for a variant arrives.
- **Prefix is configuration**, validated at start-up: 7 to 9 digits, all digits. The restricted-circulation range 020-029 is the test prefix (spec D2); the application refuses to start in a Production environment with a prefix that starts `02`. The item-reference width is derived (`12 − L`), and the counter refuses to pass `10^(12−L) − 1` (exhaustion is an error, not a wrap-around).
- **Capacity warning:** an event-log warning and an admin banner when 80% of the item references are used.
- **Check digit:** standard GS1 modulo-10 over the 12 payload digits, a pure function in the Domain layer with unit tests against published examples.

## Consequences

- When applying to GS1 Pakistan, request the shortest prefix the budget allows (7 digits gives 100,000 variants; 8 digits only 10,000). The owner's intent, "enough capacity for many seasons", points to the shorter prefix, not the longer one.
- Switching from the test prefix to the real one: the real prefix gets its own counter row; test-prefix GTINs stay as they are and are never printed on production stock. Variants that were only ever labelled with test numbers are re-assigned by voiding.
- The counter is per prefix, so a later second prefix (e.g. after exhaustion) needs no schema change.
