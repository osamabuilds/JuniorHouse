# Address book: technical plan

- **Spec:** [spec.md](spec.md) · **Module:** Customers · **Module plan:** [../plan.md](../plan.md)

## Domain model
`CustomerAddress` (`Id`, `CustomerId`, `RecipientName`, `Phone`, `ProvinceId`, `CityId`, `LineOne`, `LineTwo?`, `Postcode?`, `IsDefault`). Rules: recipient 2..80 chars, line one 5..150, line two ≤150, postcode ≤10 digits/letters, phone normalised to `+92XXXXXXXXXX` (Pakistani mobile, rule defaulted since Q-ACT-7 is deferred; landlines accepted as `+92` plus area code, 10 digits); at most `MaxAddresses` (10) per customer; exactly one default when the book is non-empty. A pure `AddressBook` domain service applies "first becomes default" and "removing the default promotes the most recent".

## Application layer
| Command / Query | Output | Covers |
|---|---|---|
| `ListAddressesQuery` (paged) | `PagedResult<AddressDto>` with province and city names | ADD-1, ADD-7 |
| `AddAddressCommand`, `UpdateAddressCommand`, `RemoveAddressCommand`, `MakeDefaultAddressCommand` | `AddressDto` / none | ADD-1, 2, 3, 6 |
| `IGeographyQueries` (from `Reference.Contracts`) | provinces, cities, pair check | ADD-4 |
| `ICustomerAddressQueries` (new, in `Customers.Contracts`) | the customer's addresses for checkout in S12 (reads `ICurrentCustomer`) | FR-40 |

Validators: lengths above, phone format, `CityId` belongs to `ProvinceId` (one bulk `IGeographyQueries` call).

## API endpoints
| Method | Route | Auth | Ownership check (SEC-05) |
|---|---|---|---|
| GET | `/api/customers/me/addresses?page&pageSize` | `Customer` | filter `CUST_ID` |
| POST | `/api/customers/me/addresses` | `Customer` | customer from `ICurrentCustomer` |
| PUT | `/api/customers/me/addresses/{id}` | `Customer` | `WHERE ID=@id AND CUST_ID=@cust`, else 404 |
| DELETE | `/api/customers/me/addresses/{id}` | `Customer` | same |
| POST | `/api/customers/me/addresses/{id}/default` | `Customer` | same |
| GET | `/api/geography/provinces`, `/api/geography/provinces/{id}/cities` | `Public`, cacheable | none (reference data) |

## Events
None.

## Data
`CUST.CUST_ADDR`: `ID`, `CUST_ID` FK, `RCPT_NAME` varchar(80), `PHON` varchar(20), `PROV_ID` smallint, `CITY_ID` smallint, `LINE_ONE` varchar(150), `LINE_TWO` varchar(150) null, `PCDE` varchar(10) null, `DFLT_IND` boolean, audit, `xmin`. Indexes: `IX_CUST_ADDR_CUST_ID`; **partial unique** `UQ_CUST_ADDR_DFLT` on (`CUST_ID`) where `DFLT_IND`; check `CK_CUST_ADDR_...` on lengths. Province and city ids are plain references (no cross-schema FK).
Reference change (additive migration): `PROV_LKP` (standard lookup shape, seeded: Punjab, Sindh, Khyber Pakhtunkhwa, Balochistan, Islamabad Capital Territory, Gilgit-Baltistan, Azad Jammu and Kashmir), `CITY_LKP.PROV_ID` smallint FK (existing cities mapped), seed of main cities; `IGeographyQueries` in `Reference.Contracts`. The seed list is confirmed with the business (Q-ADD-1 default).

## NFR / security design
Add/make-default/remove run in one transaction that first locks the `CUST_MAIN` row (`SELECT … FOR UPDATE`) so the 10-address limit and the single default are race-free; the partial unique index is the backstop (ADD-2, ADD-6). Lists project in SQL and join names from the cached `IGeographyQueries` result, no per-row calls (ADD-7). Addresses are never logged in full.

## Frontend
Storefront `features/customers/addresses` (`/account/addresses`): panels with Default status word, add/edit dialog (focus trap, Esc, focus return), remove confirmation, province→city dependent selects; all labels, `autocomplete` values (`street-address`, `tel`, `address-level1/2`, `postal-code`, `name`), `noindex`, `no-store`. NgRx store per conventions; paged via `<rp-pagination>` equivalent in the design system.

## Risks & alternatives
Geography seed is incomplete until the business supplies the list; adding cities is a migration/data row, not code. Free-text city rejected (naming rules: lookups for enumerations).

## Security & coupling review
| Area | How this feature meets it |
|---|---|
| Authorization | `Policies.Customer`; every statement filters `CUST_ID`; tests: customer B cannot read, edit, delete or default customer A's address (404) |
| Input | FluentValidation (lengths, charset, phone format, city-in-province); request records separate from entity |
| Output encoding | Angular encoding; no raw SQL beyond the quoted lock statement |
| Outbound calls | none |
| Resource limits | 10 addresses, page size 1..100, body size limit |
| Concurrency | row lock + partial unique index + `xmin` token |
| Idempotency | make-default and remove are repeat-safe |
| Audit | audit columns |
| Data protection | address and phone are personal data (NFR-26): not logged, not in events |
| Secrets and config | `Customers:MaxAddresses` validated |
| Error responses | problem details, 404 for foreign ids |
| New dependencies | none |
| Coupling | `Reference.Contracts.IGeographyQueries` only; no cross-schema FK |
| Abuse cases | (1) Id enumeration of others' addresses: 404, indistinguishable from missing. (2) Parallel adds to exceed the limit: lock + limit check. (3) Oversized/odd strings or script in fields: validated, encoded on output |
