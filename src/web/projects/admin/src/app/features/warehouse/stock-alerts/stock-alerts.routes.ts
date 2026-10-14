import { Routes } from '@angular/router';
import { provideEffects } from '@ngrx/effects';
import { provideState } from '@ngrx/store';
import * as stockAlertsEffects from './store/stock-alerts.effects';
import { stockAlertsReducer } from './store/stock-alerts.reducer';
import { STOCK_ALERTS_FEATURE_KEY } from './store/stock-alerts.state';

export const STOCK_ALERTS_ROUTES: Routes = [
  {
    path: '',
    providers: [provideState(STOCK_ALERTS_FEATURE_KEY, stockAlertsReducer), provideEffects(stockAlertsEffects)],
    loadComponent: () => import('./containers/stock-alerts-page/stock-alerts-page.container').then((m) => m.StockAlertsPageContainer),
    title: 'Alerts · Romp Admin',
  },
];
