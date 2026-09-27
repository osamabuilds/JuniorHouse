import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '../core/api-client';

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
}

/** REF.PO_STS_LKP's seed order (Romp.Modules.Reference.Infrastructure/ReferenceSeedData.cs) - system-owned and stable, so it's safe to mirror here for display. */
export const PO_STATUS_LABELS: Readonly<Record<number, string>> = {
  1: 'Draft',
  2: 'Sent to Vendor',
  3: 'Acknowledged',
  4: 'Cancelled',
};

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

  send(id: number): Observable<PoDto> {
    return this.api.post<PoDto>(`/api/purchase-orders/${id}/send`, {});
  }

  acknowledge(id: number): Observable<PoDto> {
    return this.api.post<PoDto>(`/api/purchase-orders/${id}/acknowledge`, {});
  }

  cancel(id: number, cancelReasonId: number): Observable<PoDto> {
    return this.api.post<PoDto>(`/api/purchase-orders/${id}/cancel`, { cancelReasonId });
  }
}
