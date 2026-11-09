import { createFeatureSelector, createSelector } from '@ngrx/store';
import { ORDER_QUEUE_FEATURE_KEY, OrderQueueState } from './order-queue.state';

const selectState = createFeatureSelector<OrderQueueState>(ORDER_QUEUE_FEATURE_KEY);

export const selectOrders = createSelector(selectState, (s) => s.orders);
export const selectTotal = createSelector(selectState, (s) => s.total);
export const selectPage = createSelector(selectState, (s) => s.page);
export const selectPageSize = createSelector(selectState, (s) => s.pageSize);
export const selectFilters = createSelector(selectState, (s) => s.filters);
export const selectLoading = createSelector(selectState, (s) => s.loading);
export const selectDetail = createSelector(selectState, (s) => s.detail);
export const selectDetailLoading = createSelector(selectState, (s) => s.detailLoading);
export const selectActing = createSelector(selectState, (s) => s.acting);
export const selectError = createSelector(selectState, (s) => s.error);
export const selectNotice = createSelector(selectState, (s) => s.notice);

export const selectQuery = createSelector(selectFilters, selectPage, selectPageSize, (filters, page, pageSize) => ({ filters, page, pageSize }));
