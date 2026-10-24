import { EnvironmentProviders, makeEnvironmentProviders } from '@angular/core';
import { provideEffects } from '@ngrx/effects';
import { provideState } from '@ngrx/store';
import { SearchSuggestEffects } from './store/search-suggest.effects';
import { searchSuggestFeature } from './store/search-suggest.reducer';

export function provideSearchSuggest(): EnvironmentProviders {
  return makeEnvironmentProviders([provideState(searchSuggestFeature), provideEffects(SearchSuggestEffects)]);
}
