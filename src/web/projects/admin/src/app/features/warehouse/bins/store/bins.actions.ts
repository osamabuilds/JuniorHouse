import { createActionGroup, emptyProps, props } from '@ngrx/store';
import { ApiError } from '@core/http';
import { Page } from '@shared';
import { BinRowDto, CreateBinValue } from '../models';

export const BinsPageActions = createActionGroup({
  source: 'Bins Page',
  events: {
    Opened: emptyProps(),
    'Zone Changed': props<{ zone: string }>(),
    'Page Changed': props<{ page: number }>(),
    'Page Size Changed': props<{ pageSize: number }>(),
    'Refresh Requested': emptyProps(),
    'Form Opened': emptyProps(),
    'Form Closed': emptyProps(),
    'Create Submitted': props<{ value: CreateBinValue }>(),
    'Active Toggled': props<{ bin: BinRowDto; actingPerson: string }>(),
    'Notice Dismissed': emptyProps(),
  },
});

export const BinsApiActions = createActionGroup({
  source: 'Bins API',
  events: {
    'Load Bins Succeeded': props<{ result: Page<BinRowDto> }>(),
    'Load Bins Failed': props<{ error: ApiError }>(),
    'Create Succeeded': props<{ bin: BinRowDto }>(),
    'Create Failed': props<{ error: ApiError }>(),
    'Toggle Succeeded': props<{ bin: BinRowDto }>(),
    'Toggle Failed': props<{ error: ApiError }>(),
  },
});
