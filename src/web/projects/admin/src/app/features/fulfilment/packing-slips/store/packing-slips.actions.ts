import { createActionGroup, emptyProps, props } from '@ngrx/store';
import { ApiError } from '@core/http';
import { PackingSlip, PrintResult } from '../models';

export const PackingSlipPageActions = createActionGroup({
  source: 'Packing Slip Page',
  events: {
    Opened: props<{ orderId: number }>(),
    'Print Clicked': props<{ orderId: number }>(),
    'Notice Dismissed': emptyProps(),
  },
});

export const PackingSlipApiActions = createActionGroup({
  source: 'Packing Slip API',
  events: {
    'Load Succeeded': props<{ slip: PackingSlip }>(),
    'Load Failed': props<{ error: ApiError }>(),
    'Print Succeeded': props<{ result: PrintResult }>(),
    'Print Failed': props<{ error: ApiError }>(),
  },
});
