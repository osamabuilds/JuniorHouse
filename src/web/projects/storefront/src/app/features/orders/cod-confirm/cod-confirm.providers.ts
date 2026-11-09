import { EnvironmentProviders, makeEnvironmentProviders } from '@angular/core';
import { provideEffects } from '@ngrx/effects';
import { provideState } from '@ngrx/store';
import { CodEffects } from './store/cod.effects';
import { codFeature } from './store/cod.reducer';

/** The confirm state lives only while a page that shows it is open, so the routes provide it. */
export function provideCodConfirm(): EnvironmentProviders {
  return makeEnvironmentProviders([provideState(codFeature), provideEffects(CodEffects)]);
}
