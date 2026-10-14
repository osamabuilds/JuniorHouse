import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '@core/http';
import { Page } from '@shared';
import { BinFilters, BinRowDto, CreateBinValue } from '../models';

/** HTTP only, no state (CONVENTIONS section 1). The routes are listed in docs/specs/warehouse/bins/plan.md. */
@Injectable({ providedIn: 'root' })
export class BinApiService {
  private readonly api = inject(ApiClient);

  search(filters: BinFilters): Observable<Page<BinRowDto>> {
    return this.api.get<Page<BinRowDto>>('/api/warehouse/bins', {
      zone: filters.zone || undefined,
      page: filters.page,
      pageSize: filters.pageSize,
    });
  }

  create(value: CreateBinValue): Observable<BinRowDto> {
    return this.api.post<BinRowDto>('/api/warehouse/bins', value);
  }

  setActive(id: number, active: boolean, actingPerson: string): Observable<BinRowDto> {
    return this.api.post<BinRowDto>(`/api/warehouse/bins/${id}/active`, { active, actingPerson });
  }
}
