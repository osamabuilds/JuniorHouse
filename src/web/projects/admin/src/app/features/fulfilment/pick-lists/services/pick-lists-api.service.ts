import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '@core/http';
import { Page } from '@shared';
import { CompleteResult, PickList, QueueItem, ScanOutcome } from '../models';

/** HTTP only, no state (CONVENTIONS section 1). Each scan is one small request (APP-6). The staff member comes from the session. */
@Injectable({ providedIn: 'root' })
export class PickListsApiService {
  private readonly api = inject(ApiClient);

  queue(page: number, pageSize: number): Observable<Page<QueueItem>> {
    return this.api.get<Page<QueueItem>>('/api/fulfilment/queue', { stage: 'pick', page, pageSize });
  }

  get(orderId: number): Observable<PickList> {
    return this.api.get<PickList>(`/api/fulfilment/orders/${orderId}`);
  }

  claim(orderId: number): Observable<PickList> {
    return this.api.post<PickList>(`/api/fulfilment/orders/${orderId}/pick/claim`, {});
  }

  release(orderId: number): Observable<{ released: boolean }> {
    return this.api.post<{ released: boolean }>(`/api/fulfilment/orders/${orderId}/pick/release`, {});
  }

  scan(orderId: number, code: string): Observable<ScanOutcome> {
    return this.api.post<ScanOutcome>(`/api/fulfilment/orders/${orderId}/pick/scan`, { code });
  }

  complete(orderId: number): Observable<CompleteResult> {
    return this.api.post<CompleteResult>(`/api/fulfilment/orders/${orderId}/pick/complete`, {});
  }
}
