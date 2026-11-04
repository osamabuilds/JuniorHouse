import { createSelector } from '@ngrx/store';
import { selectCart } from './cart.reducer';

export { selectCart, selectCartBusy, selectCartCodeBusy, selectCartCodeError, selectCartDrawerOpen, selectCartMessage, selectCartStatus } from './cart.reducer';

/** The number on the header's bag button. */
export const selectCartItemCount = createSelector(selectCart, (cart) => cart.itemCount);

export const selectCartIsEmpty = createSelector(selectCart, (cart) => cart.lines.length === 0);
