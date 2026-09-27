import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '../core/api-client';

export interface VendorDto {
  readonly id: number;
  readonly name: string;
  readonly contactName: string;
  readonly contactPhone: string;
  readonly contactEmail: string | null;
  readonly cityId: number;
  readonly paymentTermId: number;
  readonly onTimePercent: number | null;
  readonly onQuantityPercent: number | null;
  readonly defectRatePercent: number | null;
  readonly isActive: boolean;
  readonly specialisationIds: readonly number[];
}

export interface VendorSummaryDto {
  readonly id: number;
  readonly name: string;
  readonly cityId: number;
  readonly isActive: boolean;
}

export interface CreateVendorValue {
  readonly name: string;
  readonly contactName: string;
  readonly contactPhone: string;
  readonly contactEmail: string | null;
  readonly cityId: number;
  readonly paymentTermId: number;
  readonly specialisationIds: readonly number[];
}

export interface UpdateVendorValue extends Omit<CreateVendorValue, 'name'> {
  readonly isActive: boolean;
}

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
