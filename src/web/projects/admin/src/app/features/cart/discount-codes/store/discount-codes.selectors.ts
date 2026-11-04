import { createFeatureSelector, createSelector } from '@ngrx/store';
import { DISCOUNT_CODES_FEATURE_KEY, DiscountCodesState } from './discount-codes.state';

const selectState = createFeatureSelector<DiscountCodesState>(DISCOUNT_CODES_FEATURE_KEY);

export const selectCodes = createSelector(selectState, (s) => s.codes);
export const selectTotal = createSelector(selectState, (s) => s.total);
export const selectPage = createSelector(selectState, (s) => s.page);
export const selectPageSize = createSelector(selectState, (s) => s.pageSize);
export const selectLoading = createSelector(selectState, (s) => s.loading);
export const selectSaving = createSelector(selectState, (s) => s.saving);
export const selectEditingId = createSelector(selectState, (s) => s.editingId);
export const selectSwitchingId = createSelector(selectState, (s) => s.switchingId);
export const selectError = createSelector(selectState, (s) => s.error);
export const selectNotice = createSelector(selectState, (s) => s.notice);

export const selectPaging = createSelector(selectPage, selectPageSize, (page, pageSize) => ({ page, pageSize }));
export const selectEditing = createSelector(selectCodes, selectEditingId, (codes, id) => codes.find((c) => c.id === id) ?? null);
