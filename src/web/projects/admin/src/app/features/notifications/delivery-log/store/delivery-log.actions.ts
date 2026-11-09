import { createActionGroup, emptyProps, props } from '@ngrx/store';
import { ApiError } from '@core/http';
import { Page } from '@shared';
import { DeliveryDetail, DeliveryFilters, DeliveryLogRow, DeliverySummary } from '../models';

export const DeliveryLogPageActions = createActionGroup({
  source: 'Delivery Log Page',
  events: {
    /** The page opened, optionally for one order (the link from the order screen, DLG-9). */
    Opened: props<{ orderId: number | null }>(),
    'Filters Changed': props<{ filters: DeliveryFilters }>(),
    'Page Changed': props<{ page: number }>(),
    'Page Size Changed': props<{ pageSize: number }>(),
    Refreshed: emptyProps(),
    'Detail Opened': props<{ id: number }>(),
    'Detail Closed': emptyProps(),
    'Resend Confirmed': props<{ id: number; key: string; acknowledgeDuplicate: boolean }>(),
    'Notice Dismissed': emptyProps(),
  },
});

export const DeliveryLogApiActions = createActionGroup({
  source: 'Delivery Log API',
  events: {
    'Load Succeeded': props<{ result: Page<DeliveryLogRow> }>(),
    'Load Failed': props<{ error: ApiError }>(),
    'Summary Succeeded': props<{ summary: DeliverySummary }>(),
    'Summary Failed': props<{ error: ApiError }>(),
    'Detail Succeeded': props<{ detail: DeliveryDetail }>(),
    'Detail Failed': props<{ error: ApiError }>(),
    'Resend Succeeded': props<{ id: number }>(),
    'Resend Failed': props<{ error: ApiError }>(),
  },
});
