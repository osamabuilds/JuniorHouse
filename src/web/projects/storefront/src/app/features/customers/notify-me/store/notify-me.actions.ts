import { createActionGroup, props } from '@ngrx/store';
import { NotifyResult } from '../models/notify-me.model';

export const NotifyMeActions = createActionGroup({
  source: 'Notify Me',
  events: {
    /** The form for one size was submitted. */
    Request: props<{ variantId: number; email: string }>(),
    'Request Done': props<{ variantId: number; result: NotifyResult }>(),
    /** Typing again after an answer clears it, so an old message never sits beside a new address. */
    Reset: props<{ variantId: number }>(),
  },
});
