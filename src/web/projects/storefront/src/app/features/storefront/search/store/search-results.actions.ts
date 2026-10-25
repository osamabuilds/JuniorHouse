import { createActionGroup, props } from '@ngrx/store';
import { ListingFilter } from '../../listings';
import { SearchResult } from '../models/search.model';

export const SearchResultsActions = createActionGroup({
  source: 'Search results',
  events: {
    Load: props<{ key: string; query: string; filter: ListingFilter }>(),
    'Load Done': props<{ key: string; result: SearchResult }>(),
  },
});
