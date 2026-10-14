import { createActionGroup, emptyProps, props } from '@ngrx/store';
import { ApiError } from '@core/http';
import { Page } from '@shared';
import { StockMovementDto, StockVariantRowDto } from '../models';

export const StockPageActions = createActionGroup({
  source: 'Stock Page',
  events: {
    Opened: emptyProps(),
    'Search Changed': props<{ searchText: string }>(),
    'Page Changed': props<{ page: number }>(),
    'Page Size Changed': props<{ pageSize: number }>(),
    'Refresh Requested': emptyProps(),
    'History Requested': props<{ variant: StockVariantRowDto }>(),
    'History Page Changed': props<{ page: number }>(),
    'History Closed': emptyProps(),
  },
});

export const StockApiActions = createActionGroup({
  source: 'Stock API',
  events: {
    'Load Stock Succeeded': props<{ result: Page<StockVariantRowDto> }>(),
    'Load Stock Failed': props<{ error: ApiError }>(),
    'Load Movements Succeeded': props<{ result: Page<StockMovementDto> }>(),
    'Load Movements Failed': props<{ error: ApiError }>(),
  },
});
