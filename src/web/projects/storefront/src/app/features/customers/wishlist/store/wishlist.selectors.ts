import { createSelector } from '@ngrx/store';
import { wishlistFeature } from './wishlist.reducer';

export const {
  selectSignedIn: selectWishlistSignedIn,
  selectIdsKnown: selectWishlistIdsKnown,
  selectStatus: selectWishlistStatus,
  selectPage: selectWishlistPage,
  selectPromptOpen: selectWishlistPromptOpen,
  selectMessage: selectWishlistMessage,
} = wishlistFeature;

/** The product ids drawn as saved: the account's list when signed in, else what this browser remembers. */
export const selectSavedIds = createSelector(wishlistFeature.selectSignedIn, wishlistFeature.selectIds, wishlistFeature.selectGuestIds, (signedIn, ids, guest) =>
  signedIn ? ids : guest,
);

/** How many products are saved: shown beside the header link. */
export const selectWishlistCount = createSelector(selectSavedIds, (ids) => ids.length);
