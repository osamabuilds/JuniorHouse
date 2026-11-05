import { EnvironmentProviders, makeEnvironmentProviders } from '@angular/core';
import { provideEffects } from '@ngrx/effects';
import { provideState } from '@ngrx/store';
import { CollectionsPageEffects } from './store/collections-page.effects';
import { collectionsPageFeature } from './store/collections-page.reducer';

export function provideCollectionsPage(): EnvironmentProviders {
  return makeEnvironmentProviders([provideState(collectionsPageFeature), provideEffects(CollectionsPageEffects)]);
}
