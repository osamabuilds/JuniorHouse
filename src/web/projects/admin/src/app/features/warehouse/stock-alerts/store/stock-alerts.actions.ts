import { createActionGroup, emptyProps, props } from '@ngrx/store';
import { ApiError } from '@core/http';
import { Page } from '@shared';
import { AlertLevelFilter, AlertRowDto, ThresholdRowDto } from '../models';

export const StockAlertsPageActions = createActionGroup({
  source: 'Stock Alerts Page',
  events: {
    Opened: emptyProps(),
    'Level Changed': props<{ level: AlertLevelFilter }>(),
    'Unacknowledged Only Changed': props<{ unacknowledgedOnly: boolean }>(),
    'Alerts Page Changed': props<{ page: number }>(),
    'Search Changed': props<{ searchText: string }>(),
    'Thresholds Page Changed': props<{ page: number }>(),
    'Refresh Requested': emptyProps(),
    'Acknowledge Submitted': props<{ id: number; actingPerson: string }>(),
    'Threshold Submitted': props<{ variantRefId: number; quantity: number; actingPerson: string }>(),
    'Notice Dismissed': emptyProps(),
  },
});

export const StockAlertsApiActions = createActionGroup({
  source: 'Stock Alerts API',
  events: {
    'Load Alerts Succeeded': props<{ result: Page<AlertRowDto> }>(),
    'Load Alerts Failed': props<{ error: ApiError }>(),
    'Load Thresholds Succeeded': props<{ result: Page<ThresholdRowDto> }>(),
    'Load Thresholds Failed': props<{ error: ApiError }>(),
    'Acknowledge Succeeded': props<{ alert: AlertRowDto }>(),
    'Acknowledge Failed': props<{ error: ApiError }>(),
    'Threshold Succeeded': props<{ threshold: ThresholdRowDto }>(),
    'Threshold Failed': props<{ error: ApiError }>(),
  },
});
