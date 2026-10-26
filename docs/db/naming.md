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
| Customers (storefront accounts) | `CUST` |
| Identity (staff accounts, sessions, roles, audit trail) | `IDNT` |
| Storefront (settings and read model) | `STFR` |
| Logistics | `LOGS` |
| Returns | `RTRN` |
| Notifications | `NTFY` |
| Outbox (per module) | the module's own schema, table `OUTB_MSG` |
| Inbox (per module, once a module registers a handler with a DB effect) | the module's own schema, table `INBX`, composite PK (`MSG_ID`, `HNDL_NAME`) |

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
| after (e.g. value after) | AFT | | | |
| aggregate | AGGR | | message | MSG |
| amendment | AMND | | migration (EF Core bookkeeping table) | MIG |
| amount | AMT | | milestone | MLST |
| approved | APRV | | name | NAME |
| attachment | ATCH | | next | NXT |
| attempt | ATMP | | note | NOTE |
| attribute | ATTR | | number | NO |
| barcode | BRCD | | on-quantity (vendor scorecard metric) | ONQT |
| batch | BTCH | | on-time (vendor scorecard metric) | ONTM |
| before (e.g. value before) | BEF | | | |
| beyond (e.g. beyond latest acceptable date) | BYND | | | |
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
| | | | process (e.g. processed date) | PROC |
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
| day | DAY | | | |
| dead-letter | DEDL | | revision | REV |
| default | DFLT | | run (e.g. size run) | RUN |
| defect | DFCT | | sample | SMPL |
| deleted | DELD | | segment | SGMT |
| delivered / delivery | DLVR | | sent | SENT |
| description | DSCR | | sequence | SEQ |
| difference | DIFF | | shift (e.g. date shift) | SHFT |
| document | DOC | | size | SIZE |
| email | EML | | sort | SORT |
| event | EVNT | | specialisation | SPCL |
| evidence | EVDN | | status | STS |
| expected | EXPC | | stock | STCK |
| expiry | EXPY | | storage | STOR |
| fabric | FBRC | | style | STYL |
| file | FILE | | target | TGT |
| from (e.g. from-status) | FROM | | | |
| gender | GNDR | | tech pack | TCPK |
| goods receipt note | GRN | | terms | TERM |
| | | | to (e.g. to-status) | TO |
| handler | HNDL | | | |
| history | HIST | | tolerance | TOL |
| identifier | ID | | total | TOT |
| impact | IMPC | | type | TYP |
| inbox | INBX | | under (e.g. under-ship tolerance) | UNDR |
| indicator | IND | | unit | UNIT |
| initiator | INIT | | updated | UPDT |
| insert | INSR | | user | USR |
| item | ITEM | | value | VAL |
| | | | variant | VRNT |
| key | KEY | | vendor | VNDR |
| label | LBL | | version | VER |
| | | | visible (e.g. vendor-visible) | VSBL |
| latest | LATE | | warehouse | WHSE |
| lead time | LEAD | | year | YR |

### Added in Sprint 3 (SCRUM-183, Production Tracking)

| Word | Abbr. | | Word | Abbr. |
|---|---|---|---|---|
| acting (the person acting) | ACTR | | new | NEW |
| change | CHNG | | old | OLD |
| completion | CMPL | | pre-production | PP |
| construction | CNST | | recommended | RECM |
| cutting | CUT | | recorded | RCRD |
| decision | DCSN | | reporter | RPTR |
| delay | DLAY | | resample | RSMP |
| delivery note | DN | | Romp (the company) | ROMP |
| dispatch | DSPT | | round | RND |
| final | FNL | | shipment | SHIP |
| finished | FNSH | | short (short of the ordered quantity) | SHRT |
| flag | FLAG | | start / started | STRT |
| | | | submitted | SUBM |
| | | | superseded | SUPR |
| | | | timing | TMNG |
| | | | without | WOUT |

### Added in Sprint 4 (SCRUM-197, Receiving and Quality Control)

| Word | Abbr. | | Word | Abbr. |
|---|---|---|---|---|
| acceptable quality level | AQL | | quality control | QC |
| check | CHK | | reconciliation (claimed vs counted) | RCNL |
| counted | CNTD | | reject | RJCT |
| excess | EXCS | | result | RSLT |
| inbound | INBD | | returned | RTRN |
| inspection | INSP | | rework | RWRK |
| letter | LTR | | safety | SAFE |
| limit | LIMT | | severity | SVRT |
| lot | LOT | | split (sub-batch) | SPLT |
| maximum | MAX | | | |
| minimum | MIN | | | |
| mode | MODE | | | |
| outcome | OTCM | | | |
| scorecard (vendor scorecard facts) | SCRD | | | |

### Added in Sprint 5 (SCRUM-198, Labelling)

`PRNT` is already "parent", so "print" is `PRT`. `ACT` is already "active", so "manual action by" is written `MNL_BY`.

| Word | Abbr. | | Word | Abbr. |
|---|---|---|---|---|
| error (error text) | ERR | | printer | PRTR |
| format | FMT | | reprint | RPRT |
| GS1 trade item number | GTIN | | request / requested | REQ |
| job | JOB | | snapshot | SNAP |
| kind | KIND | | stock keeping unit | SKU |
| manual | MNL | | sub (as in sub-batch) | SUB |
| output | OUTP | | void | VOID |
| parked | PARK | | work (work item) | WORK |
| prefix | PFX | | | |
| print | PRT | | | |

### Added in Sprint 6 (SCRUM-200, Warehouse)

`STCK` is already "stock" and `CNT` is "count", so a cycle count's tables are `CNT_*`. `ACPT` is "acceptable", so the accepted grade is a lookup row, never a column name.

| Word | Abbr. | | Word | Abbr. |
|---|---|---|---|---|
| adjustment | ADJ | | pending | PEND |
| aisle | AISL | | putaway | PUT |
| alert | ALRT | | reconfirmed | RCNF |
| awaiting | AWTG | | scanned | SCAN |
| balance | BAL | | shelf | SHLF |
| closed | CLSD | | system (as in system quantity) | SYS |
| confirmed | CNFM | | threshold | THRS |
| cycle | CYC | | variance | VARN |
| grade | GRD | | zone | ZONE |
| idempotency | IDEM | | | |
| large | LRG | | | |
| last | LAST | | | |
| mark (as in movement mark) | MARK | | | |
| movement | MVMT | | | |
| open | OPEN | | | |
| created | CRTD | | | |

### Added in Sprint 7 (SCRUM-201, Catalog / Publish)

`PRIC`, `PRDT`, `COLN`, `CLR`, `PCT`, `BYT`, `CNTT`, `HIST`, `MIN`, `MAX`, `MAP`, `VRNT` and `LBL` already existed. Only the words below are new.

| Word | Abbr. | | Word | Abbr. |
|---|---|---|---|---|
| age | AGE | | instruction | INST |
| alt text | ALT | | kilogram | KG |
| archived | ARCH | | length | LEN |
| care | CARE | | month | MON |
| centimetre | CM | | original | ORIG |
| chart | CHRT | | planned | PLAN |
| chest | CHST | | published | PUBL |
| compare (compare-at price) | CMPR | | redirect | RDIR |
| composition | CMPS | | slug | SLUG |
| fibre | FIBR | | symbol | SYMB |
| height | HGHT | | waist | WAIS |
| image | IMG | | weight | WGHT |
| | | | width | WDTH |

### Added in Sprint 9 (SCRUM-206, Storefront settings)

`AGE`, `ITEM`, `KIND`, `KEY`, `SLUG`, `RDIR`, `PUBL`, `SNAP`, `TGT`, `DLVR`, `DAY`, `MIN`, `MAX`, `NOTE`, `DOC`, `VER`, `NO`, `LVL`, `LBL`, `PRNT`, `CITY`, `EML`, `PHON` and `ACT` (used for "enabled") already existed. `CURR` is already "currency", so "current" is `CRNT`. `RDIR` is "redirect", so the table is `PAGE_RDIR`. `TGT` is "target", so the link-target lookup is `LINK_TGT_LKP`. Only the words below are new.

| Word | Abbr. | | Word | Abbr. |
|---|---|---|---|---|
| announcement | ANNC | | page | PAGE |
| block | BLCK | | policy | POLY |
| brand | BRND | | popular | POPL |
| charge | CHRG | | row | ROW |
| column | COLM | | rule | RULE |
| condition | COND | | search | SRCH |
| courier | CRER | | setting | STNG |
| current | CRNT | | show | SHOW |
| draft | DRFT | | step | STEP |
| exchange | XCHG | | text | TEXT |
| external | XTRN | | title | TTL |
| fact | FACT | | window | WNDW |
| footer | FOOT | | | |
| free | FREE | | | |
| hash | HASH | | | |
| link | LINK | | | |
| meta | META | | | |
| navigation | NAV | | | |

### Added in Sprint 9a (SCRUM-208, storefront read model)

`SRCH`, `HASH`, `VSBL`, `PRDT`, `VRNT`, `IMG`, `CMPS`, `CARE`, `CHRT`, `COLN`, `CATG`, `GNDR`, `AGE`, `SKU`, `PRIC`, `CMPR` and `STCK` already existed. "Version" is `VER` and "reconciliation/reconcile" is `RCNL`, so the read model uses `VER` and `RCNL_*`. The concurrency token is `xmin` (section 2), not a column of ours. Only the words below are new.

| Word | Abbr. | | Word | Abbr. |
|---|---|---|---|---|
| available | AVLB | | rebuild | RBLD |
| derivative (resized image) | DRV | | ready | RDY |
| miss (a search with no results) | MISS | | swatch | SWCH |
| model (as in read model) | MODL | | discount | DISC |
| sellable (can be bought) | SELL | | end (as in end date) | END |
| query | QRY | | synonym | SYNM |

### Added in Sprint 9c (search)

`SRCH`, `SYNM`, `MISS`, `QRY`, `TEXT`, `TERM`, `IND` and `ACT` already existed (text is `TEXT`, so the columns are `SRCH_TEXT` and `QRY_TEXT`). Only the words below are new.

| Word | Abbr. | | Word | Abbr. |
|---|---|---|---|---|
| group | GRP | | phrase | PHRS |
| vector (full-text search vector) | VEC | | | |

### Added in Sprint 8 (SCRUM-202, Identity)

`HASH`, `ADDR`, `CHNG`, `PEND`, `STEP`, `MODL`, `LAST`, `USR`, `OTCM`, `ATMP`, `EXPY`, `CNT`, `NAME`, `DTE`, `IND`, `TYP`, `KEY`, `ACTR`, `MSG` and `COLM` ("column", Sprint 9) already existed and keep their abbreviation. `IDNT` is the schema name for "identity". "Revoked" is `RVKD` everywhere (including `SESS_RVKD_RSN_LKP`), never `RVK`. `GlossaryTests` fails if a word gets two abbreviations or an abbreviation is used for two words.

| Word | Abbr. | | Word | Abbr. |
|---|---|---|---|---|
| absolute | ABS | | must | MUST |
| access | ACSS | | occurred | OCCR |
| agent (user agent) | AGNT | | operation | OPRN |
| all | ALL | | password | PWD |
| audit | AUDT | | permission | PERM |
| client | CLNT | | previous | PREV |
| correlation | CORR | | recovery | RCVR |
| display | DISP | | refresh | RFSH |
| encrypted | ENC | | revoked | RVKD |
| enrolled | ENRL | | role | ROLE |
| entity | ENTY | | secret | SECR |
| failed | FAIL | | session | SESS |
| identity | IDNT | | source | SRC |
| IP address | IP | | temporary | TEMP |
| login | LGIN | | token | TOKN |
| multi-factor authentication | MFA | | used | USED |
| rotated | ROTD | | | |
