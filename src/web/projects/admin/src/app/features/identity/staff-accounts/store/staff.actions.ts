import { createActionGroup, emptyProps, props } from '@ngrx/store';
import { ApiError } from '@core/http';
import { Page } from '@shared';
import { CreateStaffValue, RoleDto, StaffDto, TemporaryPasswordDto } from '../models';

export const StaffPageActions = createActionGroup({
  source: 'Staff Page',
  events: {
    Opened: emptyProps(),
    'Search Changed': props<{ searchText: string }>(),
    'Active Only Toggled': emptyProps(),
    'Page Changed': props<{ page: number }>(),
    'Page Size Changed': props<{ pageSize: number }>(),
    'Create Started': emptyProps(),
    'Back To List Clicked': emptyProps(),
    'Staff Selected': props<{ id: number }>(),
    'Form Invalid': props<{ error: ApiError }>(),
    'Create Submitted': props<{ value: CreateStaffValue }>(),
    'Deactivate Clicked': props<{ id: number }>(),
    'Reactivate Clicked': props<{ id: number }>(),
    'Reset Password Clicked': props<{ id: number }>(),
    'Password Dismissed': emptyProps(),
  },
});

export const StaffApiActions = createActionGroup({
  source: 'Staff API',
  events: {
    'Load Staff Succeeded': props<{ result: Page<StaffDto> }>(),
    'Load Staff Failed': props<{ error: ApiError }>(),
    'Load Roles Succeeded': props<{ roles: readonly RoleDto[] }>(),
    'Load Roles Failed': props<{ error: ApiError }>(),
    'Load Detail Succeeded': props<{ staff: StaffDto }>(),
    'Load Detail Failed': props<{ error: ApiError }>(),
    'Create Succeeded': props<{ password: TemporaryPasswordDto }>(),
    'Reset Succeeded': props<{ password: TemporaryPasswordDto }>(),
    'Status Change Succeeded': props<{ id: number }>(),
    'Action Failed': props<{ error: ApiError }>(),
  },
});
