import { EnvironmentProviders, makeEnvironmentProviders } from '@angular/core';
import { provideEffects } from '@ngrx/effects';
import { provideState } from '@ngrx/store';
import { WishlistEffects } from './store/wishlist.effects';
import { wishlistFeature } from './store/wishlist.reducer';

/** The wishlist lives for the whole session (the header shows its count and every product has a heart), so it is provided at the app root. */
export function provideWishlist(): EnvironmentProviders {
  return makeEnvironmentProviders([provideState(wishlistFeature), provideEffects(WishlistEffects)]);
}
