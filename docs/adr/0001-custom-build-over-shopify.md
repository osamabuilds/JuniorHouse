# 0001 — Custom build over Shopify

- Status: Accepted
- Date: 2026-09-25

## Context
BRD v9.0 §9.2 lays out two paths: a custom .NET/Angular platform (Option A) or launching on Shopify and migrating later (Option B). Romp's differentiators (the Fit Finder, Child Profiles and the full supply-chain flow from vendor to warehouse to courier) are core to the data model, not add-ons.

## Decision
Build a custom platform (Option A).

## Consequences
- Full control of the Fit Finder, Child Profile and supply-chain modules, with no platform fees or revenue share.
- A slower first launch (roughly 3–5 months to MVP) than Shopify.
- We carry PCI scope for checkout ourselves. It stays small because card data is always handled by the payment gateway (NFR-11).
