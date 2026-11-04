import { EnvironmentProviders, makeEnvironmentProviders } from '@angular/core';
import { provideEffects } from '@ngrx/effects';
import { provideState } from '@ngrx/store';
import { AddressesEffects } from './store/addresses.effects';
import { addressesFeature } from './store/addresses.reducer';

export function provideAddresses(): EnvironmentProviders {
  return makeEnvironmentProviders([provideState(addressesFeature), provideEffects(AddressesEffects)]);
}
