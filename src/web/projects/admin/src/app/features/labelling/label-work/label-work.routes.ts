import { Routes } from '@angular/router';
import { provideEffects } from '@ngrx/effects';
import { provideState } from '@ngrx/store';
import * as labelWorkEffects from './store/label-work.effects';
import { labelWorkReducer } from './store/label-work.reducer';
import { LABEL_WORK_FEATURE_KEY } from './store/label-work.state';

export const LABEL_WORK_ROUTES: Routes = [
  {
    path: '',
    providers: [provideState(LABEL_WORK_FEATURE_KEY, labelWorkReducer), provideEffects(labelWorkEffects)],
    loadComponent: () => import('./containers/label-worklist-page/label-worklist-page.container').then((m) => m.LabelWorklistPageContainer),
    title: 'Label worklist · Romp Admin',
  },
];
