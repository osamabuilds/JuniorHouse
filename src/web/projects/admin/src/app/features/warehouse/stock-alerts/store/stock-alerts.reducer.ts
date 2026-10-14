import { createReducer, on } from '@ngrx/store';
import { StockAlertsApiActions, StockAlertsPageActions } from './stock-alerts.actions';
import { initialStockAlertsState } from './stock-alerts.state';

export const stockAlertsReducer = createReducer(
  initialStockAlertsState,

  // The store outlives the page, so opening it starts from a clean slate.
  on(StockAlertsPageActions.opened, () => ({ ...initialStockAlertsState, alertsLoading: true, thresholdsLoading: true })),
  on(StockAlertsPageActions.levelChanged, (state, { level }) => ({ ...state, level, alertsPage: 1, alertsLoading: true, error: null })),
  on(StockAlertsPageActions.unacknowledgedOnlyChanged, (state, { unacknowledgedOnly }) => ({
    ...state,
    unacknowledgedOnly,
    alertsPage: 1,
    alertsLoading: true,
    error: null,
  })),
  on(StockAlertsPageActions.alertsPageChanged, (state, { page }) => ({ ...state, alertsPage: page, alertsLoading: true, error: null })),
  on(StockAlertsPageActions.searchChanged, (state, { searchText }) => ({ ...state, searchText, thresholdsPage: 1, thresholdsLoading: true, error: null })),
  on(StockAlertsPageActions.thresholdsPageChanged, (state, { page }) => ({ ...state, thresholdsPage: page, thresholdsLoading: true, error: null })),
  on(StockAlertsPageActions.refreshRequested, (state) => ({ ...state, alertsLoading: true, thresholdsLoading: true, error: null })),
  on(StockAlertsPageActions.acknowledgeSubmitted, (state, { id }) => ({ ...state, acknowledgingId: id, error: null, notice: null })),
  on(StockAlertsPageActions.thresholdSubmitted, (state, { variantRefId }) => ({ ...state, savingVariantId: variantRefId, error: null, notice: null })),
  on(StockAlertsPageActions.noticeDismissed, (state) => ({ ...state, notice: null })),

  on(StockAlertsApiActions.loadAlertsSucceeded, (state, { result }) => ({
    ...state,
    alerts: result.items,
    alertsTotal: result.totalCount,
    alertsPage: result.page,
    alertsLoading: false,
  })),
  on(StockAlertsApiActions.loadAlertsFailed, (state, { error }) => ({ ...state, error, alertsLoading: false })),
  on(StockAlertsApiActions.loadThresholdsSucceeded, (state, { result }) => ({
    ...state,
    thresholds: result.items,
    thresholdsTotal: result.totalCount,
    thresholdsPage: result.page,
    thresholdsLoading: false,
  })),
  on(StockAlertsApiActions.loadThresholdsFailed, (state, { error }) => ({ ...state, error, thresholdsLoading: false })),
  on(StockAlertsApiActions.acknowledgeSucceeded, (state, { alert }) => ({
    ...state,
    acknowledgingId: null,
    alerts: state.alerts.map((a) => (a.id === alert.id ? alert : a)),
    notice: `Alert for ${alert.skuCode} acknowledged by ${alert.acknowledgedBy}. It stays open until stock recovers.`,
  })),
  on(StockAlertsApiActions.acknowledgeFailed, (state, { error }) => ({ ...state, acknowledgingId: null, error })),
  on(StockAlertsApiActions.thresholdSucceeded, (state, { threshold }) => ({
    ...state,
    savingVariantId: null,
    thresholds: state.thresholds.map((t) => (t.variantRefId === threshold.variantRefId ? threshold : t)),
    alertsLoading: true,
    notice: `The low-stock threshold for ${threshold.skuCode} is now ${threshold.effectiveThreshold}.`,
  })),
  on(StockAlertsApiActions.thresholdFailed, (state, { error }) => ({ ...state, savingVariantId: null, error })),
);
