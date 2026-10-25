import { createSelector } from '@ngrx/store';
import { toNavItems } from '../models/menu-mapper';
import { navigationFeature } from './navigation.reducer';

const { selectMenu } = navigationFeature;
export { selectMenu };

/** The header, mega-menu and mobile menu entries, built from the menu the server returned. */
export const selectMenuEntries = createSelector(selectMenu, (menu) => toNavItems(menu));
