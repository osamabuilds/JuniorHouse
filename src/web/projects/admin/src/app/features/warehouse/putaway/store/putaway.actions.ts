import { createActionGroup, emptyProps, props } from '@ngrx/store';
import { ApiError } from '@core/http';
import { Page } from '@shared';
import { ConfirmPutawayValue, PutawayItemRowDto, PutawayRecordDto, PutawaySelection, ScanLookupDto } from '../models';

export const PutawayPageActions = createActionGroup({
  source: 'Putaway Page',
  events: {
    Opened: emptyProps(),
    'Items Page Changed': props<{ page: number }>(),
    'Item Search Changed': props<{ search: string }>(),
    'Records Page Changed': props<{ page: number }>(),
    'Refresh Requested': emptyProps(),
    'Scan Submitted': props<{ gtin: string }>(),
    'Scan Cleared': emptyProps(),
    'Item Selected': props<{ selection: PutawaySelection }>(),
    'Selection Cleared': emptyProps(),
    'Confirm Submitted': props<{ itemId: number; value: ConfirmPutawayValue }>(),
    'Notice Dismissed': emptyProps(),
  },
});

export const PutawayApiActions = createActionGroup({
  source: 'Putaway API',
  events: {
    'Load Items Succeeded': props<{ result: Page<PutawayItemRowDto> }>(),
    'Load Items Failed': props<{ error: ApiError }>(),
    'Load Records Succeeded': props<{ result: Page<PutawayRecordDto> }>(),
    'Load Records Failed': props<{ error: ApiError }>(),
    'Scan Succeeded': props<{ result: ScanLookupDto }>(),
    'Scan Failed': props<{ error: ApiError }>(),
    'Confirm Succeeded': props<{ record: PutawayRecordDto }>(),
    'Confirm Failed': props<{ error: ApiError }>(),
  },
});
