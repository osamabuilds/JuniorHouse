import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, forkJoin, map, of, switchMap } from 'rxjs';
import { EmptyState, ListingOptions, ListingPage, ListingResult } from '../models/listing.model';
import { ListingFilter, ListingScope, toApiQuery } from '../models/listing-query';

const BASE = '/api/storefront/listings';

function params(scope: ListingScope, filter: ListingFilter): HttpParams {
  let p = new HttpParams();
  for (const [k, v] of Object.entries(toApiQuery(scope, filter))) {
    for (const value of typeof v === 'string' ? [v] : v) p = p.append(k, value);
  }
  return p;
}

/** HTTP only. A 404 (unknown scope, or a page after the last) is a result, not an exception (LST-11, 14). */
@Injectable({ providedIn: 'root' })
export class ListingsApi {
  private readonly http = inject(HttpClient);

  get(scope: ListingScope, filter: ListingFilter): Observable<ListingResult> {
    return this.http.get<ListingPage>(BASE, { params: params(scope, filter) }).pipe(
      switchMap((page) =>
        forkJoin({
          options: this.http.get<ListingOptions>(`${BASE}/options`, { params: params(scope, filter) }),
          empty: page.total === 0 ? this.http.get<EmptyState>(`${BASE}/empty`, { params: params(scope, filter) }) : of(null),
        }).pipe(map(({ options, empty }): ListingResult => ({ status: 'ok', page, options, empty }))),
      ),
      catchError((e: unknown): Observable<ListingResult> => of({ status: e instanceof HttpErrorResponse && e.status === 404 ? 'notFound' : 'error' })),
    );
  }
}
