import { createActionGroup, emptyProps, props } from '@ngrx/store';
import { ApiError } from '@core/http';
import { Page } from '@shared';
import { NotifyDemandRow } from '../models';

export const NotifyDemandPageActions = createActionGroup({
  source: 'Notify Demand Page',
  events: {
    Opened: emptyProps(),
    'Page Changed': props<{ page: number }>(),
    'Page Size Changed': props<{ pageSize: number }>(),
    Refreshed: emptyProps(),
  },
});

export const NotifyDemandApiActions = createActionGroup({
  source: 'Notify Demand API',
  events: {
    'Load Succeeded': props<{ result: Page<NotifyDemandRow> }>(),
    'Load Failed': props<{ error: ApiError }>(),
  },
});
