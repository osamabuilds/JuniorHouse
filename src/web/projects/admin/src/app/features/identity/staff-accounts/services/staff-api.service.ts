import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '@core/http';
import { Page } from '@shared';
import { CreateStaffValue, RoleDto, StaffDto, StaffFilters, TemporaryPasswordDto } from '../models';

@Injectable({ providedIn: 'root' })
export class StaffApiService {
  private readonly api = inject(ApiClient);

  search(filters: StaffFilters): Observable<Page<StaffDto>> {
    return this.api.get<Page<StaffDto>>('/api/identity/staff', {
      search: filters.searchText || undefined,
      isActive: filters.activeOnly ? true : undefined,
      page: filters.page,
      pageSize: filters.pageSize,
    });
  }

  getById(id: number): Observable<StaffDto> {
    return this.api.get<StaffDto>(`/api/identity/staff/${id}`);
  }

  roles(): Observable<readonly RoleDto[]> {
    return this.api.get<readonly RoleDto[]>('/api/identity/roles');
  }

  create(value: CreateStaffValue): Observable<TemporaryPasswordDto> {
    return this.api.post<TemporaryPasswordDto>('/api/identity/staff', value);
  }

  deactivate(id: number): Observable<void> {
    return this.api.post<void>(`/api/identity/staff/${id}/deactivate`, {});
  }

  reactivate(id: number): Observable<void> {
    return this.api.post<void>(`/api/identity/staff/${id}/reactivate`, {});
  }

  assignRoles(id: number, roleIds: readonly number[]): Observable<void> {
    return this.api.put<void>(`/api/identity/staff/${id}/roles`, { roleIds });
  }

  resetPassword(id: number): Observable<TemporaryPasswordDto> {
    return this.api.post<TemporaryPasswordDto>(`/api/identity/staff/${id}/reset-password`, {});
  }
}
