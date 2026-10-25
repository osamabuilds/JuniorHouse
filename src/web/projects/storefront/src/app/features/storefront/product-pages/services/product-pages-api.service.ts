import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, of } from 'rxjs';
import { ProductPageResponse, ProductPageResult } from '../models/product-page.model';
import { ProductSelection } from '../models/product-selection';

const URL = '/api/storefront/products/by-path';

/**
 * HTTP only. The API answers 200 with an outcome so the server-rendering layer can set 301, 404 or 410 itself; only a failed call is an
 * `error` (503, never cached).
 */
@Injectable({ providedIn: 'root' })
export class ProductPagesApi {
  private readonly http = inject(HttpClient);

  get(path: string, selection: ProductSelection): Observable<ProductPageResult> {
    let params = new HttpParams().set('path', path);
    if (selection.colour) params = params.set('colour', selection.colour);
    if (selection.size) params = params.set('size', selection.size);
    return this.http.get<ProductPageResponse>(URL, { params }).pipe(
      map((r): ProductPageResult => {
        switch (r.outcome) {
          case 'Found':
            return r.page ? { status: 'found', page: r.page } : { status: 'error' };
          case 'Moved':
            return r.path ? { status: 'moved', path: r.path } : { status: 'notFound' };
          case 'Gone':
            return { status: 'gone' };
          default:
            return { status: 'notFound' };
        }
      }),
      catchError((): Observable<ProductPageResult> => of({ status: 'error' })),
    );
  }
}
