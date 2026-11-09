import { createReducer, on } from '@ngrx/store';
import { ExceptionsApiActions, ExceptionsPageActions } from './exceptions.actions';
import { initialExceptionsState } from './exceptions.state';

export const exceptionsReducer = createReducer(
  initialExceptionsState,

  // The store outlives the page, so opening starts from a clean slate (CONVENTIONS section 1).
  on(ExceptionsPageActions.opened, (state) => ({ ...initialExceptionsState, pageSize: state.pageSize, loading: true })),
  on(ExceptionsPageActions.filterChanged, (state, { openOnly }) => ({ ...state, openOnly, page: 1, loading: true, error: null })),
  on(ExceptionsPageActions.pageChanged, (state, { page }) => ({ ...state, page, loading: true, error: null })),
  on(ExceptionsPageActions.pageSizeChanged, (state, { pageSize }) => ({ ...state, pageSize, page: 1, loading: true, error: null })),
  on(ExceptionsPageActions.refreshed, (state) => ({ ...state, loading: true, error: null })),
  on(ExceptionsPageActions.resolveConfirmed, (state, { id }) => ({ ...state, resolvingId: id, error: null, notice: null })),
  on(ExceptionsPageActions.noticeDismissed, (state) => ({ ...state, notice: null })),

  on(ExceptionsApiActions.loadSucceeded, (state, { result }) => ({ ...state, rows: result.items, total: result.totalCount, page: result.page, loading: false })),
  on(ExceptionsApiActions.loadFailed, (state, { error }) => ({ ...state, error, loading: false })),
  on(ExceptionsApiActions.resolveSucceeded, (state, { row }) => ({
    ...state,
    resolvingId: null,
    notice: `Exception on ${row.orderNumber} resolved.`,
  })),
  on(ExceptionsApiActions.resolveFailed, (state, { error }) => ({ ...state, resolvingId: null, error })),
);
