# <JIRA-KEY>: <Feature name>

- **Status:** Draft | Approved | Implemented
- **Jira:** <SCRUM-xxx> (epic <SCRUM-yy>)
- **BRD sections:** §x.y
- **Requirement IDs:** FR-xx, NFR-xx, SEC-xx, …

## Problem & intent
Who needs this and why, in 2–4 sentences. No implementation detail.

## Requirements (quoted from the BRD)
> **FR-xx:** exact text from the BRD.

> **SEC-xx:** exact text from the BRD.

## User flow
The relevant BRD §5.x flow, step by step. Mark anything added beyond the BRD with `(new)`.

## Acceptance criteria
Each one should be testable. Reuse BRD test-case IDs where they exist.

| ID | Given / When / Then | Source |
|---|---|---|
| AC-1 | Given …, when …, then … | TC-XXX-01 |
| AC-2 | … | FR-xx (new edge case) |

## Non-functional constraints
Which NFR/SEC items apply and the measurable target, e.g. "NFR-03: recommendation ≤ 500ms p95".

## Out of scope
What is explicitly not part of this feature (e.g. Phase 2 items).

## Open questions
- [ ] Question: who decides, and what it blocks.
