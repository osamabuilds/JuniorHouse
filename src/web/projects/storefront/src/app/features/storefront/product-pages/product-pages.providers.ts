import { EnvironmentProviders, makeEnvironmentProviders } from '@angular/core';
import { provideEffects } from '@ngrx/effects';
import { provideState } from '@ngrx/store';
import { ProductPagesEffects } from './store/product-pages.effects';
import { productPagesFeature } from './store/product-pages.reducer';

export function provideProductPages(): EnvironmentProviders {
  return makeEnvironmentProviders([provideState(productPagesFeature), provideEffects(ProductPagesEffects)]);
}
