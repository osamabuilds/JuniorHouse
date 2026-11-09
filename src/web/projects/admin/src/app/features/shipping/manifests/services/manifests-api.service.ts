import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '@core/http';
import { Page } from '@shared';
import { ManifestDetail, ManifestSummary } from '../models';

/** HTTP only, no state (CONVENTIONS section 1). The staff member comes from the session, never from the body (MNF-9). */
@Injectable({ providedIn: 'root' })
export class ManifestsApiService {
  private readonly api = inject(ApiClient);

  list(page: number, pageSize: number): Observable<Page<ManifestSummary>> {
    return this.api.get<Page<ManifestSummary>>('/api/shipping/manifests', { page, pageSize });
  }

  get(id: number): Observable<ManifestDetail> {
    return this.api.get<ManifestDetail>(`/api/shipping/manifests/${id}`);
  }

  generate(): Observable<readonly ManifestSummary[]> {
    return this.api.post<readonly ManifestSummary[]>('/api/shipping/manifests/generate', {});
  }

  count(id: number, parcels: number): Observable<ManifestSummary> {
    return this.api.post<ManifestSummary>(`/api/shipping/manifests/${id}/count`, { parcels });
  }

  handover(id: number, shipmentId: number, handedOver: boolean): Observable<ManifestDetail> {
    return this.api.post<ManifestDetail>(`/api/shipping/manifests/${id}/handover`, { shipmentId, handedOver });
  }

  resolve(id: number, note: string): Observable<ManifestSummary> {
    return this.api.post<ManifestSummary>(`/api/shipping/manifests/${id}/resolve`, { note });
  }
}
