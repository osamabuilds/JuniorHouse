import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '@core/http';
import { Page } from '@shared';
import { CreateVendorValue, UpdateVendorValue, VendorDto, VendorFilters, VendorSummaryDto } from '../models';

@Injectable({ providedIn: 'root' })
export class VendorApiService {
  private readonly api = inject(ApiClient);

  search(filters: VendorFilters): Observable<Page<VendorSummaryDto>> {
    return this.api.get<Page<VendorSummaryDto>>('/api/vendors', {
      search: filters.searchText || undefined,
      specialisationId: filters.specialisationId ?? undefined,
      activeOnly: filters.activeOnly,
      page: filters.page,
      pageSize: filters.pageSize,
    });
  }

  getById(id: number): Observable<VendorDto> {
    return this.api.get<VendorDto>(`/api/vendors/${id}`);
  }

  create(value: CreateVendorValue): Observable<VendorDto> {
    return this.api.post<VendorDto>('/api/vendors', value);
  }

  update(id: number, value: UpdateVendorValue): Observable<VendorDto> {
    return this.api.put<VendorDto>(`/api/vendors/${id}`, value);
  }
}
