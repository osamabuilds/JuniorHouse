# Identity (Sprint 8): module tasks

Shared steps and the order of work for the sprint. Each feature's own steps are in `<feature>/tasks.md`; platform steps are in [../platform/tasks.md](../platform/tasks.md). Every step names the test that proves it. Test-first: write the failing test, then make it pass. Run .NET builds and tests in the Linux SDK container (Windows Application Control blocks local DLLs; integration results are reported as container or CI results).

**How each task is done (owner, 2026-10-25):** one task at a time, test-first (red, then green, then refactor), with all three kinds of proof where they apply: **unit tests** for rules and pure code; **integration tests** against real PostgreSQL (Testcontainers) and the real HTTP pipeline; and **security tests** written as automated tests before the code (authorization and ownership across two users, cookie flags, session fixation and replay, throttling and enumeration, injection and log-leak checks). A scanner pass (OWASP ZAP baseline) against the local stack, which is the owner's own system, runs once the sign-in flow exists and its findings are fixed or recorded. The task is reported with the exact commands and results; nothing is committed until the owner says.

Jira: SCRUM-202. Branch: `SCRUM-202-secure-deploy`. Commits start with `SCRUM-202: `.

- [x] **G1** Specs approved, plans and ADRs 0015 to 0017 approved (owner, 2026-10-16). *Proof:* n/a (doc).
- [x] **G2** Scaffold the Identity module: four projects, `IdentityModule : IModule`, solution entries, `Identity` added to `ModuleNames`, empty feature folders. *Proof:* `ModuleBoundaryTests`, `StructureSyncTests` (identity cards switch from Planned as features land).
- [x] **G3** Glossary: add the new words from the module plan to `docs/db/naming.md` ("Added in Sprint 8"), schema row `IDNT` for staff identity. *Proof:* `NamingConventionTests`.
- [x] **G4** `Permissions` codes, `RequirePermission(...)` and `MapSecuredGroup(prefix, permission)` in `BuildingBlocks.Web`, the old policy overloads kept until roles task R9. *Proof:* `PermissionCodesTests` (codes are unique, lower-case, `area.action`), `EndpointSecurityTests` still green.
- [x] **G5** Lookup migration `AddIdentityLookups`: `ROLE_LKP`, `PERM_LKP`, `ROLE_PERM_MAP` seeded from the roles spec (Owner superset; Catalogue, Order-ops, Support, Marketing). *Proof:* `IdentityLookupSeedTests` (every `Permissions` constant has a row and the reverse; Owner has `ALL_PERM_IND`; Order-ops has `inventory.read` and no `*.write`).
- [x] **G6** `HttpContextCurrentActor` (user name from the principal, `system` outside a request) and an `ActorScope` for `operator-cli`; replaces `SystemCurrentActor` in the host (`AddHttpContextCurrentActor()`). Names outside `[a-z0-9._:-]` (max 100) become `unknown`. *Proof:* `HttpContextCurrentActorTests`, `ActorScopeTests`.
- [x] **G7** Host options (`BuildingBlocks.Web/HostOptions.cs`, `AddRompHostOptions`): `Site:PublicBaseUrl` (bare origin; https required outside Development/Testing), `Network:TrustedProxies` (IP or CIDR, never "trust everyone": prefix under /8 IPv4 or /16 IPv6 and 0.0.0.0/:: refused), `Identity:UseRealAuthentication` (unset means real outside Development/Testing; explicit false refused there), all validated at start-up. Forwarded-headers wiring itself stays with sessions AC-15 / platform D1. *Proof:* `SiteOptionsValidatorTests`, `IdentityOptionsValidatorTests`, `SecurityGateTests` (still refuses to start without a real scheme).
- [x] **G8** Host operator verbs plumbing (`create-owner`, `reset-owner`, `migrate`, `healthcheck`, plus the existing `rebuild-storefront`) in `Romp.Api/HostVerbs/`: `HostVerbParser` (first argument, exact valueless switches, errors never echo input), `OperatorSecretReader` (environment variable, else no-echo prompt), `HostVerbRunner` (runs the handler under `ActorScope` `operator-cli`, exit codes, no exception message printed). Only `rebuild-storefront` has a handler; the others exit 69 until staff-accounts 10 and platform D2/D3 register theirs. Namespace is `HostVerbs` because CA1716 forbids `Operator`. *Proof:* `HostVerbTests` (unknown verb fails, a secret on the command line is rejected).
- [x] **G9** Admin app same-origin: `apiBaseUrl` empty, `proxy.conf.json` for `ng serve`, `/api` proxy in the admin container for local compose. *Proof:* `api-client.service.spec.ts` (relative URLs), Playwright smoke `e2e/identity/same-origin.e2e.spec.ts`.
- [x] **G10** `core/auth` in the admin app: NgRx session store, HTTP interceptor (credentials, anti-forgery header, one silent refresh then redirect), `permissionGuard`, `*hasPermission`. *Proof:* `auth.effects.spec.ts`, `auth.interceptor.spec.ts`, `permission.guard.spec.ts`.
- [x] **G11** `StructureSyncTests` learns that `platform` is a spec-only area. *Proof:* `StructureSyncTests.PlatformIsSpecOnly`.
- [ ] **G12** Close the loop: traceability file, cards Implemented with real paths, specs and plans updated where the build differed, README "Try Sprint 8" (real-auth run), sprint-plan status line, ADR "As built" notes. *Proof:* `StructureSyncTests`.

## Order

1. G2, G3, G4, G5 (Monday afternoon, after approval; ADRs already approved).
2. [staff-accounts](staff-accounts/tasks.md) steps 1 to 5, G6, G7, G8, then [sessions](sessions/tasks.md) (Tuesday).
3. [mfa](mfa/tasks.md), staff-accounts steps 6 to 10, G9, G10, web sign-in (Wednesday morning).
4. [roles](roles/tasks.md): capture the matrix first (R4), review (R5), migrate groups (R6), then enforce (Wednesday afternoon and Thursday).
5. [audit-trail](audit-trail/tasks.md) (Thursday), G11.
6. Web screens for staff, roles and audit; e2e journeys with real authentication; G12 (Thursday evening and Friday).
7. [Platform](../platform/tasks.md) runs in parallel from Wednesday; staging deploy and smoke test on Friday.

Full verification before the PR: `dotnet test Romp.slnx` in the container, `npx ng test admin --watch=false`, `npx ng build admin`, the new Playwright journeys with `Identity:UseRealAuthentication=true`, the existing Playwright suites against the development handler, and the staging smoke test. Anything not run is stated as such in the PR.
