import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '@core/http';
import { Page } from '@shared';
import { CodOutcomeChoice, OrderDetail, OrderFilters, OrderListItem } from '../models';

/** HTTP only, no state (CONVENTIONS section 1). The routes are listed in docs/specs/orders/plan.md. */
@Injectable({ providedIn: 'root' })
export class OrdersApiService {
  private readonly api = inject(ApiClient);

  list(filters: OrderFilters, page: number, pageSize: number): Observable<Page<OrderListItem>> {
    return this.api.get<Page<OrderListItem>>('/api/admin/orders', {
      page,
      pageSize,
      ...(filters.status ? { status: filters.status } : {}),
      ...(filters.method ? { method: filters.method } : {}),
      ...(filters.search.trim() ? { search: filters.search.trim() } : {}),
      ...(filters.callDueNow ? { callDueNow: true } : {}),
      ...(filters.flagged ? { flagged: true } : {}),
    });
  }

  get(id: number): Observable<OrderDetail> {
    return this.api.get<OrderDetail>(`/api/admin/orders/${id}`);
  }

  recordOutcome(id: number, outcome: CodOutcomeChoice, note: string): Observable<OrderDetail> {
    return this.api.post<OrderDetail>(`/api/admin/orders/${id}/cod-outcome`, { outcome, note: note.trim() || null });
  }

  cancel(id: number, note: string): Observable<OrderDetail> {
    return this.api.post<OrderDetail>(`/api/admin/orders/${id}/cancel`, { note: note.trim() || null });
  }

  clearFlag(id: number): Observable<OrderDetail> {
    return this.api.post<OrderDetail>(`/api/admin/orders/${id}/clear-flag`, {});
  }
}
