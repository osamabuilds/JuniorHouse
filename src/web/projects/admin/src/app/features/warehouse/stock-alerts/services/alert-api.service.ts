import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '@core/http';
import { Page } from '@shared';
import { AlertFilters, AlertRowDto, OpenAlertCountDto, ThresholdFilters, ThresholdRowDto } from '../models';

/** HTTP only, no state (CONVENTIONS section 1). Delivery by email or WhatsApp is not built yet (decision D6): alerts live in the app. */
@Injectable({ providedIn: 'root' })
export class AlertApiService {
  private readonly api = inject(ApiClient);

  alerts(filters: AlertFilters): Observable<Page<AlertRowDto>> {
    return this.api.get<Page<AlertRowDto>>('/api/warehouse/alerts', {
      levelId: filters.level === 'all' ? undefined : filters.level === 'LOW' ? 1 : 2,
      acknowledged: filters.unacknowledgedOnly ? false : undefined,
      page: filters.page,
      pageSize: filters.pageSize,
    });
  }

  openCount(): Observable<OpenAlertCountDto> {
    return this.api.get<OpenAlertCountDto>('/api/warehouse/alerts/open-count');
  }

  thresholds(filters: ThresholdFilters): Observable<Page<ThresholdRowDto>> {
    return this.api.get<Page<ThresholdRowDto>>('/api/warehouse/alerts/thresholds', {
      search: filters.searchText || undefined,
      page: filters.page,
      pageSize: filters.pageSize,
    });
  }

  acknowledge(id: number, actingPerson: string): Observable<AlertRowDto> {
    return this.api.post<AlertRowDto>(`/api/warehouse/alerts/${id}/acknowledge`, { actingPerson });
  }

  setThreshold(variantRefId: number, quantity: number, actingPerson: string): Observable<ThresholdRowDto> {
    return this.api.put<ThresholdRowDto>(`/api/warehouse/alerts/thresholds/${variantRefId}`, { quantity, actingPerson });
  }
}
