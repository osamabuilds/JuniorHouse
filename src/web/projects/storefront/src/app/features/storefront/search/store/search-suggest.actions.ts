import { createActionGroup, props } from '@ngrx/store';
import { SuggestResult } from '../models/search.model';

export const SearchSuggestActions = createActionGroup({
  source: 'Search suggest',
  events: {
    'Query Changed': props<{ query: string }>(),
    'Results Loaded': props<{ query: string; result: SuggestResult }>(),
    Cleared: props<Record<string, never>>(),
  },
});
