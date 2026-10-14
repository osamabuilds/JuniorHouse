import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '@core/http';
import { Page } from '@shared';
import { BinAgeDto, CountReasonInput, CountReviewDto, CycleCountDto, CycleCountRowDto, SubmitLinesValue } from '../models';

/** HTTP only, no state (CONVENTIONS section 1). The routes are listed in docs/specs/warehouse/cycle-counts/plan.md. */
@Injectable({ providedIn: 'root' })
export class CycleCountApiService {
  private readonly api = inject(ApiClient);

  list(status: string | null, page: number, pageSize: number): Observable<Page<CycleCountRowDto>> {
    return this.api.get<Page<CycleCountRowDto>>('/api/warehouse/cycle-counts', {
      statusId: status === 'OPEN' ? 1 : status === 'CLOSED' ? 2 : status === 'CANCELLED' ? 3 : undefined,
      page,
      pageSize,
    });
  }

  binAge(zone: string, page: number, pageSize: number): Observable<Page<BinAgeDto>> {
    return this.api.get<Page<BinAgeDto>>('/api/warehouse/cycle-counts/bin-age', { zone: zone || undefined, page, pageSize });
  }

  create(binIds: readonly number[], actingPerson: string): Observable<CycleCountDto> {
    return this.api.post<CycleCountDto>('/api/warehouse/cycle-counts', { binIds, actingPerson });
  }

  /** The blind read: bins and what has been counted, never a system quantity (CC-2). */
  get(id: number): Observable<CycleCountDto> {
    return this.api.get<CycleCountDto>(`/api/warehouse/cycle-counts/${id}`);
  }

  submitLines(id: number, value: SubmitLinesValue): Observable<CycleCountDto> {
    return this.api.put<CycleCountDto>(`/api/warehouse/cycle-counts/${id}/lines`, value);
  }

  review(id: number): Observable<CountReviewDto> {
    return this.api.get<CountReviewDto>(`/api/warehouse/cycle-counts/${id}/review`);
  }

  confirm(id: number, reasons: readonly CountReasonInput[], actingPerson: string): Observable<CycleCountDto> {
    return this.api.post<CycleCountDto>(`/api/warehouse/cycle-counts/${id}/confirm`, { reasons, actingPerson });
  }

  cancel(id: number, actingPerson: string): Observable<CycleCountDto> {
    return this.api.post<CycleCountDto>(`/api/warehouse/cycle-counts/${id}/cancel`, { actingPerson });
  }
}
