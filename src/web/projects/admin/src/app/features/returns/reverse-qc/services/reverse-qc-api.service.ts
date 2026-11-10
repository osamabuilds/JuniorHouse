import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '@core/http';
import { Page } from '@shared';
import { DecideRequest, ReceiveRequest, ReturnCaseDetail, ReturnCaseSummary } from '../models';

/** HTTP only, no state (CONVENTIONS section 1). The staff member comes from the session, never from the body (RQC-12). */
@Injectable({ providedIn: 'root' })
export class ReverseQcApiService {
  private readonly api = inject(ApiClient);

  cases(page: number, pageSize: number, status: string): Observable<Page<ReturnCaseSummary>> {
    return this.api.get<Page<ReturnCaseSummary>>('/api/returns/cases', status ? { page, pageSize, status } : { page, pageSize });
  }

  case(id: number): Observable<ReturnCaseDetail> {
    return this.api.get<ReturnCaseDetail>(`/api/returns/cases/${id}`);
  }

  receive(id: number, request: ReceiveRequest): Observable<ReturnCaseDetail> {
    return this.api.post<ReturnCaseDetail>(`/api/returns/cases/${id}/receive`, request);
  }

  decide(id: number, lineId: number, request: DecideRequest): Observable<ReturnCaseDetail> {
    return this.api.post<ReturnCaseDetail>(`/api/returns/cases/${id}/lines/${lineId}/decide`, request);
  }

  complete(id: number): Observable<ReturnCaseDetail> {
    return this.api.post<ReturnCaseDetail>(`/api/returns/cases/${id}/complete`, {});
  }
}
