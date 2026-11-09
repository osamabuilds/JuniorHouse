import { createActionGroup, emptyProps, props } from '@ngrx/store';
import { StartResult, StatusResult } from '../models/payment.model';

export const PaymentActions = createActionGroup({
  source: 'Payment',
  events: {
    /** The pay page opened with a token: ask the gateway for its hosted page. */
    'Start Requested': props<{ token: string }>(),
    'Start Done': props<{ result: StartResult }>(),
    /** The shopper came back: follow the stored state until it settles (PAY-10). */
    'Status Requested': props<{ token: string }>(),
    'Status Loaded': props<{ result: StatusResult }>(),
    /** The shopper left the page: stop asking. */
    Reset: emptyProps(),
  },
});
