import { TestBed } from '@angular/core/testing';
import { provideEffects } from '@ngrx/effects';
import { Store, provideStore } from '@ngrx/store';
import { Subject } from 'rxjs';
import { describe, expect, it } from 'vitest';
import { ListingTile } from '../../listings/models/listing.model';
import { ProductRecommendations } from '../models/product-recommendations.model';
import { ProductRecommendationsApi } from '../services/product-recommendations-api.service';
import { ProductRecommendationsActions } from './product-recommendations.actions';
import { ProductRecommendationsEffects } from './product-recommendations.effects';
import { productRecommendationsFeature } from './product-recommendations.reducer';
import { selectRecommendationsKey, selectRecommendationsResult } from './product-recommendations.selectors';

const rows = (id: number): ProductRecommendations => ({ similar: [{ productId: id } as ListingTile], trending: [] });

function setup(seed?: { key: string; result: ProductRecommendations }) {
  const calls: { path: string; subject: Subject<ProductRecommendations> }[] = [];
  TestBed.configureTestingModule({
    providers: [
      provideStore(
        { [productRecommendationsFeature.name]: productRecommendationsFeature.reducer },
        seed ? { initialState: { [productRecommendationsFeature.name]: { ...seed, loading: false } } } : {},
      ),
      provideEffects(ProductRecommendationsEffects),
      {
        provide: ProductRecommendationsApi,
        useValue: {
          get: (path: string) => {
            const subject = new Subject<ProductRecommendations>();
            calls.push({ path, subject });
            return subject;
          },
        },
      },
    ],
  });
  const store = TestBed.inject(Store);
  return { store, calls, result: () => store.selectSignal(selectRecommendationsResult)(), key: () => store.selectSignal(selectRecommendationsKey)() };
}

describe('product recommendations store', () => {
  it('REC-1 keeps the rows of the product that is current, asking once per load', () => {
    const { store, calls, result, key } = setup();
    store.dispatch(ProductRecommendationsActions.load({ key: '/a' }));
    calls[0].subject.next(rows(2));

    expect(calls.length).toBe(1);
    expect(key()).toBe('/a');
    expect(result()).toEqual(rows(2));
  });

  it('REC-1 moving to another product cancels the older request, and a late answer for it is ignored', () => {
    const { store, calls, result } = setup();
    store.dispatch(ProductRecommendationsActions.load({ key: '/a' }));
    store.dispatch(ProductRecommendationsActions.load({ key: '/b' }));
    calls[0].subject.next(rows(1));
    calls[1].subject.next(rows(2));

    expect(result()).toEqual(rows(2));
  });

  it('REC-9 the browser store starts with the rows the server sent and keeps them while the same product loads again', () => {
    const { store, result } = setup({ key: '/a', result: rows(2) });
    expect(result()).toEqual(rows(2));

    store.dispatch(ProductRecommendationsActions.load({ key: '/a' }));

    expect(result()).toEqual(rows(2));
  });
});
