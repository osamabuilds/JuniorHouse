# Lookups (reference data)

- **Module:** [`reference`](../README.md) · **Feature:** `lookups` · **Status:** Implemented

Staff-maintained reference data that every other module uses: sizes, colours, fabrics, categories, age brackets, payment terms and other enumerated values. Lookup tables are seeded by migrations and read by other modules through `Reference.Contracts`.

## Specification
- Sprint spec: [po-creation-and-master-data](../../vendor/_sprint-specs/po-creation-and-master-data/spec.md) (plan, tasks alongside)

## Where it lives

| Tree | Path |
|---|---|
| API | `src/api/src/Modules/Reference/Romp.Modules.Reference.{Domain,Application,Infrastructure}/Lookups/` |
| API tests | `src/api/tests/Romp.Modules.Reference.Tests/Lookups/` |
| Web | `src/web/projects/admin/src/app/features/reference/lookups/` |
