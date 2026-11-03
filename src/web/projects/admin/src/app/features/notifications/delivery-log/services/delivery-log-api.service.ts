import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '@core/http';
import { Page } from '@shared';
import { DeliveryDetail, DeliveryFilters, DeliveryLogRow, DeliverySummary, ResendResult } from '../models';

/** HTTP only, no state (CONVENTIONS section 1). The routes are listed in docs/specs/notifications/plan.md. */
@Injectable({ providedIn: 'root' })
export class DeliveryLogApiService {
  private readonly api = inject(ApiClient);

  list(filters: DeliveryFilters, page: number, pageSize: number): Observable<Page<DeliveryLogRow>> {
    return this.api.get<Page<DeliveryLogRow>>('/api/admin/notifications', {
      page,
      pageSize,
      ...(filters.status ? { status: filters.status } : {}),
      ...(filters.channel ? { channel: filters.channel } : {}),
      ...(filters.kind ? { kind: filters.kind } : {}),
      ...(filters.deadLettersOnly ? { deadLetters: true } : {}),
      ...(filters.orderId !== null ? { orderId: filters.orderId } : {}),
      ...(filters.search.trim() ? { search: filters.search.trim() } : {}),
    });
  }

  summary(): Observable<DeliverySummary> {
    return this.api.get<DeliverySummary>('/api/admin/notifications/summary');
  }

  get(id: number): Observable<DeliveryDetail> {
    return this.api.get<DeliveryDetail>(`/api/admin/notifications/${id}`);
  }

  resend(id: number, key: string, acknowledgeDuplicate: boolean): Observable<ResendResult> {
    return this.api.post<ResendResult>(`/api/admin/notifications/${id}/resend`, { key, acknowledgeDuplicate });
  }
}
