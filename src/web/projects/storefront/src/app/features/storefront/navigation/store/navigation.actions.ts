import { createActionGroup, emptyProps, props } from '@ngrx/store';
import { Home } from '../models/home.model';
import { Menu } from '../models/menu.model';

export const NavigationActions = createActionGroup({
  source: 'Navigation',
  events: {
    'Load Menu': emptyProps(),
    'Load Menu Success': props<{ menu: Menu }>(),
    'Load Menu Failure': props<{ error: string }>(),
    'Load Home': emptyProps(),
    'Load Home Success': props<{ home: Home }>(),
    'Load Home Failure': props<{ error: string }>(),
  },
});
