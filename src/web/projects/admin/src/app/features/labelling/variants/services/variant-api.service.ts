import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '@core/http';
import { Page } from '@shared';
import { GtinCapacityDto, VariantDetailDto, VariantFilters, VariantRowDto, VoidGtinValue } from '../models';

/** HTTP only, no state (CONVENTIONS section 1). The routes are listed in docs/specs/labelling/variants/plan.md. */
@Injectable({ providedIn: 'root' })
export class VariantApiService {
  private readonly api = inject(ApiClient);

  search(filters: VariantFilters): Observable<Page<VariantRowDto>> {
    return this.api.get<Page<VariantRowDto>>('/api/labelling/variants', {
      search: filters.searchText || undefined,
      status: filters.status === 'all' ? undefined : filters.status,
      page: filters.page,
      pageSize: filters.pageSize,
    });
  }

  getById(id: number): Observable<VariantDetailDto> {
    return this.api.get<VariantDetailDto>(`/api/labelling/variants/${id}`);
  }

  voidGtin(id: number, value: VoidGtinValue): Observable<VariantDetailDto> {
    return this.api.post<VariantDetailDto>(`/api/labelling/variants/${id}/void`, value);
  }

  getCapacity(): Observable<GtinCapacityDto> {
    return this.api.get<GtinCapacityDto>('/api/labelling/capacity');
  }
}
