import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '@core/http';
import { Page } from '@shared';
import { PrintJobDto, PrintJobFilters, PrinterDto, ReprintValue } from '../models';

/** HTTP only, no state (CONVENTIONS section 1). The routes are listed in docs/specs/labelling/print-jobs/plan.md. */
@Injectable({ providedIn: 'root' })
export class PrintJobApiService {
  private readonly api = inject(ApiClient);

  /** The whole queue, or the history of one variant when a variant is chosen. */
  search(filters: PrintJobFilters): Observable<Page<PrintJobDto>> {
    const paging = { page: filters.page, pageSize: filters.pageSize };
    if (filters.variantId !== null) {
      return this.api.get<Page<PrintJobDto>>(`/api/labelling/variants/${filters.variantId}/print-history`, paging);
    }

    return this.api.get<Page<PrintJobDto>>('/api/labelling/print-jobs', {
      status: filters.status === 'all' ? undefined : filters.status,
      ...paging,
    });
  }

  retry(id: number, actingPerson: string): Observable<PrintJobDto> {
    return this.api.post<PrintJobDto>(`/api/labelling/print-jobs/${id}/retry`, { actingPerson });
  }

  cancel(id: number, actingPerson: string): Observable<PrintJobDto> {
    return this.api.post<PrintJobDto>(`/api/labelling/print-jobs/${id}/cancel`, { actingPerson });
  }

  reprint(variantId: number, value: ReprintValue): Observable<PrintJobDto> {
    return this.api.post<PrintJobDto>(`/api/labelling/variants/${variantId}/reprints`, value);
  }

  listPrinters(): Observable<readonly PrinterDto[]> {
    return this.api.get<readonly PrinterDto[]>('/api/labelling/printers');
  }
}
