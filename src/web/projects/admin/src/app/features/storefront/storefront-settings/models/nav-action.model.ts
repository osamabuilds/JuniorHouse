import { NavItem } from './storefront-settings.model';

/** Where an entry sits in the tree: the index at each level, from the top. */
export type NavNodePath = readonly number[];

export type NavMoveDirection = 'up' | 'down' | 'indent' | 'outdent';

/** What a navigation entry asks its editor to do; each is a plain button, so none depends on dragging (WCAG 2.5.7). */
export type NavAction =
  | { readonly type: 'update'; readonly path: NavNodePath; readonly patch: Partial<NavItem> }
  | { readonly type: 'add-member'; readonly path: NavNodePath }
  | { readonly type: 'remove'; readonly path: NavNodePath }
  | { readonly type: 'move'; readonly path: NavNodePath; readonly direction: NavMoveDirection };
