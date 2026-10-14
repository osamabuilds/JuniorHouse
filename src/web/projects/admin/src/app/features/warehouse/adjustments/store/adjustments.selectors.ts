import { createFeatureSelector, createSelector } from '@ngrx/store';
import { AdjustmentFilters } from '../models';
import { ADJUSTMENTS_FEATURE_KEY, AdjustmentsState } from './adjustments.state';

const selectAdjustmentsState = createFeatureSelector<AdjustmentsState>(ADJUSTMENTS_FEATURE_KEY);

export const selectAdjustments = createSelector(selectAdjustmentsState, (state) => state.adjustments);
export const selectTotal = createSelector(selectAdjustmentsState, (state) => state.total);
export const selectPage = createSelector(selectAdjustmentsState, (state) => state.page);
export const selectPageSize = createSelector(selectAdjustmentsState, (state) => state.pageSize);
export const selectLoading = createSelector(selectAdjustmentsState, (state) => state.loading);
export const selectError = createSelector(selectAdjustmentsState, (state) => state.error);
export const selectReasonId = createSelector(selectAdjustmentsState, (state) => state.reasonId);
export const selectReasons = createSelector(selectAdjustmentsState, (state) => state.reasons);
export const selectFormOpen = createSelector(selectAdjustmentsState, (state) => state.formOpen);
export const selectSaving = createSelector(selectAdjustmentsState, (state) => state.saving);
export const selectFormError = createSelector(selectAdjustmentsState, (state) => state.formError);
export const selectReconfirm = createSelector(selectAdjustmentsState, (state) => state.reconfirm);
export const selectNotice = createSelector(selectAdjustmentsState, (state) => state.notice);

export const selectFilters = createSelector(
  selectReasonId,
  selectPage,
  selectPageSize,
  (reasonId, page, pageSize): AdjustmentFilters => ({ reasonId, page, pageSize }),
);
