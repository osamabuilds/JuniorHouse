import { createFeatureSelector, createSelector } from '@ngrx/store';
import { SizeChartFilters } from '../models';
import { SIZE_CHARTS_FEATURE_KEY, SizeChartsState } from './size-charts.state';

const selectSizeChartsState = createFeatureSelector<SizeChartsState>(SIZE_CHARTS_FEATURE_KEY);

export const selectRows = createSelector(selectSizeChartsState, (state) => state.rows);
export const selectTotal = createSelector(selectSizeChartsState, (state) => state.total);
export const selectPage = createSelector(selectSizeChartsState, (state) => state.page);
export const selectPageSize = createSelector(selectSizeChartsState, (state) => state.pageSize);
export const selectLoading = createSelector(selectSizeChartsState, (state) => state.loading);
export const selectError = createSelector(selectSizeChartsState, (state) => state.error);
export const selectMode = createSelector(selectSizeChartsState, (state) => state.mode);
export const selectChart = createSelector(selectSizeChartsState, (state) => state.chart);
export const selectSaving = createSelector(selectSizeChartsState, (state) => state.saving);
export const selectFormError = createSelector(selectSizeChartsState, (state) => state.formError);
export const selectNotice = createSelector(selectSizeChartsState, (state) => state.notice);

export const selectFilters = createSelector(selectPage, selectPageSize, (page, pageSize): SizeChartFilters => ({ page, pageSize }));
