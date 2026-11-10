import { Routes } from '@angular/router';
import { provideEffects } from '@ngrx/effects';
import { provideState } from '@ngrx/store';
import * as reverseQcEffects from './store/reverse-qc.store';
import { REVERSE_QC_FEATURE_KEY, reverseQcReducer } from './store/reverse-qc.store';

const effects = {
  loadCases$: reverseQcEffects.loadCases$,
  loadCase$: reverseQcEffects.loadCase$,
  receive$: reverseQcEffects.receive$,
  decide$: reverseQcEffects.decide$,
  complete$: reverseQcEffects.complete$,
};

export const REVERSE_QC_ROUTES: Routes = [
  {
    path: '',
    providers: [provideState(REVERSE_QC_FEATURE_KEY, reverseQcReducer), provideEffects(effects)],
    children: [
      {
        path: '',
        loadComponent: () => import('./containers/cases-page/cases-page.container').then((m) => m.CasesPageContainer),
        title: 'Returned parcels · Romp Admin',
      },
      {
        path: ':id',
        loadComponent: () => import('./containers/case-detail-page/case-detail-page.container').then((m) => m.CaseDetailPageContainer),
        title: 'Returned parcel · Romp Admin',
      },
    ],
  },
];
