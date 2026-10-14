import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '@core/http';
import { Page } from '@shared';
import { LabelWorkFilters, LabelWorkItemDto, PrinterDto, QueueTagsValue, QueuedJobDto, ResolveWorkItemValue } from '../models';

/** HTTP only, no state (CONVENTIONS section 1). The routes are listed in docs/specs/labelling/label-work/plan.md. */
@Injectable({ providedIn: 'root' })
export class LabelWorkApiService {
  private readonly api = inject(ApiClient);

  search(filters: LabelWorkFilters): Observable<Page<LabelWorkItemDto>> {
    return this.api.get<Page<LabelWorkItemDto>>('/api/labelling/work-items', {
      status: filters.status === 'all' ? undefined : filters.status,
      priceFlag: filters.priceFlagOnly ? true : undefined,
      page: filters.page,
      pageSize: filters.pageSize,
    });
  }

  resolve(id: number, value: ResolveWorkItemValue): Observable<LabelWorkItemDto> {
    return this.api.post<LabelWorkItemDto>(`/api/labelling/work-items/${id}/resolve`, value);
  }

  queueTags(id: number, value: QueueTagsValue): Observable<QueuedJobDto> {
    return this.api.post<QueuedJobDto>(`/api/labelling/work-items/${id}/print-jobs`, value);
  }

  listPrinters(): Observable<readonly PrinterDto[]> {
    return this.api.get<readonly PrinterDto[]>('/api/labelling/printers');
  }

  /** Where the PDF preview of the tag is served (opened in a new tab). */
  previewUrl(id: number): string {
    return this.api.url(`/api/labelling/work-items/${id}/tag-preview`);
  }
}
