import { EnvironmentProviders, makeEnvironmentProviders } from '@angular/core';
import { provideEffects } from '@ngrx/effects';
import { provideState } from '@ngrx/store';
import { ListingsEffects } from './store/listings.effects';
import { listingsFeature } from './store/listings.reducer';

export function provideListings(): EnvironmentProviders {
  return makeEnvironmentProviders([provideState(listingsFeature), provideEffects(ListingsEffects)]);
}
