import { createReducer, on } from '@ngrx/store';
import { NO_FILTERS } from '../models';
import { DeliveryLogApiActions, DeliveryLogPageActions } from './delivery-log.actions';
import { initialDeliveryLogState } from './delivery-log.state';

export const deliveryLogReducer = createReducer(
  initialDeliveryLogState,

  // The store outlives the page, so opening starts from a clean slate (CONVENTIONS section 1).
  on(DeliveryLogPageActions.opened, (state, { orderId }) => ({
    ...initialDeliveryLogState,
    pageSize: state.pageSize,
    filters: { ...NO_FILTERS, orderId },
    loading: true,
  })),
  on(DeliveryLogPageActions.filtersChanged, (state, { filters }) => ({ ...state, filters, page: 1, loading: true, error: null })),
  on(DeliveryLogPageActions.pageChanged, (state, { page }) => ({ ...state, page, loading: true, error: null })),
  on(DeliveryLogPageActions.pageSizeChanged, (state, { pageSize }) => ({ ...state, pageSize, page: 1, loading: true, error: null })),
  on(DeliveryLogPageActions.refreshed, (state) => ({ ...state, loading: true, error: null })),
  on(DeliveryLogPageActions.detailOpened, (state) => ({ ...state, detail: null, detailLoading: true, error: null })),
  on(DeliveryLogPageActions.detailClosed, (state) => ({ ...state, detail: null, detailLoading: false })),
  on(DeliveryLogPageActions.resendConfirmed, (state) => ({ ...state, resending: true, error: null, notice: null })),
  on(DeliveryLogPageActions.noticeDismissed, (state) => ({ ...state, notice: null })),

  on(DeliveryLogApiActions.loadSucceeded, (state, { result }) => ({ ...state, rows: result.items, total: result.totalCount, page: result.page, loading: false })),
  on(DeliveryLogApiActions.loadFailed, (state, { error }) => ({ ...state, error, loading: false })),
  on(DeliveryLogApiActions.summarySucceeded, (state, { summary }) => ({ ...state, summary })),
  on(DeliveryLogApiActions.summaryFailed, (state, { error }) => ({ ...state, error })),
  on(DeliveryLogApiActions.detailSucceeded, (state, { detail }) => ({ ...state, detail, detailLoading: false })),
  on(DeliveryLogApiActions.detailFailed, (state, { error }) => ({ ...state, error, detailLoading: false })),
  on(DeliveryLogApiActions.resendSucceeded, (state) => ({ ...state, resending: false, notice: 'The message is queued and will be sent in a moment.' })),
  on(DeliveryLogApiActions.resendFailed, (state, { error }) => ({ ...state, resending: false, error })),
);
