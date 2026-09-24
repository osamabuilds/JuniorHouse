# 0006 — Business document number generation (PO, and future GRN/RMA/waybill numbers)

- Status: Accepted
- Date: 2026-09-25

## Context

FR-SC-02 requires every Purchase Order to have a "PO number." The BRD gives no format. Later sprints will need the same kind of human-facing sequential identifier for other business documents — GRNs (S4), RMAs (S16), possibly waybills (S15) — so this is a pattern worth deciding once rather than inventing per-module ad hoc.

Two failure modes to avoid:
1. **Not meaningful.** A raw surrogate `ID` (`47`) or a GUID tells a human nothing and doesn't sort usefully next to "when was this raised."
2. **Not safe under concurrency.** A naive "read the max, add one" in application code races under concurrent creation and can assign duplicate numbers.

## Decision

- **Format:** `{PREFIX}-{YYYY}-{NNNNN}` — e.g. `PO-2026-00001`. Four-digit calendar year, five-digit zero-padded sequence, sequence resets each year. Meaningful (year visible at a glance), sortable, unique.
- **Generation:** each module that needs one owns a small private counter table, e.g. `VNDR.PO_NO_SEQ` (`YR` smallint PK, `SEQ` integer not null default 0). Allocation is a single atomic statement in the same transaction as the business insert:
  ```sql
  INSERT INTO "VNDR"."PO_NO_SEQ" ("YR", "SEQ") VALUES ($1, 1)
  ON CONFLICT ("YR") DO UPDATE SET "SEQ" = "VNDR"."PO_NO_SEQ"."SEQ" + 1
  RETURNING "SEQ";
  ```
  This relies on PostgreSQL's row-level locking on the upsert target row, so two concurrent transactions can never read and return the same `SEQ` value — the second waits for the first to commit or roll back.
- **No shared cross-module sequence table.** Each module's counter is private to that module's schema (no `REF.BUS_NO_SEQ` shared table), consistent with ADR 0002 — a module never reads or writes another module's tables. The *pattern* (format + atomic-upsert technique) is shared by convention, documented here and in `docs/db/naming.md`; the physical table is not.
- The generated number is stored in its own unique-constrained column (e.g. `PO_MAIN.PO_NO`), separate from the surrogate `ID` primary key, per `docs/db/naming.md`'s rule that real-world identifiers get their own column.

## Consequences

- Safe under concurrent creation without an advisory lock or `SERIALIZABLE` isolation — an `ON CONFLICT ... DO UPDATE` upsert is enough.
- A gap can still occur if a transaction allocates a number and then rolls back for an unrelated reason (e.g. a later validation failure in the same transaction) — the sequence value is consumed and not reused. This is acceptable: the format promises uniqueness and rough chronological order, not gaplessness.
- Every future module adopting this pattern (GRN, RMA, waybill numbers) copies the same shape: its own `<PREFIX>_NO_SEQ` table, its own prefix, same atomic upsert. No new ADR needed for those unless the pattern itself needs to change.
