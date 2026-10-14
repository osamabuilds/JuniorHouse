import { createReducer, on } from '@ngrx/store';
import { RECONFIRM_PREFIX } from '../adjustments.constants';
import { AdjustmentsApiActions, AdjustmentsPageActions } from './adjustments.actions';
import { initialAdjustmentsState } from './adjustments.state';

export const adjustmentsReducer = createReducer(
  initialAdjustmentsState,

  // The store outlives the page, so opening it starts from a clean slate.
  on(AdjustmentsPageActions.opened, () => ({ ...initialAdjustmentsState, loading: true })),
  on(AdjustmentsPageActions.reasonFilterChanged, (state, { reasonId }) => ({ ...state, reasonId, page: 1, loading: true, error: null })),
  on(AdjustmentsPageActions.pageChanged, (state, { page }) => ({ ...state, page, loading: true, error: null })),
  on(AdjustmentsPageActions.pageSizeChanged, (state, { pageSize }) => ({ ...state, pageSize, page: 1, loading: true, error: null })),
  on(AdjustmentsPageActions.refreshRequested, (state) => ({ ...state, loading: true, error: null })),
  on(AdjustmentsPageActions.formOpened, (state) => ({ ...state, formOpen: true, formError: null, reconfirm: null, notice: null })),
  on(AdjustmentsPageActions.formClosed, (state) => ({ ...state, formOpen: false, saving: false, formError: null, reconfirm: null })),
  on(AdjustmentsPageActions.submitted, (state) => ({ ...state, saving: true, formError: null })),
  // Declining the confirmation goes back to the form with everything still typed in.
  on(AdjustmentsPageActions.reconfirmDeclined, (state) => ({ ...state, reconfirm: null, saving: false })),
  on(AdjustmentsPageActions.noticeDismissed, (state) => ({ ...state, notice: null })),

  on(AdjustmentsApiActions.loadAdjustmentsSucceeded, (state, { result }) => ({
    ...state,
    adjustments: result.items,
    total: result.totalCount,
    page: result.page,
    loading: false,
  })),
  on(AdjustmentsApiActions.loadAdjustmentsFailed, (state, { error }) => ({ ...state, error, loading: false })),
  on(AdjustmentsApiActions.loadReasonsSucceeded, (state, { reasons }) => ({ ...state, reasons })),
  on(AdjustmentsApiActions.loadReasonsFailed, (state, { error }) => ({ ...state, error })),
  on(AdjustmentsApiActions.postSucceeded, (state, { adjustment }) => ({
    ...state,
    formOpen: false,
    saving: false,
    formError: null,
    reconfirm: null,
    loading: true,
    notice: `${adjustment.quantityChange > 0 ? '+' : ''}${adjustment.quantityChange} unit(s) of ${adjustment.skuCode} in ${adjustment.binCode} (${adjustment.reasonCode}). The bin now holds ${adjustment.balanceAfter}.`,
  })),
  // A change above the limit is not an error: it is a question, so the screen asks and keeps the very same attempt (ADJ-4b, ADJ-8).
  on(AdjustmentsApiActions.postFailed, (state, { error, value }) =>
    error.message.startsWith(RECONFIRM_PREFIX)
      ? { ...state, saving: false, formError: null, reconfirm: { value, message: error.message } }
      : { ...state, saving: false, reconfirm: null, formError: error },
  ),
);
