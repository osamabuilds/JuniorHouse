import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '@core/http';
import { Page } from '@shared';
import { ExceptionRow, RaiseExceptionRequest, ResolveAction } from '../models';

/** HTTP only, no state (CONVENTIONS section 1). The routes are listed in docs/specs/fulfilment/plan.md. */
@Injectable({ providedIn: 'root' })
export class ExceptionsApiService {
  private readonly api = inject(ApiClient);

  list(openOnly: boolean, page: number, pageSize: number): Observable<Page<ExceptionRow>> {
    return this.api.get<Page<ExceptionRow>>('/api/fulfilment/exceptions', { openOnly, page, pageSize });
  }

  /** The picker flags a mismatch instead of force-completing (PEX-1). The staff member comes from the session, never from the body. */
  raise(orderId: number, request: RaiseExceptionRequest): Observable<ExceptionRow> {
    return this.api.post<ExceptionRow>(`/api/fulfilment/orders/${orderId}/pick/exceptions`, request);
  }

  resolve(id: number, action: ResolveAction): Observable<ExceptionRow> {
    return this.api.post<ExceptionRow>(`/api/fulfilment/exceptions/${id}/resolve`, { action });
  }
}
