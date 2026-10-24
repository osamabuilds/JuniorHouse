import { createActionGroup, props } from '@ngrx/store';
import { ProductSuggestion } from '../../../../shared/data/mock-products';

export const SearchSuggestActions = createActionGroup({
  source: 'Search suggest',
  events: {
    'Query Changed': props<{ query: string }>(),
    'Results Loaded': props<{ query: string; results: readonly ProductSuggestion[] }>(),
    Cleared: props<Record<string, never>>(),
  },
});
