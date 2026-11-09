import { EnvironmentProviders, makeEnvironmentProviders } from '@angular/core';
import { provideEffects } from '@ngrx/effects';
import { provideState } from '@ngrx/store';
import { PaymentEffects } from './store/payment.effects';
import { paymentFeature } from './store/payment.reducer';

/** The payment state lives only while a page that shows it is open, so the routes provide it. */
export function providePayments(): EnvironmentProviders {
  return makeEnvironmentProviders([provideState(paymentFeature), provideEffects(PaymentEffects)]);
}
