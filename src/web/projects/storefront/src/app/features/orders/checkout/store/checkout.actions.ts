import { createActionGroup, emptyProps, props } from '@ngrx/store';
import { CheckoutForm, City, PlaceResult, Province, SummaryResult } from '../models/checkout.model';

export const CheckoutActions = createActionGroup({
  source: 'Checkout',
  events: {
    /** The checkout page opened: start from a clean slate and load the summary and the provinces. */
    Opened: emptyProps(),
    /** The city or the payment method changed: the delivery fee and the total are asked of the server again. */
    'Summary Requested': props<{ cityId: number | null; method: string }>(),
    'Summary Loaded': props<{ result: SummaryResult }>(),
    'Provinces Loaded': props<{ provinces: readonly Province[] }>(),
    'Province Chosen': props<{ provinceId: number }>(),
    'Cities Loaded': props<{ cities: readonly City[] }>(),
    Submitted: props<{ form: CheckoutForm; key: string }>(),
    'Submit Done': props<{ result: PlaceResult }>(),
  },
});
