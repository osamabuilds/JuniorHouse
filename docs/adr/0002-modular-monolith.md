# 0002 — Modular monolith with Clean Architecture

- Status: Accepted
- Date: 2026-09-25

## Context
BRD §9.1 and §9.10 define about 17 domain modules (Catalog, Orders, WMS, LOG, …). Microservices would add operational overhead that a small team can't justify at launch.

## Decision
- One deployable ASP.NET Core host (`src/api/src/Romp.Api`) that loads modules through `IModule`.
- Each module has `Domain`, `Application` and `Infrastructure` projects under `src/api/src/Modules/<Name>/`.
- Modules never reference each other. They communicate through integration events sent via the transactional outbox (see ADR 0004).
- Each module owns its own PostgreSQL schema. No module reads another module's tables.
- The rules are enforced by `tests/Romp.ArchitectureTests`.

## Consequences
- A module can be split out into a separate service later with little rework.
- Every new module has to be added to `ModuleBoundaryTests.ModuleNames`.
