import { createFeature, createReducer, on } from '@ngrx/store';
import { ListingResult } from '../models/listing.model';
import { ListingsActions } from './listings.actions';

export interface ListingsState {
  /** The key of the request the result belongs to; a result for another key is stale and ignored. */
  readonly key: string | null;
  readonly result: ListingResult | null;
  readonly loading: boolean;
}

export const initialListingsState: ListingsState = { key: null, result: null, loading: false };

export const listingsReducer = createReducer(
  initialListingsState,
  // A new request keeps the previous page on screen until the new one arrives, so filtering does not flash blank.
  on(ListingsActions.load, (s, { key }): ListingsState => ({ ...s, key, loading: true })),
  on(ListingsActions.loadDone, (s, { key, result }): ListingsState => (key === s.key ? { key, result, loading: false } : s)),
);

export const listingsFeature = createFeature({ name: 'listings', reducer: listingsReducer });
