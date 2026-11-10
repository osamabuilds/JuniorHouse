import { Routes } from '@angular/router';
import { provideEffects } from '@ngrx/effects';
import { provideState } from '@ngrx/store';
import * as codEffects from './store/cod.store';
import { COD_FEATURE_KEY, codReducer } from './store/cod.store';

const effects = {
  loadSummary$: codEffects.loadSummary$,
  loadAwaiting$: codEffects.loadAwaiting$,
  loadExceptions$: codEffects.loadExceptions$,
  loadRemittances$: codEffects.loadRemittances$,
  loadDetail$: codEffects.loadDetail$,
  record$: codEffects.record$,
  resolveFromList$: codEffects.resolveFromList$,
  resolveFromDetail$: codEffects.resolveFromDetail$,
};

export const COD_RECONCILIATION_ROUTES: Routes = [
  {
    path: '',
    providers: [provideState(COD_FEATURE_KEY, codReducer), provideEffects(effects)],
    children: [
      {
        path: '',
        loadComponent: () => import('./containers/cod-page/cod-page.container').then((m) => m.CodPageContainer),
        title: 'COD reconciliation · Romp Admin',
      },
      {
        path: 'remittances/new',
        loadComponent: () => import('./containers/remittance-form-page/remittance-form-page.container').then((m) => m.RemittanceFormPageContainer),
        title: 'Record a courier statement · Romp Admin',
      },
      {
        path: 'remittances/:id',
        loadComponent: () => import('./containers/remittance-detail-page/remittance-detail-page.container').then((m) => m.RemittanceDetailPageContainer),
        title: 'Courier statement · Romp Admin',
      },
    ],
  },
];
