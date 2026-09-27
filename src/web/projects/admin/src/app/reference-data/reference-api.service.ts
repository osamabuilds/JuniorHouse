import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '../core/api-client';

export interface LookupDto {
  readonly id: number;
  readonly code: string;
  readonly name: string;
  readonly description: string | null;
  readonly sortSeq: number;
  readonly isActive: boolean;
  readonly parentCategoryId: number | null;
  readonly defaultAdvancePercent: number | null;
}

export interface LookupFormValue {
  readonly code: string;
  readonly name: string;
  readonly description: string | null;
  readonly sortSeq: number;
  readonly parentCategoryId?: number | null;
  readonly defaultAdvancePercent?: number | null;
}

@Injectable({ providedIn: 'root' })
export class ReferenceApiService {
  private readonly api = inject(ApiClient);

  list(routeSegment: string, includeInactive: boolean): Observable<LookupDto[]> {
    return this.api.get<LookupDto[]>(`/api/ref/${routeSegment}`, { includeInactive });
  }

  create(routeSegment: string, value: LookupFormValue): Observable<LookupDto> {
    return this.api.post<LookupDto>(`/api/ref/${routeSegment}`, value);
  }

  update(routeSegment: string, id: number, value: Omit<LookupFormValue, 'code'>): Observable<LookupDto> {
    return this.api.put<LookupDto>(`/api/ref/${routeSegment}/${id}`, value);
  }

  retire(routeSegment: string, id: number): Observable<void> {
    return this.api.post<void>(`/api/ref/${routeSegment}/${id}/retire`, {});
  }
}
