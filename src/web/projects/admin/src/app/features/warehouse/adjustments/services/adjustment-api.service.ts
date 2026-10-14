import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '@core/http';
import { Page } from '@shared';
import { AdjustmentDto, AdjustmentFilters, AdjustmentReasonDto, PostAdjustmentValue } from '../models';

/** HTTP only, no state (CONVENTIONS section 1). There is no edit or delete: a mistake is fixed by a new, opposite adjustment (ADJ-6). */
@Injectable({ providedIn: 'root' })
export class AdjustmentApiService {
  private readonly api = inject(ApiClient);

  search(filters: AdjustmentFilters): Observable<Page<AdjustmentDto>> {
    return this.api.get<Page<AdjustmentDto>>('/api/warehouse/adjustments', {
      reasonId: filters.reasonId ?? undefined,
      page: filters.page,
      pageSize: filters.pageSize,
    });
  }

  reasons(): Observable<readonly AdjustmentReasonDto[]> {
    return this.api.get<readonly AdjustmentReasonDto[]>('/api/warehouse/adjustments/reasons');
  }

  post(value: PostAdjustmentValue): Observable<AdjustmentDto> {
    return this.api.post<AdjustmentDto>('/api/warehouse/adjustments', value);
  }
}
