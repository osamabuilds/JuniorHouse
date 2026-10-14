import { createFeatureSelector, createSelector } from '@ngrx/store';
import { ProductFilters } from '../models';
import { PRODUCTS_FEATURE_KEY, ProductsState } from './products.state';

const selectProductsState = createFeatureSelector<ProductsState>(PRODUCTS_FEATURE_KEY);

export const selectRows = createSelector(selectProductsState, (state) => state.rows);
export const selectTotal = createSelector(selectProductsState, (state) => state.total);
export const selectPage = createSelector(selectProductsState, (state) => state.page);
export const selectPageSize = createSelector(selectProductsState, (state) => state.pageSize);
export const selectSearch = createSelector(selectProductsState, (state) => state.search);
export const selectStatusId = createSelector(selectProductsState, (state) => state.statusId);
export const selectListLoading = createSelector(selectProductsState, (state) => state.listLoading);
export const selectListError = createSelector(selectProductsState, (state) => state.listError);
export const selectCreateOpen = createSelector(selectProductsState, (state) => state.createOpen);
export const selectCreating = createSelector(selectProductsState, (state) => state.creating);
export const selectCreateError = createSelector(selectProductsState, (state) => state.createError);

export const selectProduct = createSelector(selectProductsState, (state) => state.product);
export const selectProductVersion = createSelector(selectProduct, (product) => product?.version ?? 0);
export const selectImages = createSelector(selectProductsState, (state) => state.images);
export const selectComposition = createSelector(selectProductsState, (state) => state.composition);
export const selectChart = createSelector(selectProductsState, (state) => state.chart);
export const selectHistory = createSelector(selectProductsState, (state) => state.history);
export const selectSuggestion = createSelector(selectProductsState, (state) => state.suggestion);
export const selectLoading = createSelector(selectProductsState, (state) => state.loading);
export const selectSaving = createSelector(selectProductsState, (state) => state.saving);
export const selectError = createSelector(selectProductsState, (state) => state.error);
export const selectNotice = createSelector(selectProductsState, (state) => state.notice);
export const selectPublishFailures = createSelector(selectProductsState, (state) => state.publishFailures);

export const selectFilters = createSelector(
  selectSearch,
  selectStatusId,
  selectPage,
  selectPageSize,
  (search, statusId, page, pageSize): ProductFilters => ({ search, statusId, page, pageSize }),
);

/** Whether the product can still be edited: archived products are read-only. */
export const selectEditable = createSelector(selectProduct, (product) => product !== null && product.status !== 'Archived');
