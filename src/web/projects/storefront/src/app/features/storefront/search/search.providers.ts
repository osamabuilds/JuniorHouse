import { EnvironmentProviders, makeEnvironmentProviders } from '@angular/core';
import { provideEffects } from '@ngrx/effects';
import { provideState } from '@ngrx/store';
import { SearchResultsEffects } from './store/search-results.effects';
import { searchResultsFeature } from './store/search-results.reducer';
import { SearchSuggestEffects } from './store/search-suggest.effects';
import { searchSuggestFeature } from './store/search-suggest.reducer';

/** Autosuggest (the header overlay) and the results page share one feature; each has its own state. */
export function provideSearch(): EnvironmentProviders {
  return makeEnvironmentProviders([
    provideState(searchSuggestFeature),
    provideState(searchResultsFeature),
    provideEffects(SearchSuggestEffects, SearchResultsEffects),
  ]);
}
