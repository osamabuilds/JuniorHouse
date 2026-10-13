import { createFeatureSelector, createSelector } from '@ngrx/store';
import { LabelWorkFilters } from '../models';
import { LABEL_WORK_FEATURE_KEY, LabelWorkState } from './label-work.state';

const selectLabelWorkState = createFeatureSelector<LabelWorkState>(LABEL_WORK_FEATURE_KEY);

export const selectItems = createSelector(selectLabelWorkState, (state) => state.items);
export const selectTotal = createSelector(selectLabelWorkState, (state) => state.total);
export const selectPage = createSelector(selectLabelWorkState, (state) => state.page);
export const selectPageSize = createSelector(selectLabelWorkState, (state) => state.pageSize);
export const selectLoading = createSelector(selectLabelWorkState, (state) => state.loading);
export const selectError = createSelector(selectLabelWorkState, (state) => state.error);
export const selectStatus = createSelector(selectLabelWorkState, (state) => state.status);
export const selectPriceFlagOnly = createSelector(selectLabelWorkState, (state) => state.priceFlagOnly);
export const selectPrinters = createSelector(selectLabelWorkState, (state) => state.printers);
export const selectQueueItem = createSelector(selectLabelWorkState, (state) => state.queueItem);
export const selectQueueSaving = createSelector(selectLabelWorkState, (state) => state.queueSaving);
export const selectQueueError = createSelector(selectLabelWorkState, (state) => state.queueError);
export const selectResolvingId = createSelector(selectLabelWorkState, (state) => state.resolvingId);
export const selectNotice = createSelector(selectLabelWorkState, (state) => state.notice);

export const selectFilters = createSelector(
  selectStatus,
  selectPriceFlagOnly,
  selectPage,
  selectPageSize,
  (status, priceFlagOnly, page, pageSize): LabelWorkFilters => ({ status, priceFlagOnly, page, pageSize }),
);
