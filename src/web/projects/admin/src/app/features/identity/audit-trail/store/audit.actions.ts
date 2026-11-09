import { createActionGroup, emptyProps, props } from '@ngrx/store';
import { ApiError } from '@core/http';
import { Page } from '@shared';
import { AuditEntryDto, AuditFormValue } from '../models';

export const AuditPageActions = createActionGroup({
  source: 'Audit Page',
  events: {
    Opened: emptyProps(),
    'Filters Applied': props<{ form: AuditFormValue }>(),
    'Page Changed': props<{ page: number }>(),
    'Page Size Changed': props<{ pageSize: number }>(),
    'Entry Selected': props<{ entry: AuditEntryDto }>(),
    'Detail Closed': emptyProps(),
  },
});

export const AuditApiActions = createActionGroup({
  source: 'Audit API',
  events: { 'Load Succeeded': props<{ result: Page<AuditEntryDto> }>(), 'Load Failed': props<{ error: ApiError }>() },
});
