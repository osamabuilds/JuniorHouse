import { createReducer, on } from '@ngrx/store';
import { PackingSlipApiActions, PackingSlipPageActions } from './packing-slips.actions';
import { initialPackingSlipsState } from './packing-slips.state';

export const packingSlipsReducer = createReducer(
  initialPackingSlipsState,

  // The store outlives the page, so opening starts from a clean slate (CONVENTIONS section 1).
  on(PackingSlipPageActions.opened, () => ({ ...initialPackingSlipsState, loading: true })),
  on(PackingSlipPageActions.printClicked, (state) => ({ ...state, printing: true, error: null, notice: null })),
  on(PackingSlipPageActions.noticeDismissed, (state) => ({ ...state, notice: null })),

  on(PackingSlipApiActions.loadSucceeded, (state, { slip }) => ({ ...state, slip, loading: false })),
  on(PackingSlipApiActions.loadFailed, (state, { error }) => ({ ...state, error, loading: false })),
  // A printer that is down does not block the pack (SLP-6): the slip prints on retry, and the screen says so.
  on(PackingSlipApiActions.printSucceeded, (state, { result }) => ({
    ...state,
    slip: result.slip,
    printing: false,
    notice: result.printed
      ? 'The slip was sent to the printer.'
      : 'The printer did not answer. The slip is waiting and will print on its own when the printer is back. You can also print this page.',
  })),
  on(PackingSlipApiActions.printFailed, (state, { error }) => ({ ...state, printing: false, error })),
);
