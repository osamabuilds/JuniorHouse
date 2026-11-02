import { createFeature, createReducer, on } from '@ngrx/store';
import { Cart, EMPTY_CART } from '../models/cart.model';
import { CartActions } from './cart.actions';

export type CartStatus = 'idle' | 'loading' | 'ready' | 'error';

export interface CartState {
  readonly status: CartStatus;
  readonly cart: Cart;
  /** A change is in flight: the steppers wait so two quick presses are not lost or doubled. */
  readonly busy: boolean;
  /** The server's words for the last refusal ("Only 3 are in stock."), shown with the basket. */
  readonly message: string | null;
  /** The quick-view drawer: opens after Add to bag and from the header bag button. */
  readonly drawerOpen: boolean;
}

export const initialCartState: CartState = { status: 'idle', cart: EMPTY_CART, busy: false, message: null, drawerOpen: false };

export const cartFeature = createFeature({
  name: 'cart',
  reducer: createReducer(
    initialCartState,
    // The basket on screen stays while it reloads, so the page and the header count never flash empty.
    on(CartActions.load, (s): CartState => ({ ...s, status: s.status === 'ready' ? 'ready' : 'loading' })),
    on(CartActions.loadDone, (s, { result }): CartState =>
      result.status === 'ok' ? { ...s, status: 'ready', cart: result.cart } : { ...s, status: s.status === 'ready' ? 'ready' : 'error' },
    ),
    on(CartActions.add, CartActions.setQuantity, CartActions.remove, (s): CartState => ({ ...s, busy: true, message: null })),
    on(CartActions.changeDone, (s, { kind, result }): CartState => {
      switch (result.status) {
        case 'ok':
          return { ...s, busy: false, status: 'ready', cart: result.cart, message: null, drawerOpen: kind === 'add' ? true : s.drawerOpen };
        case 'refused':
          return { ...s, busy: false, message: result.message };
        default:
          return { ...s, busy: false, message: 'Something went wrong on our side. Please try again in a moment.' };
      }
    }),
    on(CartActions.openDrawer, (s): CartState => ({ ...s, drawerOpen: true })),
    on(CartActions.closeDrawer, (s): CartState => ({ ...s, drawerOpen: false })),
    on(CartActions.clearMessage, (s): CartState => ({ ...s, message: null })),
  ),
});

export const { selectStatus: selectCartStatus, selectCart, selectBusy: selectCartBusy, selectMessage: selectCartMessage, selectDrawerOpen: selectCartDrawerOpen } = cartFeature;
