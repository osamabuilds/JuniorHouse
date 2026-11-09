import { createFeature, createReducer, on } from '@ngrx/store';
import { PaymentStatus, StartResult } from '../models/payment.model';
import { PaymentActions } from './payment.actions';

export interface PaymentFeatureState {
  /** A start is on its way: the button waits so a double tap cannot open two payment pages. */
  readonly starting: boolean;
  readonly start: StartResult | null;
  readonly status: 'idle' | 'loading' | 'ready' | 'invalid' | 'error';
  readonly payment: PaymentStatus | null;
  /** How many answers the return page has had while still confirming; at the limit it says it has not heard back. */
  readonly polls: number;
}

export const initialPaymentState: PaymentFeatureState = { starting: false, start: null, status: 'idle', payment: null, polls: 0 };

export const paymentFeature = createFeature({
  name: 'payment',
  reducer: createReducer(
    initialPaymentState,
    on(PaymentActions.startRequested, (s): PaymentFeatureState => ({ ...s, starting: true, start: null })),
    on(PaymentActions.startDone, (s, { result }): PaymentFeatureState => ({ ...s, starting: false, start: result })),
    on(PaymentActions.statusRequested, (): PaymentFeatureState => ({ ...initialPaymentState, status: 'loading' })),
    on(PaymentActions.statusLoaded, (s, { result }): PaymentFeatureState => {
      if (result.status === 'ok') return { ...s, status: 'ready', payment: result.payment, polls: result.payment.state === 'confirming' ? s.polls + 1 : s.polls };
      return { ...s, status: result.status === 'invalid' ? 'invalid' : s.payment ? 'ready' : 'error' };
    }),
    on(PaymentActions.reset, (): PaymentFeatureState => initialPaymentState),
  ),
});

export const {
  selectStarting: selectPaymentStarting,
  selectStart: selectPaymentStart,
  selectStatus: selectPaymentStatus,
  selectPayment: selectPaymentPayment,
  selectPolls: selectPaymentPolls,
} = paymentFeature;
