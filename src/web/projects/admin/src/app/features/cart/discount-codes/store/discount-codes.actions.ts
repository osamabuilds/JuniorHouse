import { createActionGroup, emptyProps, props } from '@ngrx/store';
import { ApiError } from '@core/http';
import { Page } from '@shared';
import { DiscountCodeDto, DiscountCodeInput } from '../models';

export const DiscountCodesPageActions = createActionGroup({
  source: 'Discount Codes Page',
  events: {
    Opened: emptyProps(),
    'Page Changed': props<{ page: number }>(),
    'Page Size Changed': props<{ pageSize: number }>(),
    'Edit Started': props<{ id: number }>(),
    'Edit Cancelled': emptyProps(),
    Submitted: props<{ input: DiscountCodeInput }>(),
    Switched: props<{ id: number; active: boolean }>(),
    'Notice Dismissed': emptyProps(),
  },
});

export const DiscountCodesApiActions = createActionGroup({
  source: 'Discount Codes API',
  events: {
    'Load Succeeded': props<{ result: Page<DiscountCodeDto> }>(),
    'Load Failed': props<{ error: ApiError }>(),
    'Save Succeeded': props<{ code: DiscountCodeDto; created: boolean }>(),
    'Save Failed': props<{ error: ApiError }>(),
    'Switch Succeeded': props<{ code: DiscountCodeDto }>(),
    'Switch Failed': props<{ error: ApiError }>(),
  },
});
