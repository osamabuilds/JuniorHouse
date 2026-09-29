import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '@core/http';

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

export interface PoRevisionCommunicationDto {
  readonly typeId: number;
  readonly channelId: number;
  readonly responderName: string;
  readonly responseDte: string;
  readonly evidence: readonly { readonly fileId: number; readonly fileName: string }[] | null;
}

export interface PoRevisionDto {
  readonly id: number;
  readonly poId: number;
  readonly revisionNumber: number;
  readonly initiatorId: number;
  readonly statusId: number;
  readonly reasonId: number;
  readonly impactNote: string;
  readonly vendorMessage: string | null;
  readonly unitCost: number;
  readonly expectedDeliveryDate: string;
  readonly latestAcceptableDate: string | null;
  readonly overTolerancePercent: number | null;
  readonly underTolerancePercent: number | null;
  readonly paymentTermId: number;
  readonly advancePercent: number;
  readonly fabricResponsibilityId: number | null;
  readonly poValueBefore: number;
  readonly poValueAfter: number;
  readonly poValueDiff: number;
  readonly advanceAmountBefore: number;
  readonly advanceAmountAfter: number;
  readonly expectedDateShiftDays: number;
  readonly latestAcceptableDateShiftDays: number | null;
  readonly quantityDiff: number;
  readonly isBeyondLatestAcceptableDate: boolean;
  readonly lines: readonly PoLineDto[];
  readonly communications: readonly PoRevisionCommunicationDto[] | null;
}

/** A vendor-visible file an amendment introduces; `content` is the file's bytes (the API's `byte[]`). */
export interface AmendmentFileAdd {
  readonly categoryId: number;
  readonly fileName: string;
  readonly content: string;
}

export interface TermsValue {
  readonly unitCost: number;
  readonly expectedDeliveryDate: string;
  readonly latestAcceptableDate: string | null;
  readonly overTolerancePercent: number | null;
  readonly underTolerancePercent: number | null;
  readonly paymentTermId: number;
  readonly advancePercent: number;
  readonly fabricResponsibilityId: number | null;
  readonly lines: readonly PoLineDto[];
}

export interface AmendmentValue extends TermsValue {
  readonly initiatorId: number;
  readonly reasonId: number;
  readonly impactNote: string;
  readonly vendorMessage: string | null;
  readonly addFiles?: readonly AmendmentFileAdd[];
  readonly retireFileIds?: readonly number[];
}

export interface CounterProposalValue extends TermsValue {
  readonly reasonId: number;
  readonly impactNote: string;
  readonly vendorMessage: string | null;
}

export interface VendorResponseValue {
  readonly outcomeTypeId: number;
  readonly revisionNumber: number;
  readonly channelId: number;
  readonly responderName: string;
  readonly responseDte: string | null;
  readonly counter: CounterProposalValue | null;
  /** Optional proof of what the vendor said (e.g. a WhatsApp screenshot); kept as internal files. */
  readonly evidence?: readonly { readonly fileName: string; readonly content: string }[];
}

export interface VendorResponseResult {
  readonly outcomeTypeId: number;
  readonly po: PoDto;
  readonly revision: PoRevisionDto | null;
  readonly suggestedCancelReasonId: number | null;
}

export interface PoFileDto {
  readonly id: number;
  readonly fileName: string;
  readonly categoryId: number;
  readonly isVendorVisible: boolean;
  readonly fileSizeBytes: number;
  readonly uploadedBy: string;
  readonly uploadedAt: string;
  readonly addedInRevisionNumber: number | null;
  readonly retiredInRevisionNumber: number | null;
}

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

/** REF.PO_STS_LKP's seed order (Romp.Modules.Reference.Infrastructure/ReferenceSeedData.cs) - system-owned and stable, so it's safe to mirror here for display. */
export const PO_STATUS_LABELS: Readonly<Record<number, string>> = {
  1: 'Draft',
  2: 'Sent to Vendor',
  3: 'Acknowledged',
  4: 'Cancelled',
};

/** REF.PO_REV_STS_LKP, AMND_INIT_LKP, PO_VNDR_COMM_TYP_LKP: system-owned seeds, mirrored for display like the PO statuses above. */
export const REVISION_STATUS_LABELS: Readonly<Record<number, string>> = {
  1: 'Pending',
  2: 'In force',
  3: 'Superseded',
  4: 'Rejected',
  5: 'Withdrawn',
};

export const INITIATOR_LABELS: Readonly<Record<number, string>> = { 1: 'Romp buyer', 2: 'Vendor' };

export const COMM_TYPE_LABELS: Readonly<Record<number, string>> = {
  1: 'Confirmed',
  2: 'Countered',
  3: 'Declined',
  4: 'Amendment request',
  5: 'Decision',
};

/** Vendor-visible categories are the first five of REF.PO_FILE_CATG_LKP; the rest are internal. */
export const FILE_CATEGORY_LABELS: Readonly<Record<number, string>> = {
  1: 'Tech Pack Spec',
  2: 'Artwork / Labels',
  3: 'Trim Card / BOM',
  4: 'Colour Standard',
  5: 'Packing Instructions',
  6: 'Cost Sheet',
  7: 'Compliance / Test Report',
  8: 'Vendor Evidence',
  9: 'Other',
};

export const TECH_PACK_SPEC = 1;

export function isVendorVisibleCategory(categoryId: number): boolean {
  return categoryId >= 1 && categoryId <= 5;
}

@Injectable({ providedIn: 'root' })
export class PoApiService {
  private readonly api = inject(ApiClient);

  search(vendorId: number | null, statusId: number | null, deliveryFrom: string | null, deliveryTo: string | null): Observable<PoSummaryDto[]> {
    return this.api.get<PoSummaryDto[]>('/api/purchase-orders', {
      vendorId: vendorId ?? undefined,
      statusId: statusId ?? undefined,
      deliveryFrom: deliveryFrom || undefined,
      deliveryTo: deliveryTo || undefined,
    });
  }

  getById(id: number): Observable<PoDto> {
    return this.api.get<PoDto>(`/api/purchase-orders/${id}`);
  }

  create(value: CreatePoValue): Observable<PoDto> {
    return this.api.post<PoDto>('/api/purchase-orders', value);
  }

  update(id: number, value: UpdatePoValue): Observable<PoDto> {
    return this.api.put<PoDto>(`/api/purchase-orders/${id}`, value);
  }

  /** `sendWithoutTechPack` is the explicit "send anyway" (AC-39); the API rejects a send with no tech pack without it. */
  send(id: number, sendWithoutTechPack = false): Observable<PoDto> {
    return this.api.post<PoDto>(`/api/purchase-orders/${id}/send?sendWithoutTechPack=${sendWithoutTechPack}`, {});
  }

  cancel(id: number, cancelReasonId: number): Observable<PoDto> {
    return this.api.post<PoDto>(`/api/purchase-orders/${id}/cancel`, { cancelReasonId });
  }

  getRevisions(id: number): Observable<PoRevisionDto[]> {
    return this.api.get<PoRevisionDto[]>(`/api/purchase-orders/${id}/revisions`);
  }

  createAmendment(id: number, value: AmendmentValue): Observable<PoRevisionDto> {
    return this.api.post<PoRevisionDto>(`/api/purchase-orders/${id}/amendments`, value);
  }

  acceptRevision(id: number, revisionNumber: number): Observable<PoDto> {
    return this.api.post<PoDto>(`/api/purchase-orders/${id}/amendments/${revisionNumber}/accept`, {});
  }

  rejectRevision(id: number, revisionNumber: number, note: string | null): Observable<PoDto> {
    return this.api.post<PoDto>(`/api/purchase-orders/${id}/amendments/${revisionNumber}/reject`, { note });
  }

  withdrawRevision(id: number, revisionNumber: number, note: string | null): Observable<PoDto> {
    return this.api.post<PoDto>(`/api/purchase-orders/${id}/amendments/${revisionNumber}/withdraw`, { note });
  }

  recordVendorResponse(id: number, value: VendorResponseValue): Observable<VendorResponseResult> {
    return this.api.post<VendorResponseResult>(`/api/purchase-orders/${id}/vendor-response`, value);
  }

  listFiles(id: number): Observable<PoFileDto[]> {
    return this.api.get<PoFileDto[]>(`/api/purchase-orders/${id}/files`);
  }

  uploadFile(id: number, categoryId: number, file: File): Observable<PoFileDto> {
    const form = new FormData();
    form.append('categoryId', String(categoryId));
    form.append('file', file, file.name);
    return this.api.postForm<PoFileDto>(`/api/purchase-orders/${id}/files`, form);
  }

  removeFile(id: number, fileId: number): Observable<void> {
    return this.api.delete<void>(`/api/purchase-orders/${id}/files/${fileId}`);
  }

  fileDownloadUrl(id: number, fileId: number): string {
    return this.api.url(`/api/purchase-orders/${id}/files/${fileId}`);
  }

  getVendorView(id: number): Observable<VendorPoViewDto> {
    return this.api.get<VendorPoViewDto>(`/api/purchase-orders/${id}/vendor-view`);
  }
}
