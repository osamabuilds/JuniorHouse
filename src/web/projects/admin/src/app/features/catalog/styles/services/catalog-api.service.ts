import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '@core/http';
import { Page } from '@shared';
import { StyleDto, StyleFilters, StyleFormValue, StyleSummaryDto } from '../models';

@Injectable({ providedIn: 'root' })
export class CatalogApiService {
  private readonly api = inject(ApiClient);

  search(filters: StyleFilters): Observable<Page<StyleSummaryDto>> {
    return this.api.get<Page<StyleSummaryDto>>('/api/catalog/styles', {
      search: filters.searchText || undefined,
      categoryId: filters.categoryId ?? undefined,
      activeOnly: filters.activeOnly,
      page: filters.page,
      pageSize: filters.pageSize,
    });
  }

  getById(id: number): Observable<StyleDto> {
    return this.api.get<StyleDto>(`/api/catalog/styles/${id}`);
  }

  create(value: StyleFormValue): Observable<StyleDto> {
    return this.api.post<StyleDto>('/api/catalog/styles', value);
  }

  update(id: number, value: Omit<StyleFormValue, 'code'>): Observable<StyleDto> {
    return this.api.put<StyleDto>(`/api/catalog/styles/${id}`, value);
  }
}
