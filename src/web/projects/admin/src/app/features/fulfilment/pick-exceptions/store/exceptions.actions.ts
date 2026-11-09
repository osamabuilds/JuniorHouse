import { createActionGroup, emptyProps, props } from '@ngrx/store';
import { ApiError } from '@core/http';
import { Page } from '@shared';
import { ExceptionRow, ResolveAction } from '../models';

export const ExceptionsPageActions = createActionGroup({
  source: 'Pick Exceptions Page',
  events: {
    Opened: emptyProps(),
    'Filter Changed': props<{ openOnly: boolean }>(),
    'Page Changed': props<{ page: number }>(),
    'Page Size Changed': props<{ pageSize: number }>(),
    Refreshed: emptyProps(),
    'Resolve Confirmed': props<{ id: number; action: ResolveAction }>(),
    'Notice Dismissed': emptyProps(),
  },
});

export const ExceptionsApiActions = createActionGroup({
  source: 'Pick Exceptions API',
  events: {
    'Load Succeeded': props<{ result: Page<ExceptionRow> }>(),
    'Load Failed': props<{ error: ApiError }>(),
    'Resolve Succeeded': props<{ row: ExceptionRow }>(),
    'Resolve Failed': props<{ error: ApiError }>(),
  },
});
