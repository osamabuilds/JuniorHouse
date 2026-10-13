# Vendor & procurement

Plan and buy (BRD §5.9): vendors, purchase orders, versioned amendments, tech-pack files and the vendor view.

**Schema:** `VNDR` · **Code:** `src/api/src/Modules/Vendor/` · **Web:** `features/vendor/` · **Rules:** [constitution](../../CONSTITUTION.md)

## Features

| Feature | What it does | Status |
|---|---|---|
| [vendors](vendors/README.md) | Vendor records and scorecard | Implemented |
| [purchase-orders](purchase-orders/README.md) | PO creation, lifecycle and receipt progress | Implemented |
| [revisions](revisions/README.md) | Versioned PO amendments | Implemented |
| [files](files/README.md) | Tech-pack and spec attachments | Implemented |
| [vendor-responses](vendor-responses/README.md) | Vendor response capture | Implemented |
| [vendor-view](vendor-view/README.md) | Vendor-facing PO view | Implemented |

## Module decisions

Decisions that apply to several features of this module are recorded here, or in the ADRs under [`docs/adr/`](../../adr/).

Back to the [spec index](../README.md).
