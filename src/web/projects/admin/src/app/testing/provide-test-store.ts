import { EnvironmentProviders, Provider } from '@angular/core';
import { provideEffects } from '@ngrx/effects';
import { provideStore } from '@ngrx/store';
import { provideReferenceDataStore } from '@features/reference-data';

/** The app-wide store setup (root store + the eagerly registered reference-data feature) for unit tests. */
export function provideTestStore(): (Provider | EnvironmentProviders)[] {
  return [provideStore(), provideEffects(), provideReferenceDataStore()];
}
