import { PoLineDto } from './po.model';

export interface VendorPoViewDto {
  readonly poNo: string;
  readonly vendorName: string;
  readonly styleId: number;
  readonly statusId: number;
  readonly revisionNumber: number;
  readonly unitCost: number;
  readonly expectedDeliveryDate: string;
  readonly latestAcceptableDate: string | null;
  readonly overTolerancePercent: number | null;
  readonly underTolerancePercent: number | null;
  readonly paymentTermId: number;
  readonly advancePercent: number;
  readonly fabricResponsibilityId: number | null;
  readonly lines: readonly PoLineDto[];
  readonly files: readonly { readonly id: number; readonly fileName: string; readonly categoryId: number }[];
  readonly pendingRevision: {
    readonly revisionNumber: number;
    readonly initiatorId: number;
    readonly vendorMessage: string | null;
    readonly unitCost: number;
    readonly expectedDeliveryDate: string;
    readonly latestAcceptableDate: string | null;
  } | null;
}
