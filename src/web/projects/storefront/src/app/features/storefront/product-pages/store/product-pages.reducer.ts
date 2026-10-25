import { createFeature, createReducer, on } from '@ngrx/store';
import { ProductPageResult } from '../models/product-page.model';
import { ProductPagesActions } from './product-pages.actions';

export interface ProductPagesState {
  /** The key of the request the result belongs to; a result for another key is stale and ignored. */
  readonly key: string | null;
  readonly result: ProductPageResult | null;
  readonly loading: boolean;
}

export const initialProductPagesState: ProductPagesState = { key: null, result: null, loading: false };

export const productPagesReducer = createReducer(
  initialProductPagesState,
  // The previous page stays on screen while a new colour or size loads, so choosing does not flash blank.
  on(ProductPagesActions.load, (s, { key }): ProductPagesState => ({ ...s, key, loading: true })),
  on(ProductPagesActions.loadDone, (s, { key, result }): ProductPagesState => (key === s.key ? { key, result, loading: false } : s)),
);

export const productPagesFeature = createFeature({ name: 'productPages', reducer: productPagesReducer });
