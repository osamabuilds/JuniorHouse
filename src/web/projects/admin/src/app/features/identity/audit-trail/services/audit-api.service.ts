import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '@core/http';
import { Page } from '@shared';
import { AuditEntryDto, AuditFilters } from '../models';

@Injectable({ providedIn: 'root' })
export class AuditApiService {
  private readonly api = inject(ApiClient);

  search(filters: AuditFilters): Observable<Page<AuditEntryDto>> {
    return this.api.get<Page<AuditEntryDto>>('/api/identity/audit', {
      actor: filters.actor || undefined,
      module: filters.module || undefined,
      entityType: filters.entityType || undefined,
      entityKey: filters.entityKey || undefined,
      operation: filters.operation || undefined,
      from: filters.fromDate ? `${filters.fromDate}T00:00:00Z` : undefined,
      to: filters.toDate ? `${filters.toDate}T23:59:59Z` : undefined,
      page: filters.page,
      pageSize: filters.pageSize,
    });
  }
}
