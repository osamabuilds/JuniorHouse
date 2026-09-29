/** Received against ordered for one PO (Sprint 4, spec AC-40): what VNDR knows from QC's goods receipts. */

export interface PoReceiptProgressLineDto {
  readonly sizeId: number;
  readonly colourId: number;
  readonly orderedQty: number;
  readonly receivedQty: number;
  readonly outstandingQty: number;
}

export interface PoReceiptDto {
  readonly grnId: number;
  readonly grnNo: string;
  readonly receivedDate: string;
  readonly claimedTotal: number;
  readonly countedTotal: number;
}

export interface PoReceiptProgressDto {
  readonly poId: number;
  readonly orderedTotal: number;
  readonly receivedTotal: number;
  readonly lines: readonly PoReceiptProgressLineDto[];
  readonly receipts: readonly PoReceiptDto[];
}
