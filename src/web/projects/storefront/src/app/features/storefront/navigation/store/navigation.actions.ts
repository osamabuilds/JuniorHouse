import { createActionGroup, emptyProps, props } from '@ngrx/store';
import { Menu } from '../models/menu.model';

export const NavigationActions = createActionGroup({
  source: 'Navigation',
  events: {
    'Load Menu': emptyProps(),
    'Load Menu Success': props<{ menu: Menu }>(),
    'Load Menu Failure': props<{ error: string }>(),
  },
});
