# Tags: technical plan

- **Spec:** [spec.md](spec.md) · **Module design:** [../plan.md](../plan.md) · **ADR:** [0010](../../../adr/0010-label-printing-queue-and-printer-port.md)

## Domain model
Pure, dependency-free types in `Tags/`:
- `TagData` (record): `Sku`, `Gtin`, `ProductName`, `ColourName`, `SizeName`, `Price` (decimal), `IsConcession`.
- `Ean13` (static): `Encode(gtin)` returns the 95-module bar pattern; verified against published examples.
- `TextSanitizer`: removes control characters, normalises whitespace, truncates by display width with an ellipsis.
- `ZplEscaper`: hex-escapes `^ ~ _ \` and anything outside printable ASCII/Latin-1 using the field-hex indicator, so user text cannot terminate a field or start a command.

## Application layer
| Use case | Covers |
|---|---|
| `TagDataBuilder` (assembles `TagData` from a variant, style info and outcome through ports) | TAG-1, TAG-3, TAG-4, TAG-5 |
| `ITagDocumentBuilder` (implements print-jobs' port): `BuildZpl(TagData, qty)`, `BuildPdfPreview(TagData)` | TAG-2, TAG-6, TAG-7, TAG-8 |

Both outputs come from one `TagLayout` (positions in millimetres, default 50 × 30 mm), so changing the label size is a one-file change. The PDF writer is pure Domain code (`TagPdf`, no library); Application and Domain contain no printer, file or network code (TAG-9).

## API endpoints
None of its own; the preview is served by print-jobs (`GET /api/labelling/work-items/{id}/tag-preview`) through this builder.

## Events
None.

## Data
None. Tag data is computed at print time and a snapshot of the price is stored on the print job (print-jobs).

## Frontend
No separate feature: the preview and tag layout appear inside `labelling/print-jobs`.

## Security & coupling review
| Area | How this feature meets it |
|---|---|
| Authorization | no endpoints |
| Input validation | all text passes `TextSanitizer` (printable only, length-capped); numbers are `decimal`/digits |
| Output encoding | `ZplEscaper` for every text field (TAG-6), tested with hostile strings including `^XA`, `~DG`, `^FS`, `\&`, long and Unicode text; the PDF writer draws strings as text only |
| Outbound calls | none |
| Resource limits | text length caps; one tag layout per job; PDF preview limited to one page |
| Concurrency | pure functions |
| Idempotency | same input, same output |
| Audit | n/a (the job stores the price snapshot) |
| Data protection | no personal data |
| Secrets/config | label size and logo are plain configuration |
| Error responses | build failures raise a domain error that print-jobs turns into a job failure, never an HTTP 500 with details |
| New dependencies | none: the PDF is written by hand in the Domain layer |
| Coupling | Domain and Application have no knowledge of printers, files or other features; style, colour and size names arrive through ports |
| Abuse cases | (1) a product named `^XA^HV…` tries to take over the printer: escaped; (2) a name 10,000 characters long: truncated; (3) a crafted name containing a barcode-looking string: it is drawn as text and cannot alter the barcode field |

## Tests
Unit only (no database): EAN-13 vectors, check digit, escaper with a table of hostile strings (assert the output contains no unescaped command characters), truncation, concession mark, price formatting, equality of preview and ZPL data.
