import { EnvironmentProviders, makeEnvironmentProviders } from '@angular/core';
import { provideEffects } from '@ngrx/effects';
import { provideState } from '@ngrx/store';
import * as sessionEffects from './store/auth.effects';
import { sessionReducer } from './store/auth.reducer';
import { SESSION_FEATURE_KEY } from './store/auth.state';

/** Registers the session store and its effects with the app. The HTTP interceptor is added where the HTTP client is provided. */
export function provideSession(): EnvironmentProviders {
  return makeEnvironmentProviders([provideState(SESSION_FEATURE_KEY, sessionReducer), provideEffects(sessionEffects)]);
}
