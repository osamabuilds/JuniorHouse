import { createActionGroup, emptyProps, props } from '@ngrx/store';
import { ApiError } from '@core/http';
import { Page } from '@shared';
import { JobStatusFilter, PrintJobDto, PrinterDto, ReprintValue } from '../models';

export const PrintJobsPageActions = createActionGroup({
  source: 'Print Jobs Page',
  events: {
    Opened: props<{ variantId: number | null; reprintSku: string | null; reprint: boolean }>(),
    'Status Changed': props<{ status: JobStatusFilter }>(),
    'Page Changed': props<{ page: number }>(),
    'Page Size Changed': props<{ pageSize: number }>(),
    'Refresh Requested': emptyProps(),
    'Auto Refresh Ticked': emptyProps(),
    'History Cleared': emptyProps(),
    'Retry Submitted': props<{ id: number; actingPerson: string }>(),
    'Cancel Submitted': props<{ id: number; actingPerson: string }>(),
    'Reprint Form Closed': emptyProps(),
    'Reprint Submitted': props<{ variantId: number; value: ReprintValue }>(),
    'Notice Dismissed': emptyProps(),
  },
});

export const PrintJobsApiActions = createActionGroup({
  source: 'Print Jobs API',
  events: {
    'Load Jobs Succeeded': props<{ result: Page<PrintJobDto> }>(),
    'Load Jobs Failed': props<{ error: ApiError }>(),
    'Load Printers Succeeded': props<{ printers: readonly PrinterDto[] }>(),
    'Load Printers Failed': props<{ error: ApiError }>(),
    'Job Action Succeeded': props<{ job: PrintJobDto; verb: string }>(),
    'Job Action Failed': props<{ error: ApiError }>(),
    'Reprint Succeeded': props<{ job: PrintJobDto }>(),
    'Reprint Failed': props<{ error: ApiError }>(),
  },
});
