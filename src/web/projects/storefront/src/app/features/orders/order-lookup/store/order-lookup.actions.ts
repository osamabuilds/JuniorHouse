import { createActionGroup, emptyProps, props } from '@ngrx/store';
import { LookupInput, LookupResult } from '../models/order-lookup.model';

export const OrderLookupActions = createActionGroup({
  source: 'Order Lookup',
  events: {
    /** The page opened without a link: start from an empty form. */
    'Form Opened': emptyProps(),
    'Lookup Submitted': props<{ input: LookupInput }>(),
    'Lookup Done': props<{ result: LookupResult }>(),
    /** The page opened from the emailed link (OLK-5). */
    'Link Opened': props<{ token: string }>(),
    'Link Done': props<{ result: LookupResult }>(),
    /** "Look up another order". */
    Cleared: emptyProps(),
  },
});
