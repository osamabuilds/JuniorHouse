import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '@core/http';
import { Page } from '@shared';
import { DiscountCodeDto, DiscountCodeInput } from '../models';

/** HTTP only, no state (CONVENTIONS section 1). The routes are listed in docs/specs/cart/plan.md. */
@Injectable({ providedIn: 'root' })
export class DiscountCodesApiService {
  private readonly api = inject(ApiClient);

  list(page: number, pageSize: number): Observable<Page<DiscountCodeDto>> {
    return this.api.get<Page<DiscountCodeDto>>('/api/discounts', { page, pageSize });
  }

  create(input: DiscountCodeInput): Observable<DiscountCodeDto> {
    return this.api.post<DiscountCodeDto>('/api/discounts', input);
  }

  update(id: number, input: DiscountCodeInput): Observable<DiscountCodeDto> {
    return this.api.put<DiscountCodeDto>(`/api/discounts/${id}`, input);
  }

  setActive(id: number, active: boolean): Observable<DiscountCodeDto> {
    return this.api.put<DiscountCodeDto>(`/api/discounts/${id}/active`, { active });
  }
}
