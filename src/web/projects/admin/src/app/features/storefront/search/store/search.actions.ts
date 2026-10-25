import { createActionGroup, emptyProps, props } from '@ngrx/store';
import { ApiError } from '@core/http';
import { Page } from '@shared';
import { MissRow, SynonymFormValue, SynonymGroup } from '../models';

export const SearchAdminPageActions = createActionGroup({
  source: 'Search Admin Page',
  events: {
    'Synonyms Opened': emptyProps(),
    'Synonyms Page Changed': props<{ page: number }>(),
    'Synonyms Page Size Changed': props<{ pageSize: number }>(),
    'Create Started': emptyProps(),
    'Edit Started': props<{ group: SynonymGroup }>(),
    'Edit Cancelled': emptyProps(),
    'Form Invalid': props<{ error: ApiError }>(),
    'Create Submitted': props<{ value: SynonymFormValue }>(),
    'Update Submitted': props<{ id: number; rowVersion: number; value: SynonymFormValue }>(),
    'Delete Requested': props<{ id: number }>(),
    'Delete Cancelled': emptyProps(),
    'Delete Confirmed': props<{ id: number }>(),
    'Misses Opened': emptyProps(),
    'Misses Page Changed': props<{ page: number }>(),
    'Misses Page Size Changed': props<{ pageSize: number }>(),
  },
});

export const SearchAdminApiActions = createActionGroup({
  source: 'Search Admin API',
  events: {
    'Load Synonyms Succeeded': props<{ result: Page<SynonymGroup> }>(),
    'Load Synonyms Failed': props<{ error: ApiError }>(),
    'Save Succeeded': emptyProps(),
    'Save Failed': props<{ error: ApiError }>(),
    'Delete Succeeded': emptyProps(),
    'Delete Failed': props<{ error: ApiError }>(),
    'Load Misses Succeeded': props<{ result: Page<MissRow> }>(),
    'Load Misses Failed': props<{ error: ApiError }>(),
  },
});
