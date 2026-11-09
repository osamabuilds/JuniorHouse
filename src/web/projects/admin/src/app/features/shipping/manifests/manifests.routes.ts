import { Routes } from '@angular/router';
import { provideEffects } from '@ngrx/effects';
import { provideState } from '@ngrx/store';
import * as manifestsEffects from './store/manifests.store';
import { MANIFESTS_FEATURE_KEY, manifestsReducer } from './store/manifests.store';

const effects = {
  loadList$: manifestsEffects.loadList$,
  generate$: manifestsEffects.generate$,
  loadDetail$: manifestsEffects.loadDetail$,
  count$: manifestsEffects.count$,
  handover$: manifestsEffects.handover$,
  resolve$: manifestsEffects.resolve$,
};

export const MANIFESTS_ROUTES: Routes = [
  {
    path: '',
    providers: [provideState(MANIFESTS_FEATURE_KEY, manifestsReducer), provideEffects(effects)],
    children: [
      {
        path: '',
        loadComponent: () => import('./containers/manifests-page/manifests-page.container').then((m) => m.ManifestsPageContainer),
        title: 'Manifests · Romp Admin',
      },
      {
        path: ':id',
        loadComponent: () => import('./containers/manifest-detail-page/manifest-detail-page.container').then((m) => m.ManifestDetailPageContainer),
        title: 'Manifest · Romp Admin',
      },
    ],
  },
];
