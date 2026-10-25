import { createFeature, createReducer, on } from '@ngrx/store';
import { Home } from '../models/home.model';
import { Menu } from '../models/menu.model';
import { NavigationActions } from './navigation.actions';

export interface NavigationState {
  readonly menu: Menu | null;
  readonly home: Home | null;
  readonly status: 'idle' | 'loading' | 'loaded' | 'failed';
}

export const initialNavigationState: NavigationState = { menu: null, home: null, status: 'idle' };

export const navigationReducer = createReducer(
  initialNavigationState,
  on(NavigationActions.loadMenu, (s): NavigationState => ({ ...s, status: 'loading' })),
  on(NavigationActions.loadMenuSuccess, (s, { menu }): NavigationState => ({ ...s, menu, status: 'loaded' })),
  // A failed load keeps the last good menu (none on a first load): the header shows no entries rather than a wrong one.
  on(NavigationActions.loadMenuFailure, (s): NavigationState => ({ ...s, status: 'failed' })),
  on(NavigationActions.loadHomeSuccess, (s, { home }): NavigationState => ({ ...s, home })),
);

export const navigationFeature = createFeature({ name: 'navigation', reducer: navigationReducer });
