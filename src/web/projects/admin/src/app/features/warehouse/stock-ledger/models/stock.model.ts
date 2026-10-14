/** One bin a variant sits in, with the grade and quantity (LED-6: pick staff need the location, not only the count). */
export interface StockBinQuantityDto {
  readonly binId: number;
  readonly binCode: string;
  readonly gradeId: number;
  readonly gradeCode: string;
  readonly quantity: number;
}

/** Stock of one variant (docs/specs/warehouse/stock-ledger/spec.md, LED-5). Available equals on hand until reservations exist. */
export interface StockVariantRowDto {
  readonly variantRefId: number;
  readonly variantId: number;
  readonly skuCode: string;
  readonly gtin: string;
  readonly gtinRetired: boolean;
  readonly onHand: number;
  readonly available: number;
  readonly onHandAccepted: number;
  readonly onHandConcession: number;
  readonly bins: readonly StockBinQuantityDto[];
}

/** One ledger line (LED-9): what changed, where, by whom, with the balance before and after. */
export interface StockMovementDto {
  readonly id: number;
  readonly variantRefId: number;
  readonly skuCode: string;
  readonly binId: number;
  readonly binCode: string;
  readonly gradeCode: string;
  readonly kindCode: string;
  readonly referenceId: number;
  readonly quantityChange: number;
  readonly balanceBefore: number;
  readonly balanceAfter: number;
  readonly actorName: string;
  readonly at: string;
}

export interface StockFilters {
  readonly searchText: string;
  readonly page: number;
  readonly pageSize: number;
}
