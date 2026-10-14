/** One recorded adjustment (docs/specs/warehouse/adjustments/spec.md): immutable, with its reason and the balance before and after. */
export interface AdjustmentDto {
  readonly id: number;
  readonly variantRefId: number;
  readonly skuCode: string;
  readonly gtin: string;
  readonly binId: number;
  readonly binCode: string;
  readonly gradeId: number;
  readonly gradeCode: string;
  readonly quantityChange: number;
  readonly reasonId: number;
  readonly reasonCode: string;
  readonly note: string | null;
  readonly reconfirmed: boolean;
  readonly balanceBefore: number;
  readonly balanceAfter: number;
  readonly actorName: string;
  readonly at: string;
}

export interface AdjustmentReasonDto {
  readonly id: number;
  readonly code: string;
  readonly name: string;
}

/**
 * What the form sends. Above the large-adjustment limit the first send is refused with "Confirmation required" and the screen asks
 * again; the second send carries <c>reconfirmed</c> true and the same idempotency key (ADJ-4b, ADJ-8).
 */
export interface PostAdjustmentValue {
  readonly gtin: string;
  readonly binCode: string;
  readonly gradeId: number;
  readonly quantityChange: number;
  readonly reasonId: number;
  readonly note: string | null;
  readonly reconfirmed: boolean;
  readonly idempotencyKey: string;
  readonly actingPerson: string;
}

export interface AdjustmentFilters {
  readonly reasonId: number | null;
  readonly page: number;
  readonly pageSize: number;
}

/** A refused first attempt waiting for the person to confirm a large change. */
export interface PendingReconfirm {
  readonly value: PostAdjustmentValue;
  readonly message: string;
}
