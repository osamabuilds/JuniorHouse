import { createActionGroup, props } from '@ngrx/store';
import { ListingResult } from '../models/listing.model';
import { ListingFilter, ListingScope } from '../models/listing-query';

export const ListingsActions = createActionGroup({
  source: 'Listings',
  events: {
    Load: props<{ key: string; scope: ListingScope; filter: ListingFilter }>(),
    'Load Done': props<{ key: string; result: ListingResult }>(),
  },
});
