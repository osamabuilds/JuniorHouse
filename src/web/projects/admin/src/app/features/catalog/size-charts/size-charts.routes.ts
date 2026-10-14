import { Routes } from '@angular/router';
import { provideEffects } from '@ngrx/effects';
import { provideState } from '@ngrx/store';
import * as sizeChartsEffects from './store/size-charts.effects';
import { sizeChartsReducer } from './store/size-charts.reducer';
import { SIZE_CHARTS_FEATURE_KEY } from './store/size-charts.state';

const page = () => import('./containers/size-charts-page/size-charts-page.container').then((m) => m.SizeChartsPageContainer);

/** The list and the editor are one screen; `/size-charts/:id` opens a chart for editing straight away. */
export const SIZE_CHARTS_ROUTES: Routes = [
  {
    path: '',
    providers: [provideState(SIZE_CHARTS_FEATURE_KEY, sizeChartsReducer), provideEffects(sizeChartsEffects)],
    children: [
      { path: '', loadComponent: page, title: 'Size charts · Romp Admin' },
      { path: ':id', loadComponent: page, title: 'Size chart · Romp Admin' },
    ],
  },
];
