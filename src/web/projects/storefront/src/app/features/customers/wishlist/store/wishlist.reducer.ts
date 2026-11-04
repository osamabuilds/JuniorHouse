import { createFeature, createReducer, on } from '@ngrx/store';
import { MAX_GUEST_IDS, WishlistItem, WishlistPage } from '../models/wishlist.model';
import { WishlistActions } from './wishlist.actions';

export type WishlistStatus = 'idle' | 'loading' | 'ready' | 'signedOut' | 'error';

export interface WishlistState {
  /** Null until the sign-in state is known (the server render, the first moments in the browser). */
  readonly signedIn: boolean | null;
  /** The signed-in customer's saved product ids (for the hearts), newest first. */
  readonly ids: readonly number[];
  /** What a signed-out shopper tapped, remembered in this browser. */
  readonly guestIds: readonly number[];
  /** Hearts are drawn from the ids only once they are known, so a saved product never flashes unsaved. */
  readonly idsKnown: boolean;
  /** The products this browser remembers, as cards for the wishlist page (signed out only). */
  readonly guestItems: readonly WishlistItem[];
  readonly guestStatus: 'idle' | 'loading' | 'ready' | 'error';
  /** The page being shown on /wishlist. */
  readonly status: WishlistStatus;
  readonly page: WishlistPage | null;
  readonly pageNo: number;
  /** A signed-out shopper pressed a heart: ask them to sign in (WSL-2). */
  readonly promptOpen: boolean;
  readonly message: string | null;
}

export const initialWishlistState: WishlistState = {
  signedIn: null,
  ids: [],
  guestIds: [],
  idsKnown: false,
  guestItems: [],
  guestStatus: 'idle',
  status: 'idle',
  page: null,
  pageNo: 1,
  promptOpen: false,
  message: null,
};

const without = (ids: readonly number[], id: number): readonly number[] => ids.filter((x) => x !== id);

export const wishlistFeature = createFeature({
  name: 'wishlist',
  reducer: createReducer(
    initialWishlistState,
    on(WishlistActions.sessionKnown, (s, { signedIn }): WishlistState =>
      signedIn ? { ...s, signedIn } : { ...s, signedIn, ids: [], idsKnown: false, page: null, status: 'signedOut' },
    ),
    on(WishlistActions.idsLoaded, (s, { result }): WishlistState => (result.status === 'ok' ? { ...s, ids: result.ids, idsKnown: true } : s)),
    on(WishlistActions.guestLoaded, (s, { ids }): WishlistState => ({ ...s, guestIds: ids, idsKnown: true })),
    // The heart answers at once; a failed change reads the list again, so the screen settles on what the server holds.
    on(WishlistActions.toggle, (s, { productId }): WishlistState => {
      if (s.signedIn) {
        return { ...s, message: null, ids: s.ids.includes(productId) ? without(s.ids, productId) : [productId, ...s.ids] };
      }
      if (s.guestIds.includes(productId)) return { ...s, guestIds: without(s.guestIds, productId) };
      return { ...s, guestIds: [productId, ...s.guestIds].slice(0, MAX_GUEST_IDS), promptOpen: true };
    }),
    on(WishlistActions.remove, (s, { productId }): WishlistState =>
      s.signedIn ? { ...s, message: null, ids: without(s.ids, productId) } : { ...s, guestIds: without(s.guestIds, productId) },
    ),
    on(WishlistActions.guestItemsRequested, (s): WishlistState => ({ ...s, guestStatus: s.guestItems.length > 0 ? s.guestStatus : 'loading' })),
    on(WishlistActions.guestItemsLoaded, (s, { items }): WishlistState => (items ? { ...s, guestItems: items, guestStatus: 'ready' } : { ...s, guestStatus: 'error' })),
    on(WishlistActions.changeDone, (s, { result }): WishlistState => {
      switch (result.status) {
        case 'ok':
          return s;
        case 'signedOut':
          return { ...s, signedIn: false, status: 'signedOut', page: null };
        case 'refused':
          return { ...s, message: result.message };
        case 'notFound':
          return { ...s, message: 'That product is no longer available.' };
        default:
          return { ...s, message: 'Something went wrong. Please try again.' };
      }
    }),
    on(WishlistActions.closePrompt, (s): WishlistState => ({ ...s, promptOpen: false })),
    on(WishlistActions.loadPage, (s, { page }): WishlistState => ({ ...s, status: s.page ? s.status : 'loading', pageNo: page })),
    on(WishlistActions.loadPageDone, (s, { result }): WishlistState => {
      switch (result.status) {
        case 'ok':
          return { ...s, status: 'ready', page: result.page, pageNo: result.page.page };
        case 'signedOut':
          return { ...s, status: 'signedOut', page: null };
        default:
          return { ...s, status: 'error' };
      }
    }),
    on(WishlistActions.clearMessage, (s): WishlistState => ({ ...s, message: null })),
  ),
});
