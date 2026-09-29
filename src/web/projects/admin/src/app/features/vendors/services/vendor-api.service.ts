import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '@core/http';
import { CreateVendorValue, UpdateVendorValue, VendorDto, VendorSummaryDto } from '../models';

@Injectable({ providedIn: 'root' })
export class VendorApiService {
  private readonly api = inject(ApiClient);

  search(searchText: string, specialisationId: number | null, activeOnly: boolean): Observable<VendorSummaryDto[]> {
    return this.api.get<VendorSummaryDto[]>('/api/vendors', {
      search: searchText || undefined,
      specialisationId: specialisationId ?? undefined,
      activeOnly,
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
