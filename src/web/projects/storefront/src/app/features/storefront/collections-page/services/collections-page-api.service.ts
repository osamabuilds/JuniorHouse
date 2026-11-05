import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, of } from 'rxjs';
import { CollectionsPageData, CollectionsPageResult } from '../models/collections-page.model';

const URL = '/api/storefront/collections';

/** HTTP only. A failure is a result (status error), never an exception into the page. */
@Injectable({ providedIn: 'root' })
export class CollectionsPageApi {
  private readonly http = inject(HttpClient);

  get(page: number): Observable<CollectionsPageResult> {
    return this.http.get<CollectionsPageData>(URL, { params: new HttpParams().set('page', String(page)) }).pipe(
      map((data): CollectionsPageResult => ({ status: 'ok', page: data })),
      catchError((): Observable<CollectionsPageResult> => of({ status: 'error' })),
    );
  }
}
