import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '@core/http';
import { Page } from '@shared';
import { ConfirmPutawayValue, PutawayItemRowDto, PutawayRecordDto, ScanLookupDto } from '../models';

/** HTTP only, no state (CONVENTIONS section 1). The routes are listed in docs/specs/warehouse/putaway/plan.md. */
@Injectable({ providedIn: 'root' })
export class PutawayApiService {
  private readonly api = inject(ApiClient);

  items(search: string, page: number, pageSize: number): Observable<Page<PutawayItemRowDto>> {
    return this.api.get<Page<PutawayItemRowDto>>('/api/warehouse/putaway/items', { onlyOpen: true, search: search || undefined, page, pageSize });
  }

  /** The hot path (NFR-SC-01): one small GET per scan. A 404 means the barcode is unknown. */
  scan(gtin: string): Observable<ScanLookupDto> {
    return this.api.get<ScanLookupDto>(`/api/warehouse/putaway/scan/${encodeURIComponent(gtin)}`);
  }

  confirm(itemId: number, value: ConfirmPutawayValue): Observable<PutawayRecordDto> {
    return this.api.post<PutawayRecordDto>(`/api/warehouse/putaway/items/${itemId}/confirm`, value);
  }

  records(page: number, pageSize: number): Observable<Page<PutawayRecordDto>> {
    return this.api.get<Page<PutawayRecordDto>>('/api/warehouse/putaway/records', { page, pageSize });
  }
}
