import { Routes } from '@angular/router';
import { provideEffects } from '@ngrx/effects';
import { provideState } from '@ngrx/store';
import * as searchEffects from './store/search.effects';
import { searchAdminReducer } from './store/search.reducer';
import { SEARCH_FEATURE_KEY } from './store/search.state';

/** The Search screens under Storefront: synonym groups (SRC-12) and the zero-result log (SRC-13). */
export const SEARCH_ROUTES: Routes = [
  {
    path: '',
    providers: [provideState(SEARCH_FEATURE_KEY, searchAdminReducer), provideEffects(searchEffects)],
    children: [
      {
        path: '',
        pathMatch: 'full',
        loadComponent: () => import('./containers/synonyms-page/synonyms-page.container').then((m) => m.SynonymsPageContainer),
        title: 'Synonyms · Search · Romp Admin',
      },
      {
        path: 'misses',
        loadComponent: () => import('./containers/misses-page/misses-page.container').then((m) => m.MissesPageContainer),
        title: 'Zero-result searches · Search · Romp Admin',
      },
    ],
  },
];
