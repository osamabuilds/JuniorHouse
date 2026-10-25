import { createFeature, createReducer, on } from '@ngrx/store';
import { SearchResult } from '../models/search.model';
import { SearchResultsActions } from './search-results.actions';

export interface SearchResultsState {
  /** The key of the request the result belongs to; a result for another key is stale and ignored. */
  readonly key: string | null;
  readonly result: SearchResult | null;
  readonly loading: boolean;
}

export const initialSearchResultsState: SearchResultsState = { key: null, result: null, loading: false };

export const searchResultsFeature = createFeature({
  name: 'searchResults',
  reducer: createReducer(
    initialSearchResultsState,
    // A new request keeps the previous page on screen until the new one arrives, so filtering does not flash blank.
    on(SearchResultsActions.load, (s, { key }): SearchResultsState => ({ ...s, key, loading: true })),
    on(SearchResultsActions.loadDone, (s, { key, result }): SearchResultsState => (key === s.key ? { key, result, loading: false } : s)),
  ),
});
