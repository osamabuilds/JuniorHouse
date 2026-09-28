# Database naming & design conventions

These rules are mandatory. `CLAUDE.md` summarises them, and this file is the full reference and the **only** abbreviation glossary.

## 1. Naming rule

- Every identifier (schema, table, column, index, constraint, sequence) is UPPERCASE snake_case.
- Every word is **2–4 characters**, matching `[A-Z0-9]{2,4}`. Full identifier pattern: `^[A-Z0-9]{2,4}(_[A-Z0-9]{2,4})*$`.
- Take each word's abbreviation from the glossary below. If a word isn't listed, add it in the same PR. A word must never get a second abbreviation.
- Identifiers are quoted in PostgreSQL. Raw SQL must quote everything: `SELECT "PO_NO" FROM "VNDR"."PO_MAIN" WHERE "STS_ID" = 2;`

### Table name patterns

| Kind | Pattern | Example |
|---|---|---|
| Main entity | `<ENTITY>_MAIN` when the entity has child tables, otherwise `<ENTITY>` | `PO_MAIN`, `VNDR` |
| Child / detail | `<PARENT>_<CHILD>` | `PO_LINE`, `PO_AMND` |
| Lookup | `<THING>_LKP` | `PO_STS_LKP`, `SIZE_LKP` |
| Many-to-many link | `<A>_<B>_MAP` | `STYL_CLR_MAP` |
| History / version | `<ENTITY>_HIST` | `PO_LINE_HIST` |

### Column name patterns

| Kind | Pattern | Example |
|---|---|---|
| Primary key | `ID` | `ID` |
| Foreign key | `<REFERENCED_ENTITY>_ID` | `VNDR_ID`, `PO_STS_ID` |
| Boolean / indicator | `<X>_IND` | `ACT_IND` |
| Date only | `<X>_DT` | `EXPC_DLVR_DT` |
| Timestamp | `<X>_DTE` | `SENT_DTE`, `PRCS_DTE`, `INSR_DTE`, `UPDT_DTE` |
| Amount (PKR) | `<X>_AMT` | `UNIT_COST_AMT` |
| Quantity | `<X>_QTY` | `ORDR_QTY` |
| Business number | `<X>_NO` | `PO_NO` |

### Constraint & index names

| Kind | Pattern | Example |
|---|---|---|
| Primary key | `PK_<TABLE>` | `PK_PO_MAIN` |
| Foreign key | `FK_<TABLE>_<COLUMN>` | `FK_PO_MAIN_VNDR_ID` |
| Unique | `UQ_<TABLE>_<COLS>` | `UQ_PO_MAIN_PO_NO` |
| Index | `IX_<TABLE>_<COLS>` | `IX_PO_LINE_STYL_ID` |
| Check | `CK_<TABLE>_<RULE>` | `CK_PO_LINE_QTY_POS` |

PostgreSQL identifiers are limited to 63 characters, so shorten the column part if a name would exceed it.

## 2. Standard columns

**Every lookup table:**

| Column | Type | Notes |
|---|---|---|
| `ID` | `smallint` PK | Fixed values, seeded by migration |
| `CODE` | `varchar(30)` unique | Stable key referenced from code (e.g. `DRAFT`). Never renamed. |
| `NAME` | `varchar(100)` | Display name |
| `DSCR` | `varchar(500)` null | Description |
| `SORT_SEQ` | `smallint` | Display order |
| `ACT_IND` | `boolean` | Retire values instead of deleting them |

**Every transactional table:** `ID bigint GENERATED ALWAYS AS IDENTITY`, `INSR_DTE timestamptz`, `INSR_BY varchar(100)`, `UPDT_DTE timestamptz null`, `UPDT_BY varchar(100) null`, plus the PostgreSQL `xmin` system column as the concurrency token. `xmin` is PostgreSQL's own built-in column - it's the one exempt name in this whole document, since it isn't ours to rename. The naming-convention architecture test (SCRUM-170) knows about this one exception; don't add others without updating both.

Note: there is no `_AT` suffix in this convention — every timestamp column, audit or business-event, uses `_DTE` (`INSR_DTE`, `UPDT_DTE`, `SENT_DTE`, `PRCS_DTE`, …).

## 3. Design rules

1. Third normal form at minimum. Each fact is stored once.
2. Every enumerated value is a lookup table with a foreign key. No free-text status or type columns.
3. Every foreign key is declared and indexed.
4. Money is `numeric(12,2)` and quantities are `integer` (or `numeric(12,3)` where fractional). Timestamps are `timestamptz`, stored in UTC.
5. Business history is never overwritten (e.g. PO amendments, FR-SC-03). Write a version/history row instead.
6. Stock and financial changes are append-only ledgers, with adjustments recorded as new rows with a reason code (BRD §5.14).
7. No cross-schema foreign keys. Other modules' IDs are stored as plain `bigint` references, and consistency is kept through events.

## 4. Schemas

| Module | Schema |
|---|---|
| Shared reference data (sizes, colours, cities, …) | `REF` |
| Catalog / style master | `CTLG` |
| Vendor & Procurement | `VNDR` |
| Production Tracking | `PROD` |
| Quality Control | `QC` |
| SKU & Barcode | `SKU` |
| Warehouse | `WHSE` |
| Orders | `ORDR` |
| Payments | `PAYM` |
| Customers & Identity | `CUST` / `IDNT` |
| Logistics | `LOGS` |
| Returns | `RTRN` |
| Notifications | `NTFY` |
| Outbox (per module) | the module's own schema, table `OUTB_MSG` |

## 5. Abbreviation glossary

Keep this sorted alphabetically by word, and add new words when you need them.

| Word | Abbr. | | Word | Abbr. |
|---|---|---|---|---|
| acceptable | ACPT | | lease | LEAS |
| acknowledged | ACK | | level | LVL |
| active | ACT | | line | LINE |
| added (e.g. added-in revision) | ADDD | | lookup | LKP |
| address | ADDR | | main | MAIN |
| advance (e.g. advance %) | ADV | | map (link table) | MAP |
| aggregate | AGGR | | message | MSG |
| amendment | AMND | | migration (EF Core bookkeeping table) | MIG |
| amount | AMT | | milestone | MLST |
| approved | APRV | | name | NAME |
| attachment | ATCH | | next | NXT |
| attempt | ATMP | | note | NOTE |
| attribute | ATTR | | number | NO |
| barcode | BRCD | | on-quantity (vendor scorecard metric) | ONQT |
| batch | BTCH | | on-time (vendor scorecard metric) | ONTM |
| bin | BIN | | order | ORDR |
| bracket (e.g. age bracket) | BRKT | | outbox | OUTB |
| by (actor) | BY | | over (e.g. over-ship tolerance) | OVER |
| byte | BYT | | parent | PRNT |
| cancelled | CNCL | | payload | PYLD |
| category | CATG | | payment | PAYM |
| channel | CHNL | | percent | PCT |
| city | CITY | | phone | PHON |
| claim | CLM | | preferred | PREF |
| code | CODE | | price | PRIC |
| collection | COLN | | product | PRDT |
| colour | CLR | | production | PROD |
| comment | CMNT | | purchase order | PO |
| communication | COMM | | quantity | QTY |
| contact | CNTC | | rate | RATE |
| content (e.g. content type) | CNTT | | reason | RSN |
| cost | COST | | received | RCVD |
| count | CNT | | reference | REF |
| country | CTRY | | response (e.g. response date) | RSPN |
| currency | CURR | | responder | RSPR |
| customer | CUST | | responsibility | RESP |
| date (date-only) | DT | | retail | RTL |
| date/time (timestamp) | DTE | | retired (e.g. retired-in revision) | RETD |
| dead-letter | DEDL | | revision | REV |
| default | DFLT | | run (e.g. size run) | RUN |
| defect | DFCT | | sample | SMPL |
| deleted | DELD | | segment | SGMT |
| delivered / delivery | DLVR | | sent | SENT |
| description | DSCR | | sequence | SEQ |
| document | DOC | | size | SIZE |
| email | EML | | sort | SORT |
| event | EVNT | | specialisation | SPCL |
| evidence | EVDN | | status | STS |
| expected | EXPC | | stock | STCK |
| expiry | EXPY | | storage | STOR |
| fabric | FBRC | | style | STYL |
| file | FILE | | target | TGT |
| gender | GNDR | | tech pack | TCPK |
| goods receipt note | GRN | | terms | TERM |
| history | HIST | | tolerance | TOL |
| identifier | ID | | total | TOT |
| impact | IMPC | | type | TYP |
| inbox | INBX | | under (e.g. under-ship tolerance) | UNDR |
| indicator | IND | | unit | UNIT |
| initiator | INIT | | updated | UPDT |
| insert | INSR | | user | USR |
| item | ITEM | | variant | VRNT |
| key | KEY | | vendor | VNDR |
| label | LBL | | version | VER |
| latest | LATE | | warehouse | WHSE |
| lead time | LEAD | | year | YR |
