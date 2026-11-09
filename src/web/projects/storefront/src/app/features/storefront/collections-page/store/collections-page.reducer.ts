import { createFeature, createReducer, on } from '@ngrx/store';
import { CollectionsPageResult } from '../models/collections-page.model';
import { CollectionsPageActions } from './collections-page.actions';

export interface CollectionsPageState {
  /** The key of the request the result belongs to; a result for another key is stale and ignored. */
  readonly key: string | null;
  readonly result: CollectionsPageResult | null;
  readonly loading: boolean;
}

export const initialCollectionsPageState: CollectionsPageState = { key: null, result: null, loading: false };

export const collectionsPageFeature = createFeature({
  name: 'collectionsPage',
  reducer: createReducer(
    initialCollectionsPageState,
    // A new request keeps the previous page on screen until the new one arrives.
    on(CollectionsPageActions.load, (s, { key }): CollectionsPageState => ({ ...s, key, loading: true })),
    on(CollectionsPageActions.loadDone, (s, { key, result }): CollectionsPageState => (key === s.key ? { key, result, loading: false } : s)),
  ),
});
