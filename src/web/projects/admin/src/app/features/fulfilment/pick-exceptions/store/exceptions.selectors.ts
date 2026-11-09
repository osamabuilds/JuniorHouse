import { createFeatureSelector, createSelector } from '@ngrx/store';
import { EXCEPTIONS_FEATURE_KEY, ExceptionsState } from './exceptions.state';

const selectState = createFeatureSelector<ExceptionsState>(EXCEPTIONS_FEATURE_KEY);

export const selectRows = createSelector(selectState, (s) => s.rows);
export const selectTotal = createSelector(selectState, (s) => s.total);
export const selectPage = createSelector(selectState, (s) => s.page);
export const selectPageSize = createSelector(selectState, (s) => s.pageSize);
export const selectOpenOnly = createSelector(selectState, (s) => s.openOnly);
export const selectLoading = createSelector(selectState, (s) => s.loading);
export const selectResolvingId = createSelector(selectState, (s) => s.resolvingId);
export const selectError = createSelector(selectState, (s) => s.error);
export const selectNotice = createSelector(selectState, (s) => s.notice);

export const selectQuery = createSelector(selectOpenOnly, selectPage, selectPageSize, (openOnly, page, pageSize) => ({ openOnly, page, pageSize }));
