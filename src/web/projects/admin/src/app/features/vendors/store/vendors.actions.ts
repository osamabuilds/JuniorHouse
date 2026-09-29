import { createActionGroup, emptyProps, props } from '@ngrx/store';
import { ApiError } from '@core/http';
import { CreateVendorValue, UpdateVendorValue, VendorDto, VendorSummaryDto } from '../models';

export const VendorsPageActions = createActionGroup({
  source: 'Vendors Page',
  events: {
    Opened: emptyProps(),
    'Search Changed': props<{ searchText: string }>(),
    'Specialisation Filter Changed': props<{ specialisationId: number | null }>(),
    'Active Only Toggled': emptyProps(),
    'Create Started': emptyProps(),
    'Edit Requested': props<{ id: number }>(),
    'Edit Cancelled': emptyProps(),
    'Form Invalid': props<{ error: ApiError }>(),
    'Create Submitted': props<{ value: CreateVendorValue }>(),
    'Update Submitted': props<{ id: number; value: UpdateVendorValue }>(),
  },
});

export const VendorsApiActions = createActionGroup({
  source: 'Vendors API',
  events: {
    'Load Vendors Succeeded': props<{ vendors: VendorSummaryDto[] }>(),
    'Load Vendors Failed': props<{ error: ApiError }>(),
    'Load Vendor Succeeded': props<{ vendor: VendorDto }>(),
    'Load Vendor Failed': props<{ error: ApiError }>(),
    'Save Succeeded': emptyProps(),
    'Save Failed': props<{ error: ApiError }>(),
  },
});
