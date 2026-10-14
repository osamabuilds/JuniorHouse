import { alertRow, pageOf, thresholdRow } from '@app/testing/warehouse-fixtures';
import { StockAlertsApiActions, StockAlertsPageActions } from './stock-alerts.actions';
import { stockAlertsReducer } from './stock-alerts.reducer';
import { initialStockAlertsState } from './stock-alerts.state';

describe('stockAlertsReducer (spec ALR-5, ALR-6, ALR-8)', () => {
  it('opening the page starts from a clean slate and loads both lists', () => {
    const state = stockAlertsReducer({ ...initialStockAlertsState, level: 'OUT', alertsPage: 3, notice: 'old' }, StockAlertsPageActions.opened());

    expect(state).toMatchObject({ level: 'all', alertsPage: 1, notice: null, alertsLoading: true, thresholdsLoading: true });
  });

  it('filtering goes back to the first page of alerts and reloads (ALR-8)', () => {
    const byLevel = stockAlertsReducer({ ...initialStockAlertsState, alertsPage: 4 }, StockAlertsPageActions.levelChanged({ level: 'OUT' }));
    expect(byLevel).toMatchObject({ level: 'OUT', alertsPage: 1, alertsLoading: true });

    const unseen = stockAlertsReducer({ ...initialStockAlertsState, alertsPage: 4 }, StockAlertsPageActions.unacknowledgedOnlyChanged({ unacknowledgedOnly: true }));
    expect(unseen).toMatchObject({ unacknowledgedOnly: true, alertsPage: 1 });
  });

  it('searching thresholds goes back to their first page and leaves the alerts alone', () => {
    const state = stockAlertsReducer({ ...initialStockAlertsState, thresholdsPage: 5, alertsPage: 2 }, StockAlertsPageActions.searchChanged({ searchText: 'ROMP' }));

    expect(state).toMatchObject({ searchText: 'ROMP', thresholdsPage: 1, thresholdsLoading: true, alertsPage: 2 });
  });

  it('keeps both loaded pages', () => {
    const alerts = stockAlertsReducer(initialStockAlertsState, StockAlertsApiActions.loadAlertsSucceeded({ result: pageOf([alertRow()], { totalCount: 12 }) }));
    const both = stockAlertsReducer(alerts, StockAlertsApiActions.loadThresholdsSucceeded({ result: pageOf([thresholdRow()], { totalCount: 30 }) }));

    expect(both).toMatchObject({ alertsTotal: 12, thresholdsTotal: 30, alertsLoading: false, thresholdsLoading: false });
  });

  it('acknowledging replaces just that alert, says who saw it, and the alert stays open (ALR-5)', () => {
    const start = { ...initialStockAlertsState, alerts: [alertRow({ id: 90 }), alertRow({ id: 91, skuCode: 'OTHER' })], acknowledgingId: 90 };

    const state = stockAlertsReducer(start, StockAlertsApiActions.acknowledgeSucceeded({ alert: alertRow({ id: 90, acknowledged: true, acknowledgedBy: 'Ayesha Khan' }) }));

    expect(state.alerts[0].acknowledged).toBe(true);
    expect(state.alerts[1].acknowledged).toBe(false);
    expect(state.acknowledgingId).toBeNull();
    expect(state.notice).toContain('acknowledged by Ayesha Khan');
    expect(state.notice).toContain('stays open until stock recovers');
  });

  it('saving a threshold updates its row, announces it, and reloads the alerts because it can open or close one (ALR-6)', () => {
    const start = { ...initialStockAlertsState, thresholds: [thresholdRow()], savingVariantId: 31 };

    const state = stockAlertsReducer(start, StockAlertsApiActions.thresholdSucceeded({ threshold: thresholdRow({ effectiveThreshold: 2, isOverride: true }) }));

    expect(state.thresholds[0]).toMatchObject({ effectiveThreshold: 2, isOverride: true });
    expect(state).toMatchObject({ savingVariantId: null, alertsLoading: true });
    expect(state.notice).toContain('threshold for ROMP-STY-DRESS-RED-2-3Y is now 2');
  });

  it('a refused threshold shows the server\'s reason and frees the row (ALR-6)', () => {
    const error = { status: 400, message: 'A low-stock threshold is a whole number from 0 to 100000.', fieldErrors: {} };

    const state = stockAlertsReducer({ ...initialStockAlertsState, savingVariantId: 31 }, StockAlertsApiActions.thresholdFailed({ error }));

    expect(state).toMatchObject({ savingVariantId: null, error });
  });
});
