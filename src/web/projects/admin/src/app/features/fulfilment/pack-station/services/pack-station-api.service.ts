import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '@core/http';
import { Page } from '@shared';
import { QueueItem } from '../../pick-lists';
import { PackComplete, PackScanOutcome, PackView, WeightOutcome } from '../models';

/** HTTP only, no state (CONVENTIONS section 1). The staff member comes from the session, never from the body (PAK-5). */
@Injectable({ providedIn: 'root' })
export class PackStationApiService {
  private readonly api = inject(ApiClient);

  queue(page: number, pageSize: number): Observable<Page<QueueItem>> {
    return this.api.get<Page<QueueItem>>('/api/fulfilment/queue', { stage: 'pack', page, pageSize });
  }

  get(orderId: number): Observable<PackView> {
    return this.api.get<PackView>(`/api/fulfilment/orders/${orderId}/pack`);
  }

  open(orderId: number): Observable<PackView> {
    return this.api.post<PackView>(`/api/fulfilment/orders/${orderId}/pack/open`, {});
  }

  scan(orderId: number, code: string): Observable<PackScanOutcome> {
    return this.api.post<PackScanOutcome>(`/api/fulfilment/orders/${orderId}/pack/scan`, { code });
  }

  weigh(orderId: number, grams: number): Observable<WeightOutcome> {
    return this.api.post<WeightOutcome>(`/api/fulfilment/orders/${orderId}/pack/weight`, { grams });
  }

  complete(orderId: number, note: string | null): Observable<PackComplete> {
    return this.api.post<PackComplete>(`/api/fulfilment/orders/${orderId}/pack/complete`, { note });
  }
}
