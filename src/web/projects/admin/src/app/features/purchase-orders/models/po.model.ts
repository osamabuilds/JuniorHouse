export interface PoLineDto {
  readonly sizeId: number;
  readonly colourId: number;
  readonly qty: number;
}

export interface PoStatusHistoryDto {
  readonly poStatusId: number;
  readonly cancelReasonId: number | null;
  readonly insrDte: string;
  readonly insrBy: string;
  readonly note?: string | null;
}

export interface PoDto {
  readonly id: number;
  readonly poNo: string;
  readonly vendorId: number;
  readonly styleId: number;
  readonly unitCost: number;
  readonly expectedDeliveryDate: string;
  readonly paymentTermId: number;
  readonly advancePercent: number;
  readonly latestAcceptableDate: string | null;
  readonly overTolerancePercent: number | null;
  readonly underTolerancePercent: number | null;
  readonly fabricResponsibilityId: number | null;
  readonly statusId: number;
  readonly lines: readonly PoLineDto[];
  readonly statusHistory: readonly PoStatusHistoryDto[];
}

export interface PoSummaryDto {
  readonly id: number;
  readonly poNo: string;
  readonly vendorId: number;
  readonly statusId: number;
  readonly expectedDeliveryDate: string;
}

export interface CreatePoValue {
  readonly vendorId: number;
  readonly styleId: number;
  readonly unitCost: number;
  readonly expectedDeliveryDate: string;
  readonly lines: readonly PoLineDto[];
  readonly paymentTermId?: number | null;
  readonly advancePercent?: number | null;
}

export interface UpdatePoValue {
  readonly unitCost: number;
  readonly expectedDeliveryDate: string;
  readonly paymentTermId: number;
  readonly advancePercent: number;
  readonly lines: readonly PoLineDto[];
  readonly latestAcceptableDate?: string | null;
  readonly overTolerancePercent?: number | null;
  readonly underTolerancePercent?: number | null;
  readonly fabricResponsibilityId?: number | null;
}

/**
 * What the create/edit form hands to the store. The payment term and advance may be blank (use the
 * vendor's / the PO's own); the effect fills them in from the PO.
 */
export interface PoFormDraft {
  readonly vendorId: number;
  readonly styleId: number;
  readonly unitCost: number;
  readonly expectedDeliveryDate: string;
  readonly lines: readonly PoLineDto[];
  readonly paymentTermId: number | null;
  readonly advancePercent: number | null;
  readonly latestAcceptableDate: string | null;
  readonly overTolerancePercent: number | null;
  readonly underTolerancePercent: number | null;
  readonly fabricResponsibilityId: number | null;
}

export interface PoFilters {
  readonly page: number;
  readonly pageSize: number;
  readonly vendorId: number | null;
  readonly statusId: number | null;
}
