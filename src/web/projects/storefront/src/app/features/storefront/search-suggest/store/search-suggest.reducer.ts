import { createFeature, createReducer, on } from '@ngrx/store';
import { ProductSuggestion } from '../../../../shared/data/mock-products';
import { SearchSuggestActions } from './search-suggest.actions';

export interface SearchSuggestState {
  /** The text the current results are for ('' when there are none). */
  readonly searched: string;
  readonly results: readonly ProductSuggestion[];
}

export const MAX_SUGGESTIONS = 4;

const initial: SearchSuggestState = { searched: '', results: [] };

export const searchSuggestFeature = createFeature({
  name: 'searchSuggest',
  reducer: createReducer(
    initial,
    on(SearchSuggestActions.resultsLoaded, (_, { query, results }): SearchSuggestState => ({ searched: query, results: results.slice(0, MAX_SUGGESTIONS) })),
    on(SearchSuggestActions.cleared, (): SearchSuggestState => initial),
  ),
});
