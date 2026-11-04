import { createActionGroup, emptyProps, props } from '@ngrx/store';
import { MineResult, SubscribeResult } from '../models/newsletter.model';

export const NewsletterActions = createActionGroup({
  source: 'Newsletter',
  events: {
    /** The footer form was submitted. */
    Subscribe: props<{ email: string }>(),
    'Subscribe Done': props<{ result: SubscribeResult }>(),
    /** Back to an empty form (the visitor wants to subscribe another address). */
    'Reset Footer': emptyProps(),
    /** The preferences page opened. */
    'Load Mine': emptyProps(),
    'Load Mine Done': props<{ result: MineResult }>(),
    /** The preferences switch was pressed. */
    'Set Mine': props<{ subscribed: boolean }>(),
    'Set Mine Done': props<{ result: MineResult }>(),
  },
});
