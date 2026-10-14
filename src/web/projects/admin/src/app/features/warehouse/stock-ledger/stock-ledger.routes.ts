import { Routes } from '@angular/router';
import { provideEffects } from '@ngrx/effects';
import { provideState } from '@ngrx/store';
import * as stockEffects from './store/stock.effects';
import { stockReducer } from './store/stock.reducer';
import { STOCK_FEATURE_KEY } from './store/stock.state';

export const STOCK_LEDGER_ROUTES: Routes = [
  {
    path: '',
    providers: [provideState(STOCK_FEATURE_KEY, stockReducer), provideEffects(stockEffects)],
    loadComponent: () => import('./containers/stock-page/stock-page.container').then((m) => m.StockPageContainer),
    title: 'Stock · Romp Admin',
  },
];
