import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '@core/http';
import { Page } from '@shared';
import { CodAwaitingRow, CodExceptionRow, CodSummary, RecordRemittanceRequest, RecordResult, RemittanceDetail, RemittanceLine, RemittanceSummary } from '../models';

/** HTTP only, no state (CONVENTIONS section 1). The staff member comes from the session, never from the body (COD-15). */
@Injectable({ providedIn: 'root' })
export class CodApiService {
  private readonly api = inject(ApiClient);

  summary(): Observable<CodSummary> {
    return this.api.get<CodSummary>('/api/shipping/cod/summary');
  }

  awaiting(page: number, pageSize: number, overdueOnly: boolean): Observable<Page<CodAwaitingRow>> {
    return this.api.get<Page<CodAwaitingRow>>('/api/shipping/cod/awaiting', { page, pageSize, overdueOnly });
  }

  exceptions(page: number, pageSize: number): Observable<Page<CodExceptionRow>> {
    return this.api.get<Page<CodExceptionRow>>('/api/shipping/cod/exceptions', { page, pageSize });
  }

  remittances(page: number, pageSize: number): Observable<Page<RemittanceSummary>> {
    return this.api.get<Page<RemittanceSummary>>('/api/shipping/cod/remittances', { page, pageSize });
  }

  remittance(id: number): Observable<RemittanceDetail> {
    return this.api.get<RemittanceDetail>(`/api/shipping/cod/remittances/${id}`);
  }

  record(request: RecordRemittanceRequest): Observable<RecordResult> {
    return this.api.post<RecordResult>('/api/shipping/cod/remittances', request);
  }

  resolve(lineId: number, note: string): Observable<RemittanceLine> {
    return this.api.post<RemittanceLine>(`/api/shipping/cod/lines/${lineId}/resolve`, { note });
  }
}
