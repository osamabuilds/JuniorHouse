import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, of } from 'rxjs';
import { NO_RECOMMENDATIONS, ProductRecommendations } from '../models/product-recommendations.model';

const URL = '/api/storefront/products/recommendations';

/**
 * HTTP only. A failed call, a 404 or an odd answer is "no recommendations", never an error: the product page must render and be complete
 * without them (REC-10, NFR-FT-08). One request per product.
 */
@Injectable({ providedIn: 'root' })
export class ProductRecommendationsApi {
  private readonly http = inject(HttpClient);

  get(path: string): Observable<ProductRecommendations> {
    return this.http.get<Partial<ProductRecommendations> | null>(URL, { params: new HttpParams().set('path', path) }).pipe(
      map((r) => ({ similar: Array.isArray(r?.similar) ? r.similar : [], trending: Array.isArray(r?.trending) ? r.trending : [] })),
      catchError((): Observable<ProductRecommendations> => of(NO_RECOMMENDATIONS)),
    );
  }
}
