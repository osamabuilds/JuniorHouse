import { Routes } from '@angular/router';
import { provideEffects } from '@ngrx/effects';
import { provideState } from '@ngrx/store';
import { permissionGuard } from '@core/auth';
import * as auditEffects from './store/audit.effects';
import { auditReducer } from './store/audit.reducer';
import { AUDIT_FEATURE_KEY } from './store/audit.state';

export const AUDIT_TRAIL_ROUTES: Routes = [
  {
    path: 'audit',
    canActivate: [permissionGuard('audit.read')],
    providers: [provideState(AUDIT_FEATURE_KEY, auditReducer), provideEffects(auditEffects)],
    loadComponent: () => import('./containers/audit-page/audit-page.container').then((m) => m.AuditPageContainer),
    title: 'Audit trail · Romp Admin',
  },
];
