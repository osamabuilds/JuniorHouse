# SCRUM-197: requirement traceability

Every acceptance criterion AC-1..AC-46 of [spec.md](spec.md) and how many Sprint 4 test files verify it. Backend tests carry `[Trait("Spec", "AC-n")]`; web tests name the AC in the test title or a comment. Only the Sprint 4 test files are counted (earlier sprints also have AC numbers, so their files are excluded): the Quality test project, the Vendor scorecard and receipt-progress tests, the Quality API integration tests, and the Sprint 4 admin specs. The Playwright specs (`src/web/e2e/quality.e2e.spec.ts`, plus the QC screens in `responsive.e2e.spec.ts`) exercise the same criteria end to end and are described by their titles.

```bash
for n in $(seq 1 46); do grep -rlE "AC-$n([^0-9]|$)" <sprint-4 test files>; done
```

Result: **every AC-1..AC-46 has at least one test.** AC-44 (usable, labelled, text-as-well-as-colour screens) is a web concern and is covered by the component specs and the layout suite rather than a backend trait.

| AC | Backend / integration test files | Web unit test files |
|---|---|---|
| AC-1 | 2 | 1 |
| AC-2 | 2 | 0 |
| AC-3 | 4 | 2 |
| AC-4 | 3 | 1 |
| AC-5 | 3 | 1 |
| AC-6 | 3 | 1 |
| AC-7 | 3 | 3 |
| AC-8 | 2 | 1 |
| AC-9 | 2 | 0 |
| AC-10 | 5 | 0 |
| AC-11 | 1 | 1 |
| AC-12 | 4 | 0 |
| AC-13 | 5 | 1 |
| AC-14 | 3 | 1 |
| AC-15 | 2 | 0 |
| AC-16 | 3 | 1 |
| AC-17 | 3 | 1 |
| AC-18 | 3 | 1 |
| AC-19 | 3 | 1 |
| AC-20 | 3 | 3 |
| AC-21 | 2 | 0 |
| AC-22 | 5 | 2 |
| AC-23 | 3 | 1 |
| AC-24 | 4 | 0 |
| AC-25 | 3 | 2 |
| AC-26 | 2 | 5 |
| AC-27 | 2 | 3 |
| AC-28 | 1 | 0 |
| AC-29 | 6 | 0 |
| AC-30 | 4 | 2 |
| AC-31 | 4 | 3 |
| AC-32 | 3 | 1 |
| AC-33 | 1 | 1 |
| AC-34 | 3 | 3 |
| AC-35 | 2 | 2 |
| AC-36 | 1 | 1 |
| AC-37 | 1 | 1 |
| AC-38 | 1 | 0 |
| AC-39 | 1 | 2 |
| AC-40 | 2 | 1 |
| AC-41 | 2 | 0 |
| AC-42 | 4 | 2 |
| AC-43 | 2 | 0 |
| AC-44 | 0 | 3 |
| AC-45 | 3 | 0 |
| AC-46 | 1 | 0 |
