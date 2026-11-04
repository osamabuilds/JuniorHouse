import { createReducer, on } from '@ngrx/store';
import { NotifyDemandApiActions, NotifyDemandPageActions } from './notify-demand.actions';
import { initialNotifyDemandState } from './notify-demand.state';

export const notifyDemandReducer = createReducer(
  initialNotifyDemandState,

  // The store outlives the page, so opening starts from a clean slate (CONVENTIONS section 1).
  on(NotifyDemandPageActions.opened, (state) => ({ ...initialNotifyDemandState, pageSize: state.pageSize, loading: true })),
  on(NotifyDemandPageActions.pageChanged, (state, { page }) => ({ ...state, page, loading: true, error: null })),
  on(NotifyDemandPageActions.pageSizeChanged, (state, { pageSize }) => ({ ...state, pageSize, page: 1, loading: true, error: null })),
  on(NotifyDemandPageActions.refreshed, (state) => ({ ...state, loading: true, error: null })),

  on(NotifyDemandApiActions.loadSucceeded, (state, { result }) => ({ ...state, rows: result.items, total: result.totalCount, page: result.page, loading: false })),
  on(NotifyDemandApiActions.loadFailed, (state, { error }) => ({ ...state, error, loading: false })),
);
