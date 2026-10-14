import { createActionGroup, emptyProps, props } from '@ngrx/store';
import { ApiError } from '@core/http';
import { Page } from '@shared';
import { AdjustmentDto, AdjustmentReasonDto, PostAdjustmentValue } from '../models';

export const AdjustmentsPageActions = createActionGroup({
  source: 'Adjustments Page',
  events: {
    Opened: emptyProps(),
    'Reason Filter Changed': props<{ reasonId: number | null }>(),
    'Page Changed': props<{ page: number }>(),
    'Page Size Changed': props<{ pageSize: number }>(),
    'Refresh Requested': emptyProps(),
    'Form Opened': emptyProps(),
    'Form Closed': emptyProps(),
    'Submitted': props<{ value: PostAdjustmentValue }>(),
    'Reconfirm Declined': emptyProps(),
    'Notice Dismissed': emptyProps(),
  },
});

export const AdjustmentsApiActions = createActionGroup({
  source: 'Adjustments API',
  events: {
    'Load Adjustments Succeeded': props<{ result: Page<AdjustmentDto> }>(),
    'Load Adjustments Failed': props<{ error: ApiError }>(),
    'Load Reasons Succeeded': props<{ reasons: readonly AdjustmentReasonDto[] }>(),
    'Load Reasons Failed': props<{ error: ApiError }>(),
    'Post Succeeded': props<{ adjustment: AdjustmentDto }>(),
    'Post Failed': props<{ error: ApiError; value: PostAdjustmentValue }>(),
  },
});
