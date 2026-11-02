import { createActionGroup, emptyProps, props } from '@ngrx/store';
import { ApiError } from '@core/http';
import { Page } from '@shared';
import { CodOutcomeChoice, OrderDetail, OrderFilters, OrderListItem } from '../models';

export const OrderQueueActions = createActionGroup({
  source: 'Order Queue',
  events: {
    'List Opened': emptyProps(),
    'Filters Changed': props<{ filters: OrderFilters }>(),
    'Page Changed': props<{ page: number }>(),
    'Page Size Changed': props<{ pageSize: number }>(),
    'Detail Opened': props<{ id: number }>(),
    'Outcome Recorded': props<{ id: number; outcome: CodOutcomeChoice; note: string }>(),
    'Order Cancelled': props<{ id: number; note: string }>(),
    'Flag Cleared': props<{ id: number }>(),
    'Notice Dismissed': emptyProps(),
  },
});

export const OrderQueueApiActions = createActionGroup({
  source: 'Order Queue API',
  events: {
    'Load Succeeded': props<{ result: Page<OrderListItem> }>(),
    'Load Failed': props<{ error: ApiError }>(),
    'Detail Succeeded': props<{ order: OrderDetail }>(),
    'Detail Failed': props<{ error: ApiError }>(),
    'Action Succeeded': props<{ order: OrderDetail; notice: string }>(),
    'Action Failed': props<{ error: ApiError }>(),
  },
});
