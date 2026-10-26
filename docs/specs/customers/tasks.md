# Customers (Sprint 10): module tasks

Ordered, checkable steps. Each names the test that proves it. Test-first: write the failing test, then make it pass. Run .NET builds and tests in the Linux SDK container (Windows Application Control blocks local DLLs; integration results are container or CI results). Commits start with `SCRUM-xxx: `; the Jira umbrella key for Sprint 10 is created when the owner approves the plan.

**Approval status:** the specs were approved on 2026-10-25; the module and feature plans and ADRs 0021 and 0022 were drafted the same day. On 2026-10-26 the owner asked to "start planning and implementing next sprint" and was away, so work proceeds on the drafted plans as written. Anything the plans leave open (the `Q-*` questions) takes the default stated in the spec, is logged under "Decisions taken on defaults" below, and stays open for the owner to overrule.

**In scope:** accounts (the stand-in customer only), addresses, wishlist, newsletter, notify-me. **Parked by the owner:** child-profiles (no table, endpoint, screen or menu entry), and real customer sign-in, registration, reset and the FR-39 vs SEC-02 decision (the final auth sprint).

## Foundations

- [x] **F1** Module skeleton: `Romp.Modules.Customers.{Domain,Application,Infrastructure,Contracts}`, `CustomersModule : IModule`, solution entries, `Customers` in `ModuleNames`, `Program.cs` registration, `CUST` schema row in the constitution and naming doc, new glossary words. *Proof:* `ModuleBoundaryTests`, `StructureSyncTests`, `NamingConventionTests`, `GlossaryTests`.
- [x] **F2** `Customer` aggregate and `CUST_MAIN` with the first migration (`OUTB_MSG`, `INBX`); `ICustomerDbContext`. *Proof:* `CustomerTests`, `CustomerMigrationTests`.
- [x] **F3** `ICurrentCustomer`, `Policies.Customer`, the development-only handler (`X-Dev-Customer: none`, `/dev/customer` switch) and the seeder for the stand-in and a second customer. *Proof:* `DevelopmentCustomerHandlerTests`, `CustomerEndpointsTests.Me_ReturnsTheStandIn_AndNobodyWhenSwitchedOff` (ACT-14, ACT-18).
- [x] **F4** `GET /api/customers/me`. *Proof:* `CustomerEndpointsTests`, matrix row added to `authorization-matrix.csv` (customer routes are a separate policy, listed with `customer`).
- [x] **F5** Architecture tests: customer-owned data is never queried without the customer filter; no handler takes a customer id from the request; features depend one way only. *Proof:* `CustomersFeatureDependencyTests`, `CustomerOwnershipArchitectureTests`.

## addresses

- [x] **A1** Reference: `PROV_LKP`, `CITY_LKP.PROV_ID`, seed, `IGeographyQueries`, `GET /api/geography/provinces` and `/provinces/{id}/cities`. *Proof:* `GeographyMigrationTests`, `GeographyQueriesTests`, `GeographyEndpointsTests` (ADD-4).
- [ ] **A2** `CustomerAddress` and `AddressBook` (default rules, limit, phone normalisation). *Proof:* `AddressBookTests`, `PhoneNumberTests` (ADD-2, ADD-5, ADD-6).
- [ ] **A3** `CUST_ADDR` migration with the partial unique default index. *Proof:* `AddressMigrationTests`.
- [ ] **A4** Commands and the paged list query, with the row lock on `CUST_MAIN`. *Proof:* `AddressCommandTests`, `AddressConcurrencyTests` (ADD-1, ADD-2, ADD-6, ADD-7).
- [ ] **A5** Endpoints under `/api/customers/me/addresses`, ownership by construction. *Proof:* `AddressEndpointsTests.AnotherCustomersAddress_Is404ForReadEditRemoveAndDefault` (ADD-3).
- [ ] **A6** `ICustomerAddressQueries` in `Customers.Contracts`. *Proof:* `AddressContractTests`.
- [ ] **A7** Storefront `features/customers/addresses` (labels, dependent selects, focus trap), account shell, `noindex`, `no-store`. *Proof:* component and store specs, `AddressesPage` e2e (ADD-8).

## wishlist, newsletter, notify-me

Tasks for these three are added to this file when their turn comes, from their feature plans, in the order wishlist, newsletter, notify-me (module plan, "Order of work").

## Decisions taken on defaults

Recorded as the work reaches them (Q-ADD-1 provinces and cities seed list, Q-ADD-2 fields, Q-ADD-3 limit of 10, Q-ACT-7 phone format). Each is a data or configuration change, not a code change, if the owner chooses otherwise.

- **Q-ADD-1 (A1):** seven provinces and territories and 27 cities (the original 7 plus 20 main cities) are seeded. `CITY_LKP.PROV_ID` is nullable, not required: the admin Reference Data screen creates a city with no province, and a city without one is simply not offered to customers. Provinces are read-only in the admin API (a migration adds one); assigning a province to an admin-added city has no screen yet, so it is a data fix until the owner asks for one.
- `GET /api/geography/*` are anonymous and `public, max-age=3600`; the prefix is on the authorization-matrix anonymous list.
