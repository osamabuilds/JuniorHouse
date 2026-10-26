import { EnvironmentProviders, Provider } from '@angular/core';
import { provideEffects } from '@ngrx/effects';
import { provideState, provideStore } from '@ngrx/store';
import { SESSION_FEATURE_KEY, sessionReducer } from '@core/auth';
import { provideLookupsStore } from '@features/reference/lookups';

/** The app-wide store setup (root store + the eagerly registered reference-data feature) for unit tests. */
export function provideTestStore(): (Provider | EnvironmentProviders)[] {
  return [provideStore(), provideEffects(), provideState(SESSION_FEATURE_KEY, sessionReducer), provideLookupsStore()];
}
