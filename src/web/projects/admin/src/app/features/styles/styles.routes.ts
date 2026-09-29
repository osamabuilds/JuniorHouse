import { Routes } from '@angular/router';
import { provideEffects } from '@ngrx/effects';
import { provideState } from '@ngrx/store';
import * as stylesEffects from './store/styles.effects';
import { stylesReducer } from './store/styles.reducer';
import { STYLES_FEATURE_KEY } from './store/styles.state';

export const STYLES_ROUTES: Routes = [
  {
    path: '',
    providers: [provideState(STYLES_FEATURE_KEY, stylesReducer), provideEffects(stylesEffects)],
    loadComponent: () => import('./containers/styles-page/styles-page.container').then((m) => m.StylesPageContainer),
    title: 'Styles · Romp Admin',
  },
];
