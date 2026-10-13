import { createFeatureSelector, createSelector } from '@ngrx/store';
import { VariantFilters } from '../models';
import { VARIANTS_FEATURE_KEY, VariantsState } from './variants.state';

const selectVariantsState = createFeatureSelector<VariantsState>(VARIANTS_FEATURE_KEY);

export const selectVariants = createSelector(selectVariantsState, (state) => state.variants);
export const selectTotal = createSelector(selectVariantsState, (state) => state.total);
export const selectPage = createSelector(selectVariantsState, (state) => state.page);
export const selectPageSize = createSelector(selectVariantsState, (state) => state.pageSize);
export const selectLoading = createSelector(selectVariantsState, (state) => state.loading);
export const selectError = createSelector(selectVariantsState, (state) => state.error);
export const selectSearchText = createSelector(selectVariantsState, (state) => state.searchText);
export const selectStatus = createSelector(selectVariantsState, (state) => state.status);
export const selectCapacity = createSelector(selectVariantsState, (state) => state.capacity);
export const selectDetail = createSelector(selectVariantsState, (state) => state.detail);
export const selectDetailLoading = createSelector(selectVariantsState, (state) => state.detailLoading);
export const selectVoidTarget = createSelector(selectVariantsState, (state) => state.voidTarget);
export const selectVoidSaving = createSelector(selectVariantsState, (state) => state.voidSaving);
export const selectVoidError = createSelector(selectVariantsState, (state) => state.voidError);
export const selectNotice = createSelector(selectVariantsState, (state) => state.notice);

export const selectFilters = createSelector(
  selectSearchText,
  selectStatus,
  selectPage,
  selectPageSize,
  (searchText, status, page, pageSize): VariantFilters => ({ searchText, status, page, pageSize }),
);
