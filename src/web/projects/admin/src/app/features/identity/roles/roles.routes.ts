import { Routes } from '@angular/router';
import { provideEffects } from '@ngrx/effects';
import { provideState } from '@ngrx/store';
import { permissionGuard } from '@core/auth';
import * as rolesEffects from './store/roles.effects';
import { rolesReducer } from './store/roles.reducer';
import { ROLES_FEATURE_KEY } from './store/roles.state';

export const ROLES_ROUTES: Routes = [
  {
    path: 'roles',
    canActivate: [permissionGuard('staff.manage')],
    providers: [provideState(ROLES_FEATURE_KEY, rolesReducer), provideEffects(rolesEffects)],
    loadComponent: () => import('./containers/roles-page/roles-page.container').then((m) => m.RolesPageContainer),
    title: 'Roles · Romp Admin',
  },
  {
    path: 'forbidden',
    loadComponent: () => import('./containers/forbidden-page/forbidden-page.container').then((m) => m.ForbiddenPageContainer),
    title: 'No access · Romp Admin',
  },
];
