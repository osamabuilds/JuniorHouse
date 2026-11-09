import { createFeature, createReducer, on } from '@ngrx/store';
import { CheckoutSummary, City, PlaceResult, Province } from '../models/checkout.model';
import { CheckoutActions } from './checkout.actions';

export type CheckoutStatus = 'idle' | 'loading' | 'ready' | 'error';

export interface CheckoutState {
  readonly status: CheckoutStatus;
  readonly summary: CheckoutSummary | null;
  readonly provinces: readonly Province[];
  readonly cities: readonly City[];
  /** The order is being sent: the button waits so a double click cannot place two (CHK-10). */
  readonly placing: boolean;
  /** How the last send ended; null before any. */
  readonly result: PlaceResult | null;
}

export const initialCheckoutState: CheckoutState = { status: 'idle', summary: null, provinces: [], cities: [], placing: false, result: null };

export const checkoutFeature = createFeature({
  name: 'checkout',
  reducer: createReducer(
    initialCheckoutState,
    on(CheckoutActions.opened, (): CheckoutState => ({ ...initialCheckoutState, status: 'loading' })),
    // The figures on screen stay while the server recalculates, so the page never flashes empty.
    on(CheckoutActions.summaryRequested, (s): CheckoutState => ({ ...s, status: s.summary ? 'ready' : 'loading' })),
    on(CheckoutActions.summaryLoaded, (s, { result }): CheckoutState =>
      result.status === 'ok' ? { ...s, status: 'ready', summary: result.summary } : { ...s, status: s.summary ? 'ready' : 'error' },
    ),
    on(CheckoutActions.provincesLoaded, (s, { provinces }): CheckoutState => ({ ...s, provinces })),
    on(CheckoutActions.provinceChosen, (s): CheckoutState => ({ ...s, cities: [] })),
    on(CheckoutActions.citiesLoaded, (s, { cities }): CheckoutState => ({ ...s, cities })),
    on(CheckoutActions.submitted, (s): CheckoutState => ({ ...s, placing: true, result: null })),
    on(CheckoutActions.submitDone, (s, { result }): CheckoutState => ({ ...s, placing: false, result })),
  ),
});

export const {
  selectStatus: selectCheckoutStatus,
  selectSummary: selectCheckoutSummary,
  selectProvinces: selectCheckoutProvinces,
  selectCities: selectCheckoutCities,
  selectPlacing: selectCheckoutPlacing,
  selectResult: selectCheckoutResult,
} = checkoutFeature;
