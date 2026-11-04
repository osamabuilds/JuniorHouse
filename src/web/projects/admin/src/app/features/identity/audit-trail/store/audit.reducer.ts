import { createReducer, on } from '@ngrx/store';
import { AuditApiActions, AuditPageActions } from './audit.actions';
import { initialAuditState } from './audit.state';

export const auditReducer = createReducer(
  initialAuditState,
  on(AuditPageActions.opened, (state) => ({ ...state, loading: true, error: null })),
  on(AuditPageActions.filtersApplied, (state, { form }) => ({ ...state, form, page: 1, loading: true, error: null, selected: null })),
  on(AuditPageActions.pageChanged, (state, { page }) => ({ ...state, page, loading: true, error: null })),
  on(AuditPageActions.pageSizeChanged, (state, { pageSize }) => ({ ...state, pageSize, page: 1, loading: true, error: null })),
  on(AuditPageActions.entrySelected, (state, { entry }) => ({ ...state, selected: entry })),
  on(AuditPageActions.detailClosed, (state) => ({ ...state, selected: null })),
  on(AuditApiActions.loadSucceeded, (state, { result }) => ({ ...state, entries: result.items, total: result.totalCount, loading: false })),
  on(AuditApiActions.loadFailed, (state, { error }) => ({ ...state, entries: [], total: 0, loading: false, error })),
);
