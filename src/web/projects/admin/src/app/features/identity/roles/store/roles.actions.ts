import { createActionGroup, emptyProps, props } from '@ngrx/store';
import { ApiError } from '@core/http';
import { RoleMatrixDto } from '../models';

export const RolesPageActions = createActionGroup({ source: 'Roles Page', events: { Opened: emptyProps() } });

export const RolesApiActions = createActionGroup({
  source: 'Roles API',
  events: { 'Load Succeeded': props<{ matrix: RoleMatrixDto }>(), 'Load Failed': props<{ error: ApiError }>() },
});
