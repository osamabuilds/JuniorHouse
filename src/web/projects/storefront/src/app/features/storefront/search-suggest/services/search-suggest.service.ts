import { Injectable } from '@angular/core';
import { Observable, of } from 'rxjs';
import { ProductSuggestion, mockProducts } from '../../../../shared/data/mock-products';

/** Mock-backed until the search API exists (search feature); the real service will call GET /api/storefront/suggest. */
@Injectable({ providedIn: 'root' })
export class SearchSuggestService {
  suggest(query: string): Observable<readonly ProductSuggestion[]> {
    const q = query.toLowerCase();
    return of(mockProducts.filter((p) => p.name.toLowerCase().includes(q)));
  }
}
