import { createActionGroup, emptyProps, props } from '@ngrx/store';
import { ApiError } from '@core/http';
import { Page } from '@shared';
import { QueueItem } from '../../pick-lists';
import { PackComplete, PackScanOutcome, PackView, WeightOutcome } from '../models';

export const PackQueuePageActions = createActionGroup({
  source: 'Pack Queue Page',
  events: {
    Opened: emptyProps(),
    'Page Changed': props<{ page: number }>(),
    'Page Size Changed': props<{ pageSize: number }>(),
    Refreshed: emptyProps(),
  },
});

export const PackStationPageActions = createActionGroup({
  source: 'Pack Station Page',
  events: {
    /** The packer opened a picked order: the parcel is theirs (PAK-10). */
    Opened: props<{ orderId: number }>(),
    Scanned: props<{ orderId: number; code: string }>(),
    'Weight Submitted': props<{ orderId: number; grams: number }>(),
    'Complete Clicked': props<{ orderId: number; note: string | null }>(),
    'Notice Dismissed': emptyProps(),
  },
});

export const PackStationApiActions = createActionGroup({
  source: 'Pack Station API',
  events: {
    'Queue Succeeded': props<{ result: Page<QueueItem> }>(),
    'Queue Failed': props<{ error: ApiError }>(),
    'Open Succeeded': props<{ view: PackView }>(),
    'Open Refused': props<{ error: ApiError; orderId: number }>(),
    'View Succeeded': props<{ view: PackView }>(),
    'View Failed': props<{ error: ApiError }>(),
    'Scan Succeeded': props<{ outcome: PackScanOutcome }>(),
    'Scan Failed': props<{ error: ApiError }>(),
    'Weight Succeeded': props<{ outcome: WeightOutcome }>(),
    'Weight Failed': props<{ error: ApiError }>(),
    'Complete Succeeded': props<{ result: PackComplete; orderId: number }>(),
    'Complete Failed': props<{ error: ApiError }>(),
  },
});
