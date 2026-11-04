import { createActionGroup, emptyProps, props } from '@ngrx/store';
import { ChangeResult, IdsResult, ListResult } from '../models/wishlist.model';

export const WishlistActions = createActionGroup({
  source: 'Wishlist',
  events: {
    /** The sign-in state became known or changed: a signed-in customer's list is read (after merging a guest's), a signed-out one comes from the browser. */
    'Session Known': props<{ signedIn: boolean }>(),
    'Ids Loaded': props<{ result: IdsResult }>(),
    'Guest Loaded': props<{ ids: readonly number[] }>(),
    /** The heart was pressed: save the product, or take it off if it is already saved. */
    Toggle: props<{ productId: number }>(),
    /** Take the product off the list whatever the hearts say (the wishlist page's Remove button). */
    Remove: props<{ productId: number }>(),
    'Change Done': props<{ productId: number; result: ChangeResult }>(),
    'Close Prompt': emptyProps(),
    'Load Page': props<{ page: number }>(),
    'Load Page Done': props<{ result: ListResult }>(),
    'Clear Message': emptyProps(),
  },
});
