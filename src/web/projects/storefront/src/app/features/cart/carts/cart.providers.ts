import { EnvironmentProviders, makeEnvironmentProviders } from '@angular/core';
import { provideEffects } from '@ngrx/effects';
import { provideState } from '@ngrx/store';
import { CartEffects } from './store/cart.effects';
import { cartFeature } from './store/cart.reducer';

/** The basket lives for the whole session (the header shows its count on every page), so it is provided at the app root. */
export function provideCart(): EnvironmentProviders {
  return makeEnvironmentProviders([provideState(cartFeature), provideEffects(CartEffects)]);
}
