import { createFeatureSelector, createSelector } from '@ngrx/store';
import { BOOKINGS_FEATURE_KEY, BookingsState } from './bookings.state';

const selectState = createFeatureSelector<BookingsState>(BOOKINGS_FEATURE_KEY);

export const selectRows = createSelector(selectState, (s) => s.rows);
export const selectTotal = createSelector(selectState, (s) => s.total);
export const selectPage = createSelector(selectState, (s) => s.page);
export const selectPageSize = createSelector(selectState, (s) => s.pageSize);
export const selectStatus = createSelector(selectState, (s) => s.status);
export const selectLoading = createSelector(selectState, (s) => s.loading);
export const selectDetail = createSelector(selectState, (s) => s.detail);
export const selectDetailLoading = createSelector(selectState, (s) => s.detailLoading);
export const selectRetrying = createSelector(selectState, (s) => s.retrying);
export const selectError = createSelector(selectState, (s) => s.error);
export const selectNotice = createSelector(selectState, (s) => s.notice);

export const selectQuery = createSelector(selectStatus, selectPage, selectPageSize, (status, page, pageSize) => ({ status, page, pageSize }));
