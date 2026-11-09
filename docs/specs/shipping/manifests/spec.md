# Manifests

- **Status:** Draft, applied by delegation (owner away, 2026-11-09)
- **Jira:** to be created
- **BRD sections:** §5.18
- **Requirement IDs:** FR-SC-15

## Problem & intent
When the courier collects parcels, both sides need the same list. A manifest per courier per day, counted against the parcels actually handed over, ends "the courier says they never received it" disputes.

## Requirements (quoted from the BRD)
> **§5.18:** End-of-day Manifest: a consolidated list of all parcels being handed to a given courier that day, generated automatically and reconciled against physical parcel count at pickup.

> **FR-SC-15:** System shall generate a reconciled end-of-day manifest per courier, matched against physical parcel count.

## Acceptance criteria

| ID | Given / When / Then | Source |
|---|---|---|
| MNF-1 | Given booked shipments not yet on a manifest, when the manifest is generated for a courier and day, then it lists each with order number, waybill, COD amount and weight, and a total count | FR-SC-15 |
| MNF-2 | Given the end of the day arrives, then manifests are generated automatically for every courier with parcels (a worker), and staff can also generate one by hand | §5.18 |
| MNF-3 | Given a shipment is on a manifest, then it is not put on another (new) | §5.18 |
| MNF-4 | Given generating twice for the same courier and day, then new parcels are added to the open manifest and nothing is duplicated (new) | NFR-09 |
| MNF-5 | Given the courier collects, when staff enter the physical parcel count, then the manifest is "Matched" when equal and "Mismatch" with the difference when not | FR-SC-15 |
| MNF-6 | Given a mismatch, then staff can mark each listed parcel as handed over or not, and a parcel marked not handed over returns to the next manifest (new) | §5.18 |
| MNF-7 | Given a manifest is reconciled, then it is closed and cannot be changed; a correction is a new note (new) | NFR-SC-05 |
| MNF-8 | Given a manifest, then it is a printable page: one heading, a table with scoped headers, courier signature lines (new) | NFR-19 |
| MNF-9 | Given any manifest action, then it records the staff member and the time | NFR-SC-05 |
| MNF-10 | Given the manifest list, then it is paged, newest first (new) | CONVENTIONS |

## Out of scope
Electronic hand-over confirmation by the courier, COD remittance (FR-SC-17).

## Open questions
- [ ] **Q-MNF-1** When does "end of day" fall? Recommendation: 17:00 Pakistan time, a setting (`Shipping:ManifestHour`).
