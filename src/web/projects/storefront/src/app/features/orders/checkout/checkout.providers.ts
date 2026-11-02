import { EnvironmentProviders, makeEnvironmentProviders } from '@angular/core';
import { provideEffects } from '@ngrx/effects';
import { provideState } from '@ngrx/store';
import { CheckoutEffects } from './store/checkout.effects';
import { checkoutFeature } from './store/checkout.reducer';

/** Checkout state lives only while the checkout route is open, so the route provides it. */
export function provideCheckout(): EnvironmentProviders {
  return makeEnvironmentProviders([provideState(checkoutFeature), provideEffects(CheckoutEffects)]);
}
