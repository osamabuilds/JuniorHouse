# Tags: tasks

Ordered, test-first. Every step is done (2026-10-14). The full acceptance-criterion table is in [../traceability.md](../traceability.md).

- [x] T1. `Ean13` encoder, checked against an independent decoder. Test: `TagRenderingTests.Ean13_RoundTrips_ThroughAnIndependentDecoder`.
- [x] T2. `TextSanitizer` (control characters, whitespace, deterministic truncation, no split surrogate pairs). Test: `LongNames_AreShortenedDeterministically…`, `Truncation_NeverSplitsASurrogatePair`.
- [x] T3. `ZplEscaper` (hex-escapes every unsafe UTF-8 byte). Test: `Zpl_HostileProductNames_CannotInjectCommands`, `ZplEscaper_HexEscapesUtf8Bytes…`.
- [x] T4. `TagData` and `TagZpl` (50 × 30 mm label, native `^BE` barcode from the 12-digit payload, concession mark, copy count). Test: `Zpl_ContainsTheRequiredContent…`, `Zpl_ForConcessionStock_CarriesThe2ndMark…`.
- [x] T5. `TagPdf` preview written by hand, with valid cross-reference offsets and escaped text. Test: `Pdf_IsAWellFormedOnePageDocument…`, `Pdf_TextIsEscapedAndAscii…`.
- [x] T6. `TagDocumentBuilder` (`ITagDocumentBuilder`): identity via `IVariantReader`, names and price via `ILabelStyleLookup`, refuses a retired GTIN. Test: `TheTagBuilder_PutsTheCurrentCatalogPrice…`.
- [x] T7. Architecture rule: tag building depends on no printer, file or network code. Test: `TagBuilding_HasNoDependencyOnPrintersFilesOrTheNetwork`.
