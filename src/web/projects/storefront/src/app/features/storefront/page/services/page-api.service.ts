import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, of } from 'rxjs';
import { ContentPage } from '../../../../shared/models/site-settings.model';

export type PageResult =
  | { readonly status: 'ok'; readonly page: ContentPage }
  | { readonly status: 'notFound' }
  | { readonly status: 'gone' }
  | { readonly status: 'error' };

/** HTTP only: one published page by slug. 404 and 410 are results, not exceptions; anything else is an error. */
@Injectable({ providedIn: 'root' })
export class PageApi {
  private readonly http = inject(HttpClient);

  get(slug: string): Observable<PageResult> {
    return this.http.get<ContentPage>(`/api/storefront/pages/${encodeURIComponent(slug)}`).pipe(
      map((page): PageResult => ({ status: 'ok', page })),
      catchError((e: unknown): Observable<PageResult> => {
        const status = e instanceof HttpErrorResponse ? e.status : 0;
        return of(status === 404 ? { status: 'notFound' } : status === 410 ? { status: 'gone' } : { status: 'error' });
      }),
    );
  }
}
