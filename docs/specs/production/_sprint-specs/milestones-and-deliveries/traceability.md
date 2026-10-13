# SCRUM-183: requirement traceability

Every acceptance criterion AC-1..AC-38 of [spec.md](spec.md) and how many Sprint 3 test files verify it. Backend tests carry `[Trait("Spec", "AC-n")]`; web tests name the AC in the test title or a comment. Only the Sprint 3 test files are counted (Sprint 2 also has AC numbers, so its files are excluded).

```bash
for n in $(seq 1 38); do grep -rlE "AC-$n([^0-9]|$)" <sprint-3 test files>; done
```

| AC | Backend / integration test files | Web unit test files |
|---|---|---|
| AC-1 | 5 | 0 |
| AC-2 | 5 | 0 |
| AC-3 | 1 | 0 |
| AC-4 | 4 | 2 |
| AC-5 | 2 | 1 |
| AC-6 | 3 | 0 |
| AC-7 | 2 | 1 |
| AC-8 | 1 | 1 |
| AC-9 | 3 | 3 |
| AC-10 | 2 | 1 |
| AC-11 | 4 | 1 |
| AC-12 | 2 | 0 |
| AC-13 | 2 | 1 |
| AC-14 | 6 | 0 |
| AC-15 | 2 | 1 |
| AC-16 | 4 | 2 |
| AC-17 | 2 | 1 |
| AC-18 | 2 | 1 |
| AC-19 | 3 | 2 |
| AC-20 | 3 | 0 |
| AC-21 | 4 | 1 |
| AC-22 | 2 | 1 |
| AC-23 | 3 | 0 |
| AC-24 | 6 | 2 |
| AC-25 | 4 | 2 |
| AC-26 | 3 | 1 |
| AC-27 | 5 | 1 |
| AC-28 | 4 | 2 |
| AC-29 | 4 | 0 |
| AC-30 | 4 | 0 |
| AC-31 | 2 | 0 |
| AC-32 | 2 | 2 |
| AC-33 | 0 | 3 |
| AC-34 | 1 | 0 |
| AC-35 | 4 | 2 |
| AC-36 | 2 | 1 |
| AC-37 | 1 | 1 |
| AC-38 | 1 | 1 |

Every one of AC-1..AC-38 has at least one backend or web test (none shows 0 in both columns). AC-33 (semantic markup, 320 px, warnings as text) is checked by the component specs and by the layout suite in `src/web/e2e/responsive.e2e.spec.ts` (every production screen at seven widths from 320 to 1920 px). The end-to-end journey and edge cases are in `src/web/e2e/production.e2e.spec.ts`; those tests are named by behaviour rather than by AC number.
