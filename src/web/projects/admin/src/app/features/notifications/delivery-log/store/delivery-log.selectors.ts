import { createFeatureSelector, createSelector } from '@ngrx/store';
import { DELIVERY_LOG_FEATURE_KEY, DeliveryLogState } from './delivery-log.state';

const selectState = createFeatureSelector<DeliveryLogState>(DELIVERY_LOG_FEATURE_KEY);

export const selectRows = createSelector(selectState, (s) => s.rows);
export const selectTotal = createSelector(selectState, (s) => s.total);
export const selectPage = createSelector(selectState, (s) => s.page);
export const selectPageSize = createSelector(selectState, (s) => s.pageSize);
export const selectFilters = createSelector(selectState, (s) => s.filters);
export const selectLoading = createSelector(selectState, (s) => s.loading);
export const selectSummary = createSelector(selectState, (s) => s.summary);
export const selectDetail = createSelector(selectState, (s) => s.detail);
export const selectDetailLoading = createSelector(selectState, (s) => s.detailLoading);
export const selectResending = createSelector(selectState, (s) => s.resending);
export const selectError = createSelector(selectState, (s) => s.error);
export const selectNotice = createSelector(selectState, (s) => s.notice);

export const selectQuery = createSelector(selectFilters, selectPage, selectPageSize, (filters, page, pageSize) => ({ filters, page, pageSize }));
