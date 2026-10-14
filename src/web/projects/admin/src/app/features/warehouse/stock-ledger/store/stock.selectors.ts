import { createFeatureSelector, createSelector } from '@ngrx/store';
import { StockFilters } from '../models';
import { STOCK_FEATURE_KEY, StockState } from './stock.state';

const selectStockState = createFeatureSelector<StockState>(STOCK_FEATURE_KEY);

export const selectRows = createSelector(selectStockState, (state) => state.rows);
export const selectTotal = createSelector(selectStockState, (state) => state.total);
export const selectPage = createSelector(selectStockState, (state) => state.page);
export const selectPageSize = createSelector(selectStockState, (state) => state.pageSize);
export const selectLoading = createSelector(selectStockState, (state) => state.loading);
export const selectError = createSelector(selectStockState, (state) => state.error);
export const selectSearchText = createSelector(selectStockState, (state) => state.searchText);
export const selectHistoryFor = createSelector(selectStockState, (state) => state.historyFor);
export const selectMovements = createSelector(selectStockState, (state) => state.movements);
export const selectMovementsTotal = createSelector(selectStockState, (state) => state.movementsTotal);
export const selectMovementsPage = createSelector(selectStockState, (state) => state.movementsPage);
export const selectMovementsLoading = createSelector(selectStockState, (state) => state.movementsLoading);

export const selectFilters = createSelector(
  selectSearchText,
  selectPage,
  selectPageSize,
  (searchText, page, pageSize): StockFilters => ({ searchText, page, pageSize }),
);
