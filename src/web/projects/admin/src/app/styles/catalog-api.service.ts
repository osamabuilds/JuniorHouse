import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '../core/api-client';

export interface StyleTargetLineDto {
  readonly sizeId: number;
  readonly colourId: number;
  readonly targetQty: number;
}

export interface StyleDto {
  readonly id: number;
  readonly code: string;
  readonly name: string;
  readonly collectionName: string | null;
  readonly categoryId: number;
  readonly genderId: number;
  readonly ageBracketId: number;
  readonly fabricId: number;
  readonly targetUnitCost: number;
  readonly targetRetailPrice: number;
  readonly isActive: boolean;
  readonly colourIds: readonly number[];
  readonly sizeIds: readonly number[];
  readonly targetLines: readonly StyleTargetLineDto[];
}

export interface StyleSummaryDto {
  readonly id: number;
  readonly code: string;
  readonly name: string;
  readonly categoryId: number;
  readonly isActive: boolean;
}

export interface StyleFormValue {
  readonly code: string;
  readonly name: string;
  readonly collectionName: string | null;
  readonly categoryId: number;
  readonly genderId: number;
  readonly ageBracketId: number;
  readonly fabricId: number;
  readonly targetUnitCost: number;
  readonly targetRetailPrice: number;
  readonly colourIds: readonly number[];
  readonly sizeIds: readonly number[];
  readonly targetLines: readonly StyleTargetLineDto[];
}

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
