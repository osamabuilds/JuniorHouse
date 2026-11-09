import { EnvironmentProviders, makeEnvironmentProviders } from '@angular/core';
import { provideEffects } from '@ngrx/effects';
import { provideState } from '@ngrx/store';
import { NotifyMeEffects } from './store/notify-me.effects';
import { notifyMeFeature } from './store/notify-me.reducer';

/** Provided at the app root, like the other customer features, so the product page needs no setup. */
export function provideNotifyMe(): EnvironmentProviders {
  return makeEnvironmentProviders([provideState(notifyMeFeature), provideEffects(NotifyMeEffects)]);
}
