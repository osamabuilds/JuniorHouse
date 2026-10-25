import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '@core/http';
import { Page } from '@shared';
import { ListFilters, MissRow, SynonymFormValue, SynonymGroup } from '../models';

const ROOT = '/api/admin/storefront/search';

@Injectable({ providedIn: 'root' })
export class SearchAdminApiService {
  private readonly api = inject(ApiClient);

  listSynonyms(filters: ListFilters): Observable<Page<SynonymGroup>> {
    return this.api.get<Page<SynonymGroup>>(`${ROOT}/synonyms`, { page: filters.page, pageSize: filters.pageSize });
  }

  createSynonym(value: SynonymFormValue): Observable<SynonymGroup> {
    return this.api.post<SynonymGroup>(`${ROOT}/synonyms`, value);
  }

  updateSynonym(id: number, rowVersion: number, value: SynonymFormValue): Observable<SynonymGroup> {
    return this.api.put<SynonymGroup>(`${ROOT}/synonyms/${id}`, { rowVersion, ...value });
  }

  deleteSynonym(id: number): Observable<void> {
    return this.api.delete<void>(`${ROOT}/synonyms/${id}`);
  }

  listMisses(filters: ListFilters): Observable<Page<MissRow>> {
    return this.api.get<Page<MissRow>>(`${ROOT}/misses`, { page: filters.page, pageSize: filters.pageSize });
  }
}
