import { createActionGroup, emptyProps, props } from '@ngrx/store';
import { ApiError } from '@core/http';
import { Page } from '@shared';
import { QueueRow } from '../models';

export const QueuePageActions = createActionGroup({
  source: 'Manual Queue Page',
  events: {
    Opened: emptyProps(),
    'Filter Changed': props<{ needsStaffOnly: boolean }>(),
    'Page Changed': props<{ page: number }>(),
    'Page Size Changed': props<{ pageSize: number }>(),
    Refreshed: emptyProps(),
    'Retry Clicked': props<{ id: number }>(),
    'Waybill Entered': props<{ id: number; courier: string; waybill: string }>(),
    'Notice Dismissed': emptyProps(),
  },
});

export const QueueApiActions = createActionGroup({
  source: 'Manual Queue API',
  events: {
    'Load Succeeded': props<{ result: Page<QueueRow> }>(),
    'Load Failed': props<{ error: ApiError }>(),
    'Action Succeeded': props<{ message: string }>(),
    'Action Failed': props<{ error: ApiError }>(),
  },
});
