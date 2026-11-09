import { createActionGroup, props } from '@ngrx/store';
import { CollectionsPageResult } from '../models/collections-page.model';

export const CollectionsPageActions = createActionGroup({
  source: 'Collections Page',
  events: {
    Load: props<{ key: string; page: number }>(),
    'Load Done': props<{ key: string; result: CollectionsPageResult }>(),
  },
});
