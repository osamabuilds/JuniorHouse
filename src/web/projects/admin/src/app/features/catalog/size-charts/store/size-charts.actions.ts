import { createActionGroup, emptyProps, props } from '@ngrx/store';
import { ApiError } from '@core/http';
import { Page } from '@shared';
import { SizeChartDto, SizeChartFormValue, SizeChartRowDto } from '../models';

export const SizeChartsPageActions = createActionGroup({
  source: 'Size Charts Page',
  events: {
    Opened: emptyProps(),
    'Page Changed': props<{ page: number }>(),
    'Page Size Changed': props<{ pageSize: number }>(),
    'Create Started': emptyProps(),
    'Edit Opened': props<{ id: number }>(),
    'Editor Closed': emptyProps(),
    'Create Submitted': props<{ value: SizeChartFormValue; person: string }>(),
    'Update Submitted': props<{ value: SizeChartFormValue; person: string }>(),
    'Delete Confirmed': props<{ id: number; person: string }>(),
    'Notice Dismissed': emptyProps(),
  },
});

export const SizeChartsApiActions = createActionGroup({
  source: 'Size Charts API',
  events: {
    'Load List Succeeded': props<{ result: Page<SizeChartRowDto> }>(),
    'Load List Failed': props<{ error: ApiError }>(),
    'Load Chart Succeeded': props<{ chart: SizeChartDto }>(),
    'Load Chart Failed': props<{ error: ApiError }>(),
    'Save Succeeded': props<{ chart: SizeChartDto }>(),
    'Save Failed': props<{ error: ApiError }>(),
    'Delete Succeeded': emptyProps(),
    'Delete Failed': props<{ error: ApiError }>(),
  },
});
