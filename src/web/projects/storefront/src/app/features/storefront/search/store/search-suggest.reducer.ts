import { createFeature, createReducer, on } from '@ngrx/store';
import { SuggestionItem } from '../models/search.model';
import { SearchSuggestActions } from './search-suggest.actions';

export interface SearchSuggestState {
  /** The text the current results are for ('' when there are none). */
  readonly searched: string;
  readonly results: readonly SuggestionItem[];
}

/** The API sends at most eight (SRC-1); the reducer holds no more than that whatever arrives. */
export const MAX_SUGGESTIONS = 8;

const initial: SearchSuggestState = { searched: '', results: [] };

export const searchSuggestFeature = createFeature({
  name: 'searchSuggest',
  reducer: createReducer(
    initial,
    on(SearchSuggestActions.resultsLoaded, (_, { query, result }): SearchSuggestState => ({ searched: query, results: result.items.slice(0, MAX_SUGGESTIONS) })),
    on(SearchSuggestActions.cleared, (): SearchSuggestState => initial),
  ),
});
