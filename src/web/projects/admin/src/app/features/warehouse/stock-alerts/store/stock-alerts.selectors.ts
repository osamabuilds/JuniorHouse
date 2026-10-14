import { createFeatureSelector, createSelector } from '@ngrx/store';
import { AlertFilters, ThresholdFilters } from '../models';
import { STOCK_ALERTS_FEATURE_KEY, StockAlertsState } from './stock-alerts.state';

const selectState = createFeatureSelector<StockAlertsState>(STOCK_ALERTS_FEATURE_KEY);

export const selectAlerts = createSelector(selectState, (state) => state.alerts);
export const selectAlertsTotal = createSelector(selectState, (state) => state.alertsTotal);
export const selectAlertsPage = createSelector(selectState, (state) => state.alertsPage);
export const selectAlertsLoading = createSelector(selectState, (state) => state.alertsLoading);
export const selectLevel = createSelector(selectState, (state) => state.level);
export const selectUnacknowledgedOnly = createSelector(selectState, (state) => state.unacknowledgedOnly);
export const selectAcknowledgingId = createSelector(selectState, (state) => state.acknowledgingId);
export const selectThresholds = createSelector(selectState, (state) => state.thresholds);
export const selectThresholdsTotal = createSelector(selectState, (state) => state.thresholdsTotal);
export const selectThresholdsPage = createSelector(selectState, (state) => state.thresholdsPage);
export const selectThresholdsLoading = createSelector(selectState, (state) => state.thresholdsLoading);
export const selectSearchText = createSelector(selectState, (state) => state.searchText);
export const selectSavingVariantId = createSelector(selectState, (state) => state.savingVariantId);
export const selectPageSize = createSelector(selectState, (state) => state.pageSize);
export const selectError = createSelector(selectState, (state) => state.error);
export const selectNotice = createSelector(selectState, (state) => state.notice);

export const selectAlertFilters = createSelector(
  selectLevel,
  selectUnacknowledgedOnly,
  selectAlertsPage,
  selectPageSize,
  (level, unacknowledgedOnly, page, pageSize): AlertFilters => ({ level, unacknowledgedOnly, page, pageSize }),
);

export const selectThresholdFilters = createSelector(
  selectSearchText,
  selectThresholdsPage,
  selectPageSize,
  (searchText, page, pageSize): ThresholdFilters => ({ searchText, page, pageSize }),
);
