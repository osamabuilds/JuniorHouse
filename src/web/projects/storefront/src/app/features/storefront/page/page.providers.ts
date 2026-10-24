import { EnvironmentProviders, makeEnvironmentProviders } from '@angular/core';
import { provideEffects } from '@ngrx/effects';
import { provideState } from '@ngrx/store';
import { PageEffects } from './store/page.effects';
import { pageFeature } from './store/page.reducer';

export function providePage(): EnvironmentProviders {
  return makeEnvironmentProviders([provideState(pageFeature), provideEffects(PageEffects)]);
}
