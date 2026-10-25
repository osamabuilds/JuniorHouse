import { EnvironmentProviders, makeEnvironmentProviders } from '@angular/core';
import { provideEffects } from '@ngrx/effects';
import { provideState } from '@ngrx/store';
import { NavigationEffects } from './store/navigation.effects';
import { navigationFeature } from './store/navigation.reducer';

export function provideNavigation(): EnvironmentProviders {
  return makeEnvironmentProviders([provideState(navigationFeature), provideEffects(NavigationEffects)]);
}
