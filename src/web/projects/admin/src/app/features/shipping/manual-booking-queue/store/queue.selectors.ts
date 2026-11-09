import { createFeatureSelector, createSelector } from '@ngrx/store';
import { QUEUE_FEATURE_KEY, QueueState } from './queue.state';

const selectState = createFeatureSelector<QueueState>(QUEUE_FEATURE_KEY);

export const selectRows = createSelector(selectState, (s) => s.rows);
export const selectTotal = createSelector(selectState, (s) => s.total);
export const selectPage = createSelector(selectState, (s) => s.page);
export const selectPageSize = createSelector(selectState, (s) => s.pageSize);
export const selectNeedsStaffOnly = createSelector(selectState, (s) => s.needsStaffOnly);
export const selectLoading = createSelector(selectState, (s) => s.loading);
export const selectBusyId = createSelector(selectState, (s) => s.busyId);
export const selectError = createSelector(selectState, (s) => s.error);
export const selectNotice = createSelector(selectState, (s) => s.notice);

export const selectQuery = createSelector(selectNeedsStaffOnly, selectPage, selectPageSize, (needsStaffOnly, page, pageSize) => ({ needsStaffOnly, page, pageSize }));
