# Stock allocations (Warehouse side)

Bin-level claims for an order: allocate oldest putaway first, release, give up part of a claim, convert a picked claim into a ledger decrement, and mark a bin "under review".

**Status:** Built (Sprint 14) · **Spec:** [fulfilment/allocations](../../fulfilment/allocations/spec.md) (this is the Warehouse half of it; ADR [0030](../../../adr/0030-fulfilment-module-allocation-and-scan-flow.md)) · **Requirements:** §5.15, FR-SC-09 (RSV-9), FR-SC-10, FR-SC-11 · **Code:** `Warehouse.Contracts/IStockAllocator`, `Allocations/` in Warehouse Domain, Application and Infrastructure · **Tests:** `Romp.Modules.Warehouse.Tests/Allocations` · **Module:** [warehouse](../README.md) · **Sprint:** S14
