import { EnvironmentProviders, inject, makeEnvironmentProviders, provideAppInitializer } from '@angular/core';
import { provideEffects } from '@ngrx/effects';
import { Store, provideState } from '@ngrx/store';
import { CustomerAuthActions } from './store/customer-auth.actions';
import { CustomerAuthEffects } from './store/customer-auth.effects';
import { customerAuthFeature } from './store/customer-auth.reducer';

/** The customer session, app-wide: the header, the notice bar and the account pages all read it. It asks the API who is signed in once at start-up. */
export function provideCustomerAuth(): EnvironmentProviders {
  return makeEnvironmentProviders([
    provideState(customerAuthFeature),
    provideEffects(CustomerAuthEffects),
    // Not awaited: the page is not held back by the answer; the header shows nothing account-related until it arrives.
    provideAppInitializer(() => {
      inject(Store).dispatch(CustomerAuthActions.sessionRequested());
    }),
  ]);
}
