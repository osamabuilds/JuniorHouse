import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, of, switchMap } from 'rxjs';
import { EmptyState, ListingFilter, toCanonicalQuery } from '../../listings';
import { SearchPageData, SearchResult, SkuTarget, SuggestResult } from '../models/search.model';
import { skuCandidate, skuTargetUrl } from '../models/search-query';

const BASE = '/api/storefront/search';

/** HTTP only. A page after the last is a result (404), and so is a failure: neither throws into the page. */
@Injectable({ providedIn: 'root' })
export class SearchApi {
  private readonly http = inject(HttpClient);

  suggest(query: string): Observable<SuggestResult> {
    return this.http
      .get<SuggestResult>(`${BASE}/suggest`, { params: new HttpParams().set('q', query) })
      .pipe(catchError(() => of<SuggestResult>({ query, items: [] })));
  }

  /** An exact SKU or style code goes straight to its product (FR-06); anything else is searched by words. */
  find(query: string, filter: ListingFilter): Observable<SearchResult> {
    const code = skuCandidate(query);
    const lookup$ = code ? this.sku(code) : of<SkuTarget | null>(null);
    return lookup$.pipe(
      switchMap((target): Observable<SearchResult> =>
        target
          ? of({ status: 'redirect', url: skuTargetUrl(target) })
          : this.http.get<SearchPageData>(BASE, { params: this.params(query, filter) }).pipe(
              switchMap((page) =>
                // Only an empty answer needs category links; a page of results does not.
                (page.total === 0 ? this.suggestions() : of([])).pipe(map((suggestions): SearchResult => ({ status: 'ok', page, suggestions }))),
              ),
            ),
      ),
      catchError((e: unknown): Observable<SearchResult> => of({ status: e instanceof HttpErrorResponse && e.status === 404 ? 'notFound' : 'error' })),
    );
  }

  private sku(code: string): Observable<SkuTarget | null> {
    return this.http.get<SkuTarget>(`${BASE}/sku`, { params: new HttpParams().set('code', code) }).pipe(catchError(() => of(null)));
  }

  /** Where to go when nothing matched: the audiences and New In (the same links an empty listing offers). */
  private suggestions(): Observable<EmptyState['suggestions']> {
    return this.http
      .get<EmptyState>('/api/storefront/listings/empty', { params: new HttpParams().set('scope', 'new-in') })
      .pipe(map((e) => e.suggestions), catchError(() => of([])));
  }

  private params(query: string, filter: ListingFilter): HttpParams {
    let p = new HttpParams().set('q', query);
    for (const [k, v] of Object.entries(toCanonicalQuery(filter))) {
      for (const value of typeof v === 'string' ? [v] : v) p = p.append(k, value);
    }
    return p;
  }
}
