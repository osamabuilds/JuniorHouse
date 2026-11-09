import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '@core/http';
import { Page } from '@shared';
import { QueueRow } from '../models';

/** HTTP only, no state (CONVENTIONS section 1). The staff member comes from the session, never from the body (MBQ-7). */
@Injectable({ providedIn: 'root' })
export class QueueApiService {
  private readonly api = inject(ApiClient);

  list(needsStaffOnly: boolean, page: number, pageSize: number): Observable<Page<QueueRow>> {
    return this.api.get<Page<QueueRow>>('/api/shipping/queue', { needsStaffOnly, page, pageSize });
  }

  retry(id: number): Observable<{ outcome: string }> {
    return this.api.post<{ outcome: string }>(`/api/shipping/shipments/${id}/retry`, {});
  }

  manualWaybill(id: number, courier: string, waybill: string): Observable<unknown> {
    return this.api.post<unknown>(`/api/shipping/shipments/${id}/manual-waybill`, { courier, waybill });
  }
}
