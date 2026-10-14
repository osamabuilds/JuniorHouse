import { createActionGroup, emptyProps, props } from '@ngrx/store';
import { ApiError } from '@core/http';
import { Page } from '@shared';
import { LabelWorkItemDto, PrinterDto, QueueTagsValue, QueuedJobDto, ResolveWorkItemValue, WorkStatusFilter } from '../models';

export const LabelWorkPageActions = createActionGroup({
  source: 'Label Work Page',
  events: {
    Opened: emptyProps(),
    'Status Changed': props<{ status: WorkStatusFilter }>(),
    'Price Flag Toggled': emptyProps(),
    'Page Changed': props<{ page: number }>(),
    'Page Size Changed': props<{ pageSize: number }>(),
    'Refresh Requested': emptyProps(),
    'Queue Form Opened': props<{ item: LabelWorkItemDto }>(),
    'Queue Form Closed': emptyProps(),
    'Queue Submitted': props<{ itemId: number; value: QueueTagsValue }>(),
    'Resolve Submitted': props<{ itemId: number; value: ResolveWorkItemValue }>(),
    'Notice Dismissed': emptyProps(),
  },
});

export const LabelWorkApiActions = createActionGroup({
  source: 'Label Work API',
  events: {
    'Load Items Succeeded': props<{ result: Page<LabelWorkItemDto> }>(),
    'Load Items Failed': props<{ error: ApiError }>(),
    'Load Printers Succeeded': props<{ printers: readonly PrinterDto[] }>(),
    'Load Printers Failed': props<{ error: ApiError }>(),
    'Queue Succeeded': props<{ job: QueuedJobDto }>(),
    'Queue Failed': props<{ error: ApiError }>(),
    'Resolve Succeeded': props<{ item: LabelWorkItemDto }>(),
    'Resolve Failed': props<{ error: ApiError }>(),
  },
});
