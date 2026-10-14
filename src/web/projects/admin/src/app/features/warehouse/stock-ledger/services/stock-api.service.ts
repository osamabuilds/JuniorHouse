import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '@core/http';
import { Page } from '@shared';
import { StockFilters, StockMovementDto, StockVariantRowDto } from '../models';

/** HTTP only, no state (CONVENTIONS section 1). Every route is a GET: stock only changes through putaway, adjustments and counts (LED-10). */
@Injectable({ providedIn: 'root' })
export class StockApiService {
  private readonly api = inject(ApiClient);

  search(filters: StockFilters): Observable<Page<StockVariantRowDto>> {
    return this.api.get<Page<StockVariantRowDto>>('/api/warehouse/stock', {
      search: filters.searchText || undefined,
      page: filters.page,
      pageSize: filters.pageSize,
    });
  }

  movements(variantRefId: number, page: number, pageSize: number): Observable<Page<StockMovementDto>> {
    return this.api.get<Page<StockMovementDto>>('/api/warehouse/stock/movements', { variantId: variantRefId, page, pageSize });
  }
}
