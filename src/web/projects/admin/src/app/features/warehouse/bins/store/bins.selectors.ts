import { createFeatureSelector, createSelector } from '@ngrx/store';
import { BinFilters } from '../models';
import { BINS_FEATURE_KEY, BinsState } from './bins.state';

const selectBinsState = createFeatureSelector<BinsState>(BINS_FEATURE_KEY);

export const selectBins = createSelector(selectBinsState, (state) => state.bins);
export const selectTotal = createSelector(selectBinsState, (state) => state.total);
export const selectPage = createSelector(selectBinsState, (state) => state.page);
export const selectPageSize = createSelector(selectBinsState, (state) => state.pageSize);
export const selectLoading = createSelector(selectBinsState, (state) => state.loading);
export const selectError = createSelector(selectBinsState, (state) => state.error);
export const selectZone = createSelector(selectBinsState, (state) => state.zone);
export const selectFormOpen = createSelector(selectBinsState, (state) => state.formOpen);
export const selectSaving = createSelector(selectBinsState, (state) => state.saving);
export const selectFormError = createSelector(selectBinsState, (state) => state.formError);
export const selectTogglingId = createSelector(selectBinsState, (state) => state.togglingId);
export const selectNotice = createSelector(selectBinsState, (state) => state.notice);

export const selectFilters = createSelector(
  selectZone,
  selectPage,
  selectPageSize,
  (zone, page, pageSize): BinFilters => ({ zone, page, pageSize }),
);
