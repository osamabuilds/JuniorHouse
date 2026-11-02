/** A discount code as staff see it (docs/specs/cart/discount-codes, FR-57). `usedCount` counts held and confirmed uses. */
export interface DiscountCodeDto {
  readonly id: number;
  readonly code: string;
  readonly kind: DiscountKind;
  readonly value: number;
  readonly minimumSpend: number | null;
  readonly expiresAt: string | null;
  readonly usageCap: number | null;
  readonly perCustomerCap: number | null;
  readonly includesSaleItems: boolean;
  readonly isActive: boolean;
  readonly categoryIds: readonly number[];
  readonly usedCount: number;
  readonly createdAt: string;
}

export type DiscountKind = 'percentage' | 'fixed';

/** What staff type; the server checks every field again and answers in words. */
export interface DiscountCodeInput {
  readonly code: string;
  readonly kind: DiscountKind;
  readonly value: number;
  readonly minimumSpend: number | null;
  readonly expiresAt: string | null;
  readonly usageCap: number | null;
  readonly perCustomerCap: number | null;
  readonly includesSaleItems: boolean;
  readonly categoryIds: readonly number[];
}
