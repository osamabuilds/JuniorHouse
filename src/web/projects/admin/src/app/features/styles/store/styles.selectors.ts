import { createFeatureSelector, createSelector } from '@ngrx/store';
import { StyleFilters } from '../models';
import { STYLES_FEATURE_KEY, StylesState } from './styles.state';

const selectStylesState = createFeatureSelector<StylesState>(STYLES_FEATURE_KEY);

export const selectStyles = createSelector(selectStylesState, (state) => state.styles);
export const selectStylesLoading = createSelector(selectStylesState, (state) => state.loading);
export const selectStylesError = createSelector(selectStylesState, (state) => state.error);
export const selectStylesMode = createSelector(selectStylesState, (state) => state.mode);
export const selectStylesSaving = createSelector(selectStylesState, (state) => state.saving);
export const selectEditingStyle = createSelector(selectStylesState, (state) => state.editingStyle);
export const selectSearchText = createSelector(selectStylesState, (state) => state.searchText);
export const selectCategoryFilter = createSelector(selectStylesState, (state) => state.categoryFilter);
export const selectActiveOnly = createSelector(selectStylesState, (state) => state.activeOnly);

export const selectStyleFilters = createSelector(
  selectSearchText,
  selectCategoryFilter,
  selectActiveOnly,
  (searchText, categoryId, activeOnly): StyleFilters => ({ searchText, categoryId, activeOnly }),
);
