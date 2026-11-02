import { createActionGroup, emptyProps, props } from '@ngrx/store';
import { CartChange, CartResult } from '../models/cart.model';

export const CartActions = createActionGroup({
  source: 'Cart',
  events: {
    Load: emptyProps(),
    'Load Done': props<{ result: CartResult }>(),
    /** Add a variant (the product page's Add to bag). */
    Add: props<{ variantId: number; quantity: number }>(),
    'Set Quantity': props<{ variantId: number; quantity: number }>(),
    Remove: props<{ variantId: number }>(),
    /** The shopper typed a code and pressed Apply (FR-25): never applied on its own. */
    'Apply Code': props<{ code: string }>(),
    'Remove Code': emptyProps(),
    'Code Done': props<{ result: CartResult }>(),
    'Change Done': props<{ kind: CartChange; result: CartResult }>(),
    'Open Drawer': emptyProps(),
    'Close Drawer': emptyProps(),
    'Clear Message': emptyProps(),
  },
});
