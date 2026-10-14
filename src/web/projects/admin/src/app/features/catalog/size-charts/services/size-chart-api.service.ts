import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '@core/http';
import { Page } from '@shared';
import { SizeChartDto, SizeChartFilters, SizeChartFormValue, SizeChartLineInput, SizeChartRowDto } from '../models';

/** HTTP only, no state (CONVENTIONS section 1). The routes are listed in docs/specs/catalog/size-charts/plan.md. */
@Injectable({ providedIn: 'root' })
export class SizeChartApiService {
  private readonly api = inject(ApiClient);

  list(filters: SizeChartFilters): Observable<Page<SizeChartRowDto>> {
    return this.api.get<Page<SizeChartRowDto>>('/api/catalog/size-charts', { page: filters.page, pageSize: filters.pageSize });
  }

  get(id: number): Observable<SizeChartDto> {
    return this.api.get<SizeChartDto>(`/api/catalog/size-charts/${id}`);
  }

  create(value: SizeChartFormValue, actingPerson: string): Observable<SizeChartDto> {
    return this.api.post<SizeChartDto>('/api/catalog/size-charts', { ...value, actingPerson });
  }

  update(id: number, name: string, lines: readonly SizeChartLineInput[], version: number, actingPerson: string): Observable<SizeChartDto> {
    return this.api.put<SizeChartDto>(`/api/catalog/size-charts/${id}`, { name, lines, version, actingPerson });
  }

  delete(id: number, actingPerson: string): Observable<void> {
    return this.api.delete<void>(`/api/catalog/size-charts/${id}?actingPerson=${encodeURIComponent(actingPerson)}`);
  }
}
