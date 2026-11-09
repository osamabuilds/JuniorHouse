import { createReducer, on } from '@ngrx/store';
import { OrderQueueActions, OrderQueueApiActions } from './order-queue.actions';
import { initialOrderQueueState } from './order-queue.state';

export const orderQueueReducer = createReducer(
  initialOrderQueueState,

  // The store outlives the page, so opening starts from a clean slate.
  on(OrderQueueActions.listOpened, (state) => ({ ...state, loading: true, error: null, notice: null, detail: null })),
  on(OrderQueueActions.filtersChanged, (state, { filters }) => ({ ...state, filters, page: 1, loading: true, error: null })),
  on(OrderQueueActions.pageChanged, (state, { page }) => ({ ...state, page, loading: true, error: null })),
  on(OrderQueueActions.pageSizeChanged, (state, { pageSize }) => ({ ...state, pageSize, page: 1, loading: true, error: null })),
  on(OrderQueueActions.detailOpened, (state) => ({ ...state, detail: null, detailLoading: true, error: null, notice: null })),
  on(OrderQueueActions.outcomeRecorded, OrderQueueActions.orderCancelled, OrderQueueActions.flagCleared, (state) => ({ ...state, acting: true, error: null, notice: null })),
  on(OrderQueueActions.noticeDismissed, (state) => ({ ...state, notice: null })),

  on(OrderQueueApiActions.loadSucceeded, (state, { result }) => ({ ...state, orders: result.items, total: result.totalCount, page: result.page, loading: false })),
  on(OrderQueueApiActions.loadFailed, (state, { error }) => ({ ...state, error, loading: false })),
  on(OrderQueueApiActions.detailSucceeded, (state, { order }) => ({ ...state, detail: order, detailLoading: false })),
  on(OrderQueueApiActions.detailFailed, (state, { error }) => ({ ...state, error, detailLoading: false })),
  on(OrderQueueApiActions.actionSucceeded, (state, { order, notice }) => ({ ...state, detail: order, acting: false, notice })),
  on(OrderQueueApiActions.actionFailed, (state, { error }) => ({ ...state, acting: false, error })),
);
