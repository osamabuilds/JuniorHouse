# Child Profiles: technical plan

- **Status:** Parked (owner, 2026-10-25): the spec and plan are kept, but nothing is built until the owner says.
- **Spec:** [spec.md](spec.md) · **Module:** Customers · **Module plan:** [../plan.md](../plan.md) · **ADR:** [0021](../../../adr/0021-customers-module-and-stand-in-customer.md) §3, §6

## Domain model
`ChildProfile` (`Id`, `CustomerId`, `Nickname`, `BirthMonth` as a first-of-month date, `UsualSizeId?`). Rules: nickname 1..50 chars, no control characters; birth month not in the future and not more than 16 years ago (configurable `Customers:ChildMaxAgeYears`); at most `MaxChildProfiles` (8) per customer. No height, weight or measurement history (owner decision Q-CHD-1). A pure `ChildAge` helper turns the birth month into an age for display (CHD-1).

## Application layer
| Command / Query | Output | Covers |
|---|---|---|
| `ListChildProfilesQuery` (paged, max 8 in practice) | `PagedResult<ChildProfileDto>` | CHD-1, 2 |
| `AddChildProfileCommand`, `UpdateChildProfileCommand` | `ChildProfileDto` | CHD-1, 3, 4, 11 |
| `DeleteChildProfileCommand` | none; permanent delete | CHD-7, FR-21 |
| `ExportChildProfileQuery` | JSON document of the one profile | CHD-8, NFR-29 |
| `IApparelCodeQueries` (Reference) | size label for the usual size | Q-CHD-2 |

Export content: profile fields, the usual size label, created/updated times, and a short note that no other data is held. `Content-Disposition: attachment`, `Cache-Control: no-store`.

## API endpoints
| Method | Route | Auth | Ownership check (SEC-05) |
|---|---|---|---|
| GET | `/api/customers/me/children` | `Customer` | filter `CUST_ID` |
| POST | `/api/customers/me/children` | `Customer` | customer from `ICurrentCustomer` |
| PUT | `/api/customers/me/children/{id}` | `Customer` | `WHERE ID AND CUST_ID`, else 404 |
| DELETE | `/api/customers/me/children/{id}` | `Customer` | same; `DELETE … WHERE ID AND CUST_ID` |
| GET | `/api/customers/me/children/{id}/export` | `Customer` | same |

## Events
None. Deliberately no child event: nothing outside the module may learn of a profile (CHD-10).

## Data
`CUST.CUST_CHLD`: `ID`, `CUST_ID` FK, `NICK_NAME` varchar(50), `BRTH_DT` date (check: day = 1), `USUL_SIZE_ID` smallint null (plain reference to Reference `SIZE_LKP`), audit, `xmin`. Index `IX_CUST_CHLD_CUST_ID`. Delete removes the row (no soft flag, no history table). When a future feature references a profile (orders), that reference is set to null on delete (recorded as a task for that feature).
Encryption: disk-level at the host; column-level not adopted now (ADR 0021 §6). The export and delete mechanism satisfies NFR-29 without a separate job.

## NFR / security design
Limit enforced under a lock on the `CUST_MAIN` row. Logging: handlers log the profile id and customer id only; a log-scrub test fails if any profile text appears in captured logs, traces or problem details (CHD-9). The storefront SSR never renders profile content on a cached response: account pages are `private, no-store` and reach the API only for signed-in requests (CHD-6). Export is a plain read of one owned row.

## Frontend
Storefront `features/customers/child-profiles` (`/account/children`): panels with an initial avatar, nickname, age text, "Usual size"; Add/Edit dialog (nickname, birth month and year selects, optional usual size); delete confirmation stating past orders are kept; "Download my child's data" button; the page text states only the account holder sees these profiles. Focus trap/Esc/focus return; `noindex`.

## Risks & alternatives
Dropping height/weight deviates from FR-18 (logged on SCRUM-168). Soft delete and column encryption rejected for now (ADR 0021).

## Security & coupling review
| Area | How this feature meets it |
|---|---|
| Authorization | `Policies.Customer`; every statement filters `CUST_ID`; tests for read, edit, delete and export of another customer's profile (404) and for signed-out access (401) |
| Input | nickname/length/charset, birth-month range, size id checked via `IApparelCodeQueries` |
| Output encoding | Angular encoding; JSON export serialised by the framework |
| Outbound calls | none |
| Resource limits | 8 profiles, body limit, page size clamp |
| Concurrency | row lock for the limit; `xmin` token on edit |
| Idempotency | delete is repeat-safe (second call 404) |
| Audit | audit columns (no profile content copied anywhere else) |
| Data protection | minors' data: owner-only, never logged, never in events, hard delete, export; not exposed to staff or the admin app (NFR-27, NFR-29, SEC-25) |
| Secrets and config | `MaxChildProfiles`, `ChildMaxAgeYears` validated at start-up |
| Error responses | problem details; 404 for foreign ids |
| New dependencies | none |
| Coupling | `Reference.Contracts` for size labels only; no contract exposes profiles |
| Abuse cases | (1) Another parent guessing profile ids: 404. (2) A support or report query reading child tables: no staff endpoint exists; architecture test forbids other modules referencing the table. (3) Child text leaking via error messages/logs: scrub test |
