import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { ApiClient } from '@core/http';
import { Page } from '@shared';
import {
  ContentPage,
  ContactSettings,
  DeliveryAndExchange,
  DraftPart,
  DraftPatch,
  DraftSettings,
  PageFilters,
  PageRow,
  PageStatus,
  PageValue,
  PublishResult,
  Saved,
  SnapshotRow,
} from '../models';

const ROOT = '/api/admin/storefront-settings';

/** HTTP only, no state (CONVENTIONS section 1). The routes are listed in docs/specs/storefront/storefront-settings/plan.md. */
@Injectable({ providedIn: 'root' })
export class StorefrontSettingsApiService {
  private readonly api = inject(ApiClient);

  getDraft(): Observable<DraftSettings> {
    return this.api.get<DraftSettings>(`${ROOT}/draft`);
  }

  /** Saves one part of the draft, quoting the draft version the admin loaded (ST-14). Returns the new version and the part as stored. */
  save(part: DraftPart, expectedVersion: number): Observable<{ version: number; patch: DraftPatch }> {
    switch (part.part) {
      case 'navigation':
        return this.api
          .put<Saved<DraftSettings['navigation']>>(`${ROOT}/navigation`, { expectedVersion, items: part.items })
          .pipe(map((saved) => ({ version: saved.version, patch: { navigation: saved.data } })));
      case 'ages':
        return this.api
          .put<Saved<DraftSettings['ages']>>(`${ROOT}/ages`, { expectedVersion, steps: part.steps })
          .pipe(map((saved) => ({ version: saved.version, patch: { ages: saved.data } })));
      case 'footer':
        return this.api
          .put<Saved<DraftSettings['footer']>>(`${ROOT}/footer`, { expectedVersion, columns: part.columns })
          .pipe(map((saved) => ({ version: saved.version, patch: { footer: saved.data } })));
      case 'contact':
        return this.api
          .put<Saved<ContactSettings>>(`${ROOT}/contact`, { expectedVersion, items: part.items, popularSearches: part.popularSearches })
          .pipe(map((saved) => ({ version: saved.version, patch: { items: saved.data.items, popularSearches: saved.data.popularSearches } })));
      case 'delivery':
        return this.api
          .put<Saved<DeliveryAndExchange>>(`${ROOT}/delivery`, { expectedVersion, rules: part.rules, exchange: part.exchange })
          .pipe(map((saved) => ({ version: saved.version, patch: { delivery: saved.data.rules, exchange: saved.data.exchange } })));
    }
  }

  listPages(filters: PageFilters): Observable<Page<PageRow>> {
    return this.api.get<Page<PageRow>>(`${ROOT}/pages`, {
      status: filters.status || undefined,
      page: filters.page,
      pageSize: filters.pageSize,
    });
  }

  getPage(id: number): Observable<ContentPage> {
    return this.api.get<ContentPage>(`${ROOT}/pages/${id}`);
  }

  createPage(value: PageValue): Observable<ContentPage> {
    return this.api.post<ContentPage>(`${ROOT}/pages`, value);
  }

  updatePage(id: number, rowVersion: number, value: PageValue): Observable<ContentPage> {
    return this.api.put<ContentPage>(`${ROOT}/pages/${id}`, { rowVersion, ...value });
  }

  changePageStatus(id: number, rowVersion: number, status: PageStatus): Observable<ContentPage> {
    return this.api.put<ContentPage>(`${ROOT}/pages/${id}/status`, { rowVersion, status });
  }

  listSnapshots(page: number, pageSize: number): Observable<Page<SnapshotRow>> {
    return this.api.get<Page<SnapshotRow>>(`${ROOT}/snapshots`, { page, pageSize });
  }

  publish(expectedVersion: number, note: string): Observable<PublishResult> {
    return this.api.post<PublishResult>(`${ROOT}/publish`, { expectedVersion, note });
  }

  restore(versionNo: number, note: string): Observable<PublishResult> {
    return this.api.post<PublishResult>(`${ROOT}/restore`, { versionNo, note });
  }

  /** A plain link to the preview document (the draft as the public JSON), for a new tab. */
  previewUrl(): string {
    return this.api.url(`${ROOT}/preview`);
  }
}
