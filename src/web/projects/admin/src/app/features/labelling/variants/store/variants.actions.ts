import { createActionGroup, emptyProps, props } from '@ngrx/store';
import { ApiError } from '@core/http';
import { Page } from '@shared';
import { GtinCapacityDto, VariantDetailDto, VariantRowDto, VariantStatusFilter, VoidGtinValue } from '../models';

export const VariantsPageActions = createActionGroup({
  source: 'Variants Page',
  events: {
    Opened: emptyProps(),
    'Search Changed': props<{ searchText: string }>(),
    'Status Changed': props<{ status: VariantStatusFilter }>(),
    'Page Changed': props<{ page: number }>(),
    'Page Size Changed': props<{ pageSize: number }>(),
    'Refresh Requested': emptyProps(),
    'Detail Requested': props<{ id: number }>(),
    'Detail Closed': emptyProps(),
    'Void Form Opened': props<{ variant: VariantRowDto }>(),
    'Void Form Closed': emptyProps(),
    'Void Submitted': props<{ id: number; value: VoidGtinValue }>(),
    'Notice Dismissed': emptyProps(),
  },
});

export const VariantsApiActions = createActionGroup({
  source: 'Variants API',
  events: {
    'Load Variants Succeeded': props<{ result: Page<VariantRowDto> }>(),
    'Load Variants Failed': props<{ error: ApiError }>(),
    'Load Capacity Succeeded': props<{ capacity: GtinCapacityDto }>(),
    'Load Capacity Failed': props<{ error: ApiError }>(),
    'Load Detail Succeeded': props<{ detail: VariantDetailDto }>(),
    'Load Detail Failed': props<{ error: ApiError }>(),
    'Void Succeeded': props<{ detail: VariantDetailDto }>(),
    'Void Failed': props<{ error: ApiError }>(),
  },
});
