import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '@core/http';
import { StyleDto, StyleFormValue, StyleSummaryDto } from '../models';

@Injectable({ providedIn: 'root' })
export class CatalogApiService {
  private readonly api = inject(ApiClient);

  search(searchText: string, categoryId: number | null, activeOnly: boolean): Observable<StyleSummaryDto[]> {
    return this.api.get<StyleSummaryDto[]>('/api/catalog/styles', {
      search: searchText || undefined,
      categoryId: categoryId ?? undefined,
      activeOnly,
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
