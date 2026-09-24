# Romp — working notes for Claude

A direct-to-consumer kidswear platform for Pakistan: the storefront (with Fit Finder and Child Profiles) plus the supply-chain back office (vendors → QC → barcodes → warehouse → packing → couriers → returns).

## Sources of truth (read before building anything)

| What | Where |
|---|---|
| Business requirements (BRD v9.0) | `Romp_Master_v9_Merged.docx`, which is being moved into `docs/specs/` one feature at a time |
| Feature specs, plans, tasks | `docs/specs/<JIRA-KEY>-<slug>/` |
| Architecture decisions | `docs/adr/` (these override the BRD where they conflict) |
| Backlog | Jira project `SCRUM`, osamabuss048.atlassian.net |

Requirement IDs come from the BRD and must be used unchanged everywhere: `FR-xx`, `FR-SC-xx`, `NFR-xx`, `NFR-FT-xx`, `NFR-SC-xx`, `SEC-xx`, `SEO-xx`, test cases `TC-<AREA>-xx`, features `FEAT-<AREA>-xx`. Each Jira task carries its ID as a label.

## Spec-driven workflow (required for every feature)

Do not write production code until the spec is approved. Work through the steps in order and stop for the user's approval at each ⏸:

1. **Spec**: copy `docs/specs/_template/spec.md` into `docs/specs/<JIRA-KEY>-<slug>/spec.md`.
   - Say *what* is needed and *why*, not *how*.
   - Quote the BRD requirement text exactly, with its IDs.
   - Take acceptance criteria from the BRD's `TC-*` cases. Add edge cases the BRD misses, marked `(new)`.
   - Record anything ambiguous under **Open questions**. Never resolve it silently. ⏸
2. **Plan**: write `plan.md` from the template: domain model, commands/queries, endpoints, events, schema changes, and which NFR/SEC items apply and how they're met. Any architectural choice not already covered by an ADR gets a new ADR. ⏸
3. **Tasks**: write `tasks.md` as small, ordered, checkable steps. Each step names the test that proves it.
4. **Implement test-first**: write the failing test for an acceptance criterion, then make it pass. Tag each test with the IDs it covers (see Testing).
5. **Close the loop**: if the implementation had to differ from the spec, update the spec in the same PR. The spec must describe what was actually built. The PR description links the spec folder and lists the requirement IDs covered.

Small changes (typo fixes, dependency bumps, CI tweaks, refactors that change no behaviour) skip steps 1–3.

**Rules**
- Never invent requirements. If the BRD doesn't say, ask.
- Where the BRD contradicts itself or an ADR, raise it; don't pick a side. Known conflicts are listed below.
- Phase 2/3 features (marked `P2`/`P3` in the BRD) are out of scope unless the user asks for them.

## Architecture (see `docs/adr/`)

- **Modular monolith** (ADR 0002). One host, `src/api/src/Romp.Api`. Each module lives in `src/api/src/Modules/<Name>/` with `Domain`, `Application` and `Infrastructure` projects and implements `IModule`.
  - Modules never reference each other. They communicate only through integration events sent via the outbox.
  - Each module owns its own PostgreSQL schema, and no module queries another module's tables.
  - `tests/Romp.ArchitectureTests` enforces this. Add every new module to `ModuleNames` there.
  - The Domain layer has no dependency on EF Core, ASP.NET Core, or any other layer.
- **Outbox** (ADR 0004). Domain events and notifications are written in the same transaction as the business change. A notification failure must never roll back the business transaction (NFR-FT-08).
- **Stack** (ADR 0003): .NET 10, Angular 22 (storefront is server-side rendered), PostgreSQL 17, Redis, Docker, self-hosted VPS behind Cloudflare. The BRD's Azure wording (Service Bus, App Insights, AKS, Azure DevOps) is stale; ignore it.

**Rules that apply everywhere (the BRD marks these launch-blocking)**
- Recalculate every price and total on the server. Never trust amounts sent by the client (SEC-09).
- Every endpoint that takes a resource ID checks that the resource belongs to the caller (SEC-05). This matters most for Child Profile data.
- Admin endpoints check the caller's role server-side on every request (SEC-08).
- Verify payment webhook signatures before changing any order state (SEC-10).
- Order placement is idempotent and stock decrement is atomic (FR-28, FR-29, NFR-08).
- Money is `decimal` / `numeric(12,2)` in PKR. Never `double`.
- Never log passwords, card data, or full Child Profile records (SEC-25).
- Child data is personal data about minors: owner-only access, and exportable or deletable on request (NFR-27..30).

## Commands

```bash
# Backend (from src/api)
dotnet build Romp.slnx
dotnet test Romp.slnx

# Frontend (from src/web)
npx ng build storefront | npx ng build admin
npx ng test storefront --watch=false
npx ng serve storefront            # http://localhost:4200

# Local infrastructure (from repo root; copy .env.example to .env first)
docker compose up -d postgres redis mailpit
```

The API runs on http://localhost:5051 (`/health/live`, `/health/ready`).

## Testing

- Backend: xUnit v2. Use `Method_Scenario_Expectation` names (underscores are allowed in `tests/`).
- Tag tests with the requirement or test-case IDs they verify:
  `[Trait("Spec", "FR-13")] [Trait("Spec", "TC-FIT-03")]`
- Every acceptance criterion in a spec has at least one test.
- Integration tests that touch PostgreSQL use Testcontainers, not an in-memory database.

## Conventions

- Branches: `<JIRA-KEY>-<short-slug>`, e.g. `SCRUM-162-ef-core-wiring`. One Jira issue per branch.
- Commits: start with `SCRUM-xxx: `. Author is Osama Shafique <osama.shafique11@gmail.com> (set per repo).
- Jira workflow: Idea → To Do → In Progress → Testing → Done. Move an issue to In Progress when work starts, to Testing when its PR is open, and to Done when the PR is merged.
- New NuGet packages: add the version in `src/api/Directory.Packages.props` only. Warnings are treated as errors.

## Environment notes (developer machine)

- Windows 11. The shell is PowerShell 5.1, so `&&` doesn't work; use `;`.
- **Windows Application Control blocks locally built DLLs** from loading in `Romp.Api.IntegrationTests`. This is a machine policy, so don't try to bypass it. CI (Linux) is the verification for those tests; say so when reporting results.
- The `gh` CLI is not installed. Open PRs through a compare link: `https://github.com/osamabuilds/JuniorHouse/compare/main...<branch>?expand=1`.
- Docker Desktop may not be running. Check before relying on it.

## Open decisions (don't assume an answer)

- **FR-39 vs SEC-02**: hard account lockout, or progressive throttling + CAPTCHA? The recommendation is SEC-02, but it's unconfirmed.
- **Mediator library**: MediatR (commercial licence) or `Mediator` (MIT, source-generated)? Neither has been added yet.
- **BRD errata** (SCRUM-168): FR-10 cites "FR-20" but means FR-28. The supply-chain section cites old section numbers.
