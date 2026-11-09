import { createActionGroup, emptyProps, props } from '@ngrx/store';
import { ApiError } from '@core/http';
import { Page } from '@shared';
import { RaiseExceptionRequest } from '../../pick-exceptions';
import { CompleteResult, PickList, QueueItem, ScanOutcome } from '../models';

export const PickQueuePageActions = createActionGroup({
  source: 'Pick Queue Page',
  events: {
    Opened: emptyProps(),
    'Page Changed': props<{ page: number }>(),
    'Page Size Changed': props<{ pageSize: number }>(),
    Refreshed: emptyProps(),
  },
});

export const PickStationPageActions = createActionGroup({
  source: 'Pick Station Page',
  events: {
    /** The picker opened an order: the pick is taken for them (PCK-9). */
    Opened: props<{ orderId: number }>(),
    Scanned: props<{ orderId: number; code: string }>(),
    'Complete Clicked': props<{ orderId: number }>(),
    'Release Clicked': props<{ orderId: number }>(),
    'Exception Flagged': props<{ orderId: number; request: RaiseExceptionRequest }>(),
    'Notice Dismissed': emptyProps(),
  },
});

export const PickListsApiActions = createActionGroup({
  source: 'Pick Lists API',
  events: {
    'Queue Succeeded': props<{ result: Page<QueueItem> }>(),
    'Queue Failed': props<{ error: ApiError }>(),
    'Claim Succeeded': props<{ list: PickList }>(),
    /** Someone else holds it, or it cannot be picked (any longer): the list is then read, not worked. */
    'Claim Refused': props<{ error: ApiError; orderId: number }>(),
    'List Succeeded': props<{ list: PickList }>(),
    'List Failed': props<{ error: ApiError }>(),
    'Scan Succeeded': props<{ outcome: ScanOutcome; orderId: number }>(),
    'Scan Failed': props<{ error: ApiError }>(),
    'Complete Succeeded': props<{ result: CompleteResult; orderId: number }>(),
    'Complete Failed': props<{ error: ApiError }>(),
    'Release Succeeded': props<{ orderId: number }>(),
    'Release Failed': props<{ error: ApiError }>(),
    'Exception Succeeded': props<{ orderId: number }>(),
    'Exception Failed': props<{ error: ApiError }>(),
  },
});
