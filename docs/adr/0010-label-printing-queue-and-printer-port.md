# 0010 — Label printing: persistent queue and printer port

- Status: Accepted (implemented in SCRUM-198)
- Date: 2026-10-14

## Context

NFR-SC-02: label printers must use a queued, retry-on-jam model, and one printer fault must not halt labelling or packing work for other items in the queue. The printer hardware is not chosen (spec D7), so the code must work today and not change when the printer arrives. S14/S15 will reuse the same machinery for pack-station and shipping labels.

## Decision

- **Persistent queue in PostgreSQL.** A print job is a row (`SKU.PRT_JOB`) with a status (Queued, Printing, Printed, Failed, Cancelled), attempt count and a `next attempt` time. Creating a job never talks to the printer, so the user action cannot fail because a printer is down.
- **A background worker** (a hosted service in the Labelling module) claims due jobs with `SELECT … FOR UPDATE SKIP LOCKED`, one at a time per printer, and prints them through the port. Printers are independent: a jam on printer A leaves printer B's jobs untouched.
- **Retry with backoff, no head-of-line blocking.** A failed attempt records an `SKU.PRT_JOB_ATMP` row, then re-queues the job with a later `next attempt` time (30 s, 2 min, 10 min); the worker moves on to the next due job. After the configured attempt limit (default 4) the job becomes Failed and shows the last error. Staff can Retry (attempt counter reset, same job row) or Cancel.
- **Stuck jobs recover.** A job stuck in Printing longer than a timeout (worker crash) is returned to Queued.
- **Printer port:** `ILabelPrinter` in `Application/Abstractions` takes a rendered `LabelDocument` (format + bytes) for a named printer and returns success or a typed failure (offline, jam, rejected). Implementations: `PdfPreviewLabelPrinter` (writes a PDF to `IFileStorage`, used in dev, tests and as the preview in the UI) now; `ZplLabelPrinter` (ZPL over TCP/9100 or a spooler) as the first real format. TSC TSPL can be added later as another renderer without touching the queue.
- **Rendering is separate from sending.** `TagZpl` and `TagPdf` (pure Domain code) turn tag data into ZPL text (EAN-13 is a native ZPL command, `^BE`, so the printer draws the barcode) or into a PDF for preview. The PDF preview draws the EAN-13 from an in-house encoder (a small, fully testable function) so no barcode library is needed. PDF output needs one new package, **PDFsharp** (MIT), added in `Directory.Packages.props` only.
- **Prices are snapshotted** onto the job when it is created, so a reprint of an old job shows what was printed and a new job shows the current price.
- **Idempotent worker steps:** a job's status transition (Queued to Printing to Printed) is a single conditional `UPDATE … WHERE STS_ID = …`, so two workers can never print the same job twice.

## Consequences

- Printing a tag is "at-least-once" if the worker dies exactly between a successful print and recording it (one duplicate tag at worst); this is acceptable for labels and is why reprints are free of side effects.
- The queue, worker and port are generic. S14/S15 can reuse them for shipping labels by adding job kinds, with no changes to the retry logic.
- **Built without a PDF library (2026-10-14).** The preview is a one-page PDF written by hand in the Domain layer (`TagPdf`): a tag needs only rectangles and two built-in fonts, so no package was added, which removes a dependency from the supply chain. The tests check the cross-reference offsets and that text is escaped.
