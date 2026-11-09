import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '@core/http';
import { ShippingLabel } from '../models';

/** HTTP only, no state (CONVENTIONS section 1). The label holds the customer phone and address, so only staff with the permission can read it (LBL-7). */
@Injectable({ providedIn: 'root' })
export class LabelsApiService {
  private readonly api = inject(ApiClient);

  get(shipmentId: number): Observable<ShippingLabel> {
    return this.api.get<ShippingLabel>(`/api/shipping/shipments/${shipmentId}/label`);
  }

  markPrinted(shipmentId: number): Observable<unknown> {
    return this.api.post<unknown>(`/api/shipping/shipments/${shipmentId}/label/printed`, {});
  }
}
