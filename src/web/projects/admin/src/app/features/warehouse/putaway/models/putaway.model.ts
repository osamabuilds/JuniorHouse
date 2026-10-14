/** One accepted line waiting to be shelved (docs/specs/warehouse/putaway/spec.md, PUT-14): "put away n of N". */
export interface PutawayItemRowDto {
  readonly id: number;
  readonly subBatchId: number;
  readonly poId: number;
  /** Null until Labelling has assigned the variant its barcode (status AWAITING_GTIN). */
  readonly skuCode: string | null;
  readonly gtin: string | null;
  readonly gtinRetired: boolean;
  readonly gradeId: number;
  readonly gradeCode: string;
  readonly qcQuantity: number;
  readonly putQuantity: number;
  readonly pending: number;
  readonly statusId: number;
  readonly statusCode: string;
  readonly receivedAt: string;
}

export interface ScanPendingItemDto {
  readonly itemId: number;
  readonly poId: number;
  readonly gradeId: number;
  readonly gradeCode: string;
  readonly pending: number;
}

/** What a barcode scan answers (PUT-6, PUT-7): the SKU, whether the barcode is retired, and the items waiting to be shelved. */
export interface ScanLookupDto {
  readonly variantRefId: number;
  readonly skuCode: string;
  readonly gtin: string;
  readonly gtinRetired: boolean;
  readonly pendingItems: readonly ScanPendingItemDto[];
}

export interface PutawayRecordDto {
  readonly id: number;
  readonly itemId: number;
  readonly binId: number;
  readonly binCode: string;
  readonly quantity: number;
  readonly scanned: boolean;
  readonly actorName: string;
  readonly at: string;
  readonly itemPending: number;
  readonly skuCode: string | null;
}

/**
 * The item staff are putting away. <c>scanned</c> is true when the barcode was scanned and false when the item was chosen from the
 * list because no tag exists yet (PUT-19). The idempotency key makes a double tap apply once (PUT-8).
 */
export interface PutawaySelection {
  readonly itemId: number;
  readonly label: string;
  readonly pending: number;
  readonly gradeCode: string;
  readonly scanned: boolean;
  readonly gtin: string | null;
  readonly idempotencyKey: string;
}

export interface ConfirmPutawayValue {
  readonly binCode: string;
  readonly quantity: number;
  readonly scanned: boolean;
  readonly gtin: string | null;
  readonly idempotencyKey: string;
  readonly actingPerson: string;
}
