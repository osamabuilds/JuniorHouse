import { createActionGroup, emptyProps, props } from '@ngrx/store';
import { ApiError } from '@core/http';
import { Page } from '@shared';
import { AdjustmentReasonDto } from '@features/warehouse/adjustments';
import { BinAgeDto, CountReasonInput, CountReviewDto, CountStatusFilter, CycleCountDto, CycleCountRowDto, SubmitLinesValue } from '../models';

export const CycleCountsPageActions = createActionGroup({
  source: 'Cycle Counts Page',
  events: {
    'List Opened': emptyProps(),
    'Status Filter Changed': props<{ status: CountStatusFilter }>(),
    'Counts Page Changed': props<{ page: number }>(),
    'Bins Page Changed': props<{ page: number }>(),
    'Bin Zone Changed': props<{ zone: string }>(),
    'Bin Toggled': props<{ binId: number }>(),
    'Create Submitted': props<{ actingPerson: string }>(),
    'Detail Opened': props<{ id: number }>(),
    'Lines Submitted': props<{ id: number; value: SubmitLinesValue }>(),
    'Review Requested': props<{ id: number }>(),
    'Review Closed': emptyProps(),
    'Reason Chosen': props<{ key: string; reasonId: number }>(),
    'Reconfirm Toggled': props<{ key: string; reconfirmed: boolean }>(),
    'Confirm Submitted': props<{ id: number; reasons: readonly CountReasonInput[]; actingPerson: string }>(),
    'Cancel Submitted': props<{ id: number; actingPerson: string }>(),
    'Notice Dismissed': emptyProps(),
  },
});

export const CycleCountsApiActions = createActionGroup({
  source: 'Cycle Counts API',
  events: {
    'Load Counts Succeeded': props<{ result: Page<CycleCountRowDto> }>(),
    'Load Counts Failed': props<{ error: ApiError }>(),
    'Load Bins Succeeded': props<{ result: Page<BinAgeDto> }>(),
    'Load Bins Failed': props<{ error: ApiError }>(),
    'Load Reasons Succeeded': props<{ reasons: readonly AdjustmentReasonDto[] }>(),
    'Create Succeeded': props<{ count: CycleCountDto }>(),
    'Create Failed': props<{ error: ApiError }>(),
    'Load Detail Succeeded': props<{ count: CycleCountDto }>(),
    'Load Detail Failed': props<{ error: ApiError }>(),
    'Submit Lines Succeeded': props<{ count: CycleCountDto }>(),
    'Submit Lines Failed': props<{ error: ApiError }>(),
    'Load Review Succeeded': props<{ review: CountReviewDto }>(),
    'Load Review Failed': props<{ error: ApiError }>(),
    'Confirm Succeeded': props<{ count: CycleCountDto }>(),
    'Confirm Failed': props<{ error: ApiError }>(),
    'Cancel Succeeded': props<{ count: CycleCountDto }>(),
    'Cancel Failed': props<{ error: ApiError }>(),
  },
});
