import { createActionGroup, emptyProps, props } from '@ngrx/store';
import { ApiError } from '@core/http';
import { Page } from '@shared';
import { ShipmentDetail, ShipmentRow } from '../models';

export const ShipmentsPageActions = createActionGroup({
  source: 'Shipments Page',
  events: {
    Opened: emptyProps(),
    'Status Chosen': props<{ status: string }>(),
    'Page Changed': props<{ page: number }>(),
    'Page Size Changed': props<{ pageSize: number }>(),
    Refreshed: emptyProps(),
  },
});

export const ShipmentDetailPageActions = createActionGroup({
  source: 'Shipment Detail Page',
  events: {
    Opened: props<{ id: number }>(),
    'Retry Clicked': props<{ id: number }>(),
    'Notice Dismissed': emptyProps(),
  },
});

export const BookingsApiActions = createActionGroup({
  source: 'Bookings API',
  events: {
    'Load Succeeded': props<{ result: Page<ShipmentRow> }>(),
    'Load Failed': props<{ error: ApiError }>(),
    'Detail Succeeded': props<{ detail: ShipmentDetail }>(),
    'Detail Failed': props<{ error: ApiError }>(),
    'Retry Succeeded': props<{ outcome: string; id: number }>(),
    'Retry Failed': props<{ error: ApiError }>(),
  },
});
