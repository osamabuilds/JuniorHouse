import { createActionGroup, props } from '@ngrx/store';
import { CodAnswer, CodRespondResult, CodStatusResult } from '../models/cod.model';

export const CodActions = createActionGroup({
  source: 'Cod Confirm',
  events: {
    /** The page opened with a link: read what it says, change nothing. */
    'Status Requested': props<{ token: string }>(),
    'Status Loaded': props<{ result: CodStatusResult }>(),
    /** A button was pressed (COD-12, COD-15). */
    Answered: props<{ token: string; answer: CodAnswer }>(),
    'Answer Done': props<{ result: CodRespondResult }>(),
  },
});
