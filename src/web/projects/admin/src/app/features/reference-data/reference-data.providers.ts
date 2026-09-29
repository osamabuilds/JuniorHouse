import { EnvironmentProviders, makeEnvironmentProviders } from '@angular/core';
import { provideEffects } from '@ngrx/effects';
import { provideState } from '@ngrx/store';
import * as referenceDataEffects from './store/reference-data.effects';
import { referenceDataReducer } from './store/reference-data.reducer';
import { REFERENCE_DATA_FEATURE_KEY } from './store/reference-data.state';

/** Registered app-wide: other features read cached lookup lists from this store. */
export function provideReferenceDataStore(): EnvironmentProviders {
  return makeEnvironmentProviders([
    provideState(REFERENCE_DATA_FEATURE_KEY, referenceDataReducer),
    provideEffects(referenceDataEffects),
  ]);
}
