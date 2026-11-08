import { createFeature, createReducer, on } from '@ngrx/store';
import { ProductRecommendations } from '../models/product-recommendations.model';
import { ProductRecommendationsActions } from './product-recommendations.actions';

export interface ProductRecommendationsState {
  /** The product path the result belongs to; a result for another path is stale and ignored. */
  readonly key: string | null;
  readonly result: ProductRecommendations | null;
  readonly loading: boolean;
}

export const initialProductRecommendationsState: ProductRecommendationsState = { key: null, result: null, loading: false };

export const productRecommendationsReducer = createReducer(
  initialProductRecommendationsState,
  // The rows already on screen stay while the same product reloads (the browser starts from the server's), so hydration shows no flash.
  on(ProductRecommendationsActions.load, (s, { key }): ProductRecommendationsState => ({ ...s, key, loading: true })),
  on(ProductRecommendationsActions.loadDone, (s, { key, result }): ProductRecommendationsState => (key === s.key ? { key, result, loading: false } : s)),
);

/** Whitelisted for the server-to-browser transfer (ADR 0028): the rows are public catalogue data, so hydration shows the same cards. */
export const productRecommendationsFeature = createFeature({ name: 'productRecommendations', reducer: productRecommendationsReducer });
