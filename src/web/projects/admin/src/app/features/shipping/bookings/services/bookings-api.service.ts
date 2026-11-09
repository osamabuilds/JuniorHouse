import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '@core/http';
import { Page } from '@shared';
import { ShipmentDetail, ShipmentRow } from '../models';

/** HTTP only, no state (CONVENTIONS section 1). The staff member comes from the session, never from the body. */
@Injectable({ providedIn: 'root' })
export class BookingsApiService {
  private readonly api = inject(ApiClient);

  list(status: string, page: number, pageSize: number): Observable<Page<ShipmentRow>> {
    return this.api.get<Page<ShipmentRow>>('/api/shipping/shipments', { ...(status ? { status } : {}), page, pageSize });
  }

  get(id: number): Observable<ShipmentDetail> {
    return this.api.get<ShipmentDetail>(`/api/shipping/shipments/${id}`);
  }

  retry(id: number): Observable<{ outcome: string }> {
    return this.api.post<{ outcome: string }>(`/api/shipping/shipments/${id}/retry`, {});
  }
}
