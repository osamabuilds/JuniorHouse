import { createFeature, createReducer, on } from '@ngrx/store';
import { TrackedOrder } from '../models/order-lookup.model';
import { OrderLookupActions } from './order-lookup.actions';

export interface OrderLookupState {
  readonly status: 'idle' | 'busy' | 'found' | 'mismatch' | 'captcha' | 'limited' | 'error';
  readonly order: TrackedOrder | null;
  /** Once the server asks for the check it stays on screen until a look-up succeeds. */
  readonly captchaRequired: boolean;
  /** The opened link did not work (broken, or the order is gone). */
  readonly linkFailed: boolean;
}

export const initialOrderLookupState: OrderLookupState = { status: 'idle', order: null, captchaRequired: false, linkFailed: false };

export const orderLookupFeature = createFeature({
  name: 'orderLookup',
  reducer: createReducer(
    initialOrderLookupState,
    on(OrderLookupActions.formOpened, OrderLookupActions.cleared, (): OrderLookupState => initialOrderLookupState),
    on(OrderLookupActions.lookupSubmitted, (s): OrderLookupState => ({ ...s, status: 'busy', order: null })),
    on(OrderLookupActions.lookupDone, (s, { result }): OrderLookupState => {
      switch (result.kind) {
        case 'found':
          return { ...s, status: 'found', order: result.order, captchaRequired: false };
        case 'captcha':
          // the check is needed before this attempt counts: ask for it (this depends on the visitor address, never on the order)
          return { ...s, status: 'captcha', captchaRequired: true };
        case 'mismatch':
          return { ...s, status: 'mismatch' };
        case 'limited':
          return { ...s, status: 'limited', captchaRequired: true };
        default:
          return { ...s, status: 'error' };
      }
    }),
    on(OrderLookupActions.linkOpened, (): OrderLookupState => ({ ...initialOrderLookupState, status: 'busy' })),
    on(OrderLookupActions.linkDone, (s, { result }): OrderLookupState =>
      result.kind === 'found' ? { ...s, status: 'found', order: result.order } : { ...s, status: 'idle', linkFailed: true },
    ),
  ),
});

export const {
  selectStatus: selectLookupStatus,
  selectOrder: selectLookupOrder,
  selectCaptchaRequired: selectLookupCaptchaRequired,
  selectLinkFailed: selectLookupLinkFailed,
} = orderLookupFeature;
