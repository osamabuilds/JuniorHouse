import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '@core/http';
import { Page } from '@shared';
import { NotifyDemandRow } from '../models';

/** HTTP only, no state (CONVENTIONS section 1). The route is listed in docs/specs/customers/notify-me/plan.md. */
@Injectable({ providedIn: 'root' })
export class NotifyDemandApiService {
  private readonly api = inject(ApiClient);

  list(page: number, pageSize: number): Observable<Page<NotifyDemandRow>> {
    return this.api.get<Page<NotifyDemandRow>>('/api/admin/customers/notify-demand', { page, pageSize });
  }
}
