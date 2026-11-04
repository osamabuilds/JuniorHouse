import { createFeatureSelector, createSelector } from '@ngrx/store';
import { NOTIFY_DEMAND_FEATURE_KEY, NotifyDemandState } from './notify-demand.state';

const selectState = createFeatureSelector<NotifyDemandState>(NOTIFY_DEMAND_FEATURE_KEY);

export const selectRows = createSelector(selectState, (s) => s.rows);
export const selectTotal = createSelector(selectState, (s) => s.total);
export const selectPage = createSelector(selectState, (s) => s.page);
export const selectPageSize = createSelector(selectState, (s) => s.pageSize);
export const selectLoading = createSelector(selectState, (s) => s.loading);
export const selectError = createSelector(selectState, (s) => s.error);

export const selectQuery = createSelector(selectPage, selectPageSize, (page, pageSize) => ({ page, pageSize }));
