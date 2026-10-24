import { createActionGroup, props } from '@ngrx/store';
import { PageResult } from '../services/page-api.service';

export const PageActions = createActionGroup({
  source: 'Page',
  events: {
    Load: props<{ slug: string }>(),
    Loaded: props<{ slug: string; result: PageResult }>(),
  },
});
