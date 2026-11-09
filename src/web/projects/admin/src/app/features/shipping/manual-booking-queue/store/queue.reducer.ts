import { createReducer, on } from '@ngrx/store';
import { QueueApiActions, QueuePageActions } from './queue.actions';
import { initialQueueState } from './queue.state';

export const queueReducer = createReducer(
  initialQueueState,

  // The store outlives the page, so opening starts from a clean slate (CONVENTIONS section 1).
  on(QueuePageActions.opened, (state) => ({ ...initialQueueState, pageSize: state.pageSize, loading: true })),
  on(QueuePageActions.filterChanged, (state, { needsStaffOnly }) => ({ ...state, needsStaffOnly, page: 1, loading: true, error: null })),
  on(QueuePageActions.pageChanged, (state, { page }) => ({ ...state, page, loading: true, error: null })),
  on(QueuePageActions.pageSizeChanged, (state, { pageSize }) => ({ ...state, pageSize, page: 1, loading: true, error: null })),
  on(QueuePageActions.refreshed, (state) => ({ ...state, loading: true, error: null })),
  on(QueuePageActions.retryClicked, (state, { id }) => ({ ...state, busyId: id, error: null, notice: null })),
  on(QueuePageActions.waybillEntered, (state, { id }) => ({ ...state, busyId: id, error: null, notice: null })),
  on(QueuePageActions.noticeDismissed, (state) => ({ ...state, notice: null })),

  on(QueueApiActions.loadSucceeded, (state, { result }) => ({ ...state, rows: result.items, total: result.totalCount, page: result.page, loading: false })),
  on(QueueApiActions.loadFailed, (state, { error }) => ({ ...state, error, loading: false })),
  on(QueueApiActions.actionSucceeded, (state, { message }) => ({ ...state, busyId: null, notice: message })),
  on(QueueApiActions.actionFailed, (state, { error }) => ({ ...state, busyId: null, error })),
);
