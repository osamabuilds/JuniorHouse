import { createFeature, createReducer, on } from '@ngrx/store';
import { Menu } from '../models/menu.model';
import { NavigationActions } from './navigation.actions';

export interface NavigationState {
  readonly menu: Menu | null;
  readonly status: 'idle' | 'loading' | 'loaded' | 'failed';
}

export const initialNavigationState: NavigationState = { menu: null, status: 'idle' };

export const navigationReducer = createReducer(
  initialNavigationState,
  on(NavigationActions.loadMenu, (s): NavigationState => ({ ...s, status: 'loading' })),
  on(NavigationActions.loadMenuSuccess, (_, { menu }): NavigationState => ({ menu, status: 'loaded' })),
  // A failed load keeps the last good menu (none on a first load): the header shows no entries rather than a wrong one.
  on(NavigationActions.loadMenuFailure, (s): NavigationState => ({ ...s, status: 'failed' })),
);

export const navigationFeature = createFeature({ name: 'navigation', reducer: navigationReducer });
