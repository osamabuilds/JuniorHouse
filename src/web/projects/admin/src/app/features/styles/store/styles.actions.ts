import { createActionGroup, emptyProps, props } from '@ngrx/store';
import { ApiError } from '@core/http';
import { StyleDto, StyleFormValue, StyleSummaryDto } from '../models';

export const StylesPageActions = createActionGroup({
  source: 'Styles Page',
  events: {
    Opened: emptyProps(),
    'Search Changed': props<{ searchText: string }>(),
    'Category Filter Changed': props<{ categoryId: number | null }>(),
    'Active Only Toggled': emptyProps(),
    'Create Started': emptyProps(),
    'Edit Requested': props<{ id: number }>(),
    'Edit Cancelled': emptyProps(),
    'Form Invalid': props<{ error: ApiError }>(),
    'Create Submitted': props<{ value: StyleFormValue }>(),
    'Update Submitted': props<{ id: number; value: Omit<StyleFormValue, 'code'> }>(),
  },
});

export const StylesApiActions = createActionGroup({
  source: 'Styles API',
  events: {
    'Load Styles Succeeded': props<{ styles: StyleSummaryDto[] }>(),
    'Load Styles Failed': props<{ error: ApiError }>(),
    'Load Style Succeeded': props<{ style: StyleDto }>(),
    'Load Style Failed': props<{ error: ApiError }>(),
    'Save Succeeded': emptyProps(),
    'Save Failed': props<{ error: ApiError }>(),
  },
});
