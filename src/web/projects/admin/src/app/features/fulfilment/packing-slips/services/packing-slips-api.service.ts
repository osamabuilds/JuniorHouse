import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '@core/http';
import { PackingSlip, PrintResult } from '../models';

/** HTTP only, no state (CONVENTIONS section 1). The slip is built from Orders at the moment of the request. */
@Injectable({ providedIn: 'root' })
export class PackingSlipsApiService {
  private readonly api = inject(ApiClient);

  get(orderId: number): Observable<PackingSlip> {
    return this.api.get<PackingSlip>(`/api/fulfilment/orders/${orderId}/slip`);
  }

  print(orderId: number): Observable<PrintResult> {
    return this.api.post<PrintResult>(`/api/fulfilment/orders/${orderId}/slip/print`, {});
  }
}
