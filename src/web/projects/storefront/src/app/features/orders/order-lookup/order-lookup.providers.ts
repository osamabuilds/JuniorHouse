import { EnvironmentProviders, makeEnvironmentProviders } from '@angular/core';
import { provideEffects } from '@ngrx/effects';
import { provideState } from '@ngrx/store';
import { OrderLookupEffects } from './store/order-lookup.effects';
import { orderLookupFeature } from './store/order-lookup.reducer';

/** The look-up state lives only while the Track order page is open, so the route provides it. */
export function provideOrderLookup(): EnvironmentProviders {
  return makeEnvironmentProviders([provideState(orderLookupFeature), provideEffects(OrderLookupEffects)]);
}
