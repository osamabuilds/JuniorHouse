import { createActionGroup, emptyProps, props } from '@ngrx/store';
import { ApiError } from '@core/http';
import { LookupDto, LookupFormValue } from '../models';

export const ReferenceDataPageActions = createActionGroup({
  source: 'Reference Data Page',
  events: {
    Opened: emptyProps(),
    'Type Selected': props<{ typeKey: string }>(),
    'Search Changed': props<{ searchText: string }>(),
    'Include Inactive Toggled': emptyProps(),
    'Create Started': emptyProps(),
    'Edit Started': props<{ id: number }>(),
    'Edit Cancelled': emptyProps(),
    'Form Invalid': props<{ error: ApiError }>(),
    'Create Submitted': props<{ typeKey: string; value: LookupFormValue }>(),
    'Update Submitted': props<{ typeKey: string; id: number; value: Omit<LookupFormValue, 'code'> }>(),
    'Retire Confirmed': props<{ typeKey: string; id: number }>(),
  },
});

/** Requests a lookup list into the shared cache other features read from. */
export const ReferenceLookupActions = createActionGroup({
  source: 'Reference Lookups',
  events: {
    Requested: props<{ typeKey: string; includeInactive: boolean }>(),
  },
});

export const ReferenceDataApiActions = createActionGroup({
  source: 'Reference Data API',
  events: {
    'Load Items Succeeded': props<{ items: LookupDto[] }>(),
    'Load Items Failed': props<{ error: ApiError }>(),
    'Save Succeeded': emptyProps(),
    'Save Failed': props<{ error: ApiError }>(),
    'Retire Succeeded': emptyProps(),
    'Retire Failed': props<{ error: ApiError }>(),
    'Load Lookups Succeeded': props<{ cacheKey: string; items: LookupDto[] }>(),
    'Load Lookups Failed': props<{ cacheKey: string; error: ApiError }>(),
  },
});
